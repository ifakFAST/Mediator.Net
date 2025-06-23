// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using System.Collections.Generic;
using Ifak.Fast.Mediator.BinSeri;

namespace Ifak.Fast.Mediator.Publish;

public abstract class BufferedVarPub(string dataFolder, bool bufferIfOffline) {

    private readonly struct ChunkOfValues(Timestamp time, VariableValues values) {
        public readonly Timestamp Time = time;
        public readonly VariableValues Values = values;
    }

    private enum State {
        Online,
        TransitionToBuffered,
        Buffered,
        TransitionToOnline,
    }

    private readonly AsyncQueue<ChunkOfValues> queue = new();
    private State state;

    private readonly string dataFolder = dataFolder;
    private readonly bool bufferIfOffline = bufferIfOffline;
    protected bool running = true;

    protected Connection? fastConnection = null;
    protected Dictionary<VariableRef, VarInfo> variables2Info = [];

    public void UpdateVarInfos(Connection client, Dictionary<VariableRef, VarInfo> variables2Info) {
        this.variables2Info = variables2Info;
        this.fastConnection = client;
    }

    public virtual Task OnConfigChanged() {
        return Task.FromResult(true);
    }

    public VarInfo GetVariableInfoOrThrow(VariableRef vref) {
        variables2Info.TryGetValue(vref, out VarInfo? v);
        if (v != null) {
            return v;
        }
        throw new Exception($"No meta info found for Variable '{vref}'");
    }

    public void Start() {
        this.state = bufferIfOffline && HasBufferedChuncks() ? State.Buffered : State.Online;
        _ = Runner();
        _ = ClearBufferRunner();
    }

    private void SetState_Online() {
        if (state != State.Online) {
            state = State.Online;
            Console.WriteLine($"Changing {PublisherID} buffered state to Online");
        }
    }

    private void SetState_TransitionToBuffered() {
        if (state != State.TransitionToBuffered) {
            state = State.TransitionToBuffered;
            Console.WriteLine($"Changing {PublisherID} buffered state to TransitionToBuffered");
        }
    }

    private void SetState_Buffered() {
        if (state != State.Buffered) {
            state = State.Buffered;
            Console.WriteLine($"Changing {PublisherID} buffered state to Buffered");
        }
    }

    private void SetState_TransitionToOnline() {
        if (state != State.TransitionToOnline) {
            state = State.TransitionToOnline;
            Console.WriteLine($"Changing {PublisherID} buffered state to TransitionToOnline");
        }
    }

    public void Close() {
        running = false;
    }

    private Timestamp lastUsedTimestamp = Timestamp.Now;

    public void Post(VariableValues values) {
        if (values.Count > 0) {

            var t = Timestamp.Now;
            if (t <= lastUsedTimestamp) {
                t = lastUsedTimestamp + Duration.FromMilliseconds(1);
            }
            lastUsedTimestamp = t;

            var chunk = new ChunkOfValues(t, values);
            queue.Post(chunk);
        }
    }

    public async Task Runner() {

        while (running) {

            ChunkOfValues chunk = await queue.ReceiveAsync();

            if (bufferIfOffline || queue.Count == 0) {
                await Process(chunk);
            }
        }
    }

    private async Task Process(ChunkOfValues chunk) {

        switch (state) {

            case State.Online:
                await Send(chunk);
                break;

            case State.Buffered:
                await Buffer(chunk);
                break;

            case State.TransitionToBuffered:
                while (state == State.TransitionToBuffered) {
                    await Task.Delay(100);
                }
                await Process(chunk);
                break;

            case State.TransitionToOnline:
                while (state == State.TransitionToOnline) {
                    await Task.Delay(500);
                }
                await Process(chunk);
                break;
        }
    }

    private async Task Send(ChunkOfValues chunk) {

        bool sendOK = await DoSend(chunk.Values);

        // Print($"Send completed ok={sendOK}");

        if (!sendOK && bufferIfOffline) {

            SetState_TransitionToBuffered();
            await Buffer(chunk);
            SetState_Buffered();
        }
    }

    protected readonly Dictionary<VariableRef, VTQ> lastSentValues = [];

    protected abstract Task<bool> DoSend(VariableValues values);
    protected abstract string BuffDirName { get; }
    internal abstract string PublisherID { get; }

    private const string FilePrefix = "Chunck_";
    private const string FileSuffix = ".dat";

    private string TheBufferDir {
        get {
            return Path.Combine(dataFolder, BuffDirName, PublisherID);
        }
    }

    private async Task Buffer(ChunkOfValues chunk) {

        string folder = TheBufferDir;

        try {
            Directory.CreateDirectory(folder);
        }
        catch {
            return;
        }

        string file = $"{FilePrefix}{chunk.Time.JavaTicks}{FileSuffix}";
        string path = Path.Combine(folder, file);

        // Print($"Buffer... {file}");

        for (int i = 0; i < 3; i++) {

            try {
                using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                VariableValue_Serializer.Serialize(fileStream, chunk.Values, Common.CurrentBinaryVersion);
                fileStream.Close();
                return;
            }
            catch {

                try {
                    File.Delete(path);
                }
                catch { }

                await Task.Delay(250);
            }
        }
    }

    public async Task ClearBufferRunner() {

        while (running) {

            if (state == State.Online) {
                await Task.Delay(TimeSpan.FromSeconds(5));
                continue;
            }
            else if (state == State.TransitionToBuffered) {
                await Task.Delay(TimeSpan.FromSeconds(1));
                continue;
            }

            // Print("ClearBufferRunner read chunck...");

            DataChunk? chunck = await ReadNextBufferedChunck();

            if (chunck == null) {
                SetState_Online();
                continue;
            }

            int retry = 1;

            while (running) {

                bool sendOK = await DoSend(chunck.Values);
                if (sendOK) {
                    SetState_TransitionToOnline();
                    chunck.Delete();
                    break;
                }
                else {
                    SetState_Buffered();
                    int delaySeconds = Math.Min(15, retry++);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
        }
    }

    private async Task<DataChunk?> ReadNextBufferedChunck() {

        DataChunk? chunk = ReadNextBufferedChunckIntern();
        if (chunk != null) return chunk;

        await Task.Delay(TimeSpan.FromMilliseconds(100));

        chunk = ReadNextBufferedChunckIntern();
        if (chunk != null) return chunk;

        await Task.Delay(TimeSpan.FromMilliseconds(300));

        return ReadNextBufferedChunckIntern();
    }

    private bool HasBufferedChuncks() {
        try {
            foreach (string file in EnumBufferedFiles()) {
                return true;
            }
        }
        catch { }
        return false;
    }

    private DataChunk? ReadNextBufferedChunckIntern() {

        try {

            IEnumerable<string> files = EnumBufferedFiles();

            string fileNext = "";
            long ticksNext = long.MaxValue;

            foreach (string file in files) {
                long ticks = JavaTicksFromFileName(file, long.MaxValue);
                if (ticks < ticksNext) {
                    ticksNext = ticks;
                    fileNext = file;
                }
            }

            if (fileNext == "") {
                return null;
            }

            byte[] data = File.ReadAllBytes(fileNext);
            using var stream = new MemoryStream(data);

            try {
                VariableValues values = VariableValue_Serializer.Deserialize(stream);
                return new DataChunk(values, fileNext);
            }
            catch {
                File.Delete(fileNext);
                return ReadNextBufferedChunckIntern();
            }
        }
        catch {
            return null;
        }
    }

    private IEnumerable<string> EnumBufferedFiles() {

        try {

            string path = TheBufferDir;

            if (!Directory.Exists(path)) {
                return [];
            }

            return Directory.EnumerateFiles(path, $"{FilePrefix}*{FileSuffix}");
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"EnumBufferedFiles: {ex.Message}");
            return [];
        }
    }

    private static long JavaTicksFromFileName(string str, long defaultValue) {
        try {
            int i = str.LastIndexOf(FilePrefix);
            if (i < 0) { return defaultValue; }
            i += FilePrefix.Length;
            int j = str.LastIndexOf(FileSuffix);
            if (j < 0) { return defaultValue; }
            ReadOnlySpan<char> span = str;
            ReadOnlySpan<char> spanNum = span.Slice(i, j - i);
            if (long.TryParse(spanNum, out var t)) { return t; }
            return defaultValue;
        }
        catch { return defaultValue; }
    }

    public sealed class DataChunk(VariableValues values, string fileName) {

        public readonly VariableValues Values = values;
        private readonly string FileName = fileName;

        public void Delete() {
            try {
                File.Delete(FileName);
            }
            catch { }
        }
    }
}

public record VarInfo(
    VariableRef VarRef, 
    Variable    Variable, 
    ClassInfo   ClassInfo, 
    ObjectInfo  Object, 
    ObjectValue ObjectValue, 
    MemInfo[]   Parents) {

    public string Name => VarRef.Name == "Value" ? Object.Name : $"{Object.Name}.{VarRef.Name}";
}

public record MemInfo(
    ClassInfo ClassInfo, 
    ObjectInfo Obj, 
    string Member, 
    int Index);
