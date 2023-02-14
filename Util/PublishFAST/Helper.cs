namespace Publish;

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Formats.Tar;

public static class Helper {

    public static void RunCmd(string cmd, string args) {
        var process = new Process();
        process.StartInfo.FileName = cmd;
        process.StartInfo.Arguments = args;
        process.Start();
        process.WaitForExit();
        int code = process.ExitCode;
        if (code != 0) throw new Exception($"Command failed with err code {code}: {cmd} {args}");
    }

    public static char ReadChar() {
        char res = Console.ReadKey().KeyChar;
        Console.WriteLine();
        return res;
    }

    public static string VerifyFileExists(string file) {
        if (!File.Exists(file)) {
            throw new FileNotFoundException($"File not found: {file}");
        }
        return file;
    }

    public static string VerifyDirExists(string dir) {
        if (!Directory.Exists(dir)) {
            throw new DirectoryNotFoundException($"Directory not found: {dir}");
        }
        return dir;
    }

    public static void DeleteDirectory(string dir) {
        if (Directory.Exists(dir)) {
            var sw = Stopwatch.StartNew();
            Directory.Delete(dir, true);
            sw.Stop();
            Console.WriteLine("Delete took " + sw.ElapsedMilliseconds + " ms");
        }
    }

    public static void DeleteFile(string file) {
        if (File.Exists(file)) {
            File.Delete(file);
        }
    }

    public static void WriteToFile(string file, string content) {
        File.WriteAllText(file, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static void CreateEmptyFile(string file) {
        using var f = File.Create(file);
    }

    public static void EnsureDirectory(string dir) {
        if (!Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }
    }

    public static string EnsureDirectory(params string[] dirs) {
        string p = Path.Combine(dirs);
        EnsureDirectory(p);
        return p;
    }

    public static void CopyFileToDir(string file, string dir) {
        string dest = Path.Combine(dir, Path.GetFileName(file));
        File.Copy(file, dest);
    }

    public static void CopyFileToDir(string file, string dir, string newName) {
        string dest = Path.Combine(dir, newName);
        File.Copy(file, dest);
    }

    public static void CopyDir(string sourceDirectory, string targetDirectory) {

        if (!Directory.Exists(targetDirectory)) {
            Directory.CreateDirectory(targetDirectory);
        }

        string[] files = Directory.GetFiles(sourceDirectory);

        foreach (string file in files) {
            string fileName = Path.GetFileName(file);
            string targetPath = Path.Combine(targetDirectory, fileName);
            File.Copy(file, targetPath, true);
        }

        string[] subdirectories = Directory.GetDirectories(sourceDirectory);

        foreach (string subdirectory in subdirectories) {
            string subdirectoryName = Path.GetFileName(subdirectory);
            string targetPath = Path.Combine(targetDirectory, subdirectoryName);
            CopyDir(subdirectory, targetPath);
        }
    }

    public static string ExtractFromFile(string file, string regex) {
        string txt = File.ReadAllText(file);
        return ExtractFromString(txt, regex);
    }

    public static string ExtractFromString(string text, string regex) {
        Match match = Regex.Match(text, regex);
        if (match.Success) {
            return match.Groups[1].Value;
        }
        throw new Exception($"Regex pattern not found: {regex}");
    }

    public static void Zip(string sourceDirectory, string targetZipFile, bool printFiles) {

        //ZipFile.CreateFromDirectory(sourceDirectory, targetZipFile);

        if (!Directory.Exists(sourceDirectory)) {
            throw new Exception($"The directory does not exist: {sourceDirectory}");
        }

        using FileStream targetZipStream = File.Create(targetZipFile);
        using ZipArchive archive = new ZipArchive(targetZipStream, ZipArchiveMode.Create);

        var files = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories);

        foreach (string file in files) {
            if (printFiles) { Console.WriteLine(file); }
            string entryName = file.Substring(sourceDirectory.Length + 1);
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            using Stream entryStream = entry.Open();
            using FileStream fileStream = File.OpenRead(file);
            fileStream.CopyTo(entryStream);
        }
    }

    public static void TarGz(string sourceDirectory, string targetZipFile, bool printFiles, Func<string, UnixFileMode?> file2Attributes) {

        if (!Directory.Exists(sourceDirectory)) {
            throw new Exception($"The directory does not exist: {sourceDirectory}");
        }

        using FileStream targetStream = File.Create(targetZipFile);
        using var compressor = new GZipStream(targetStream, CompressionLevel.SmallestSize);
        using TarWriter archive = new TarWriter(compressor, format: TarEntryFormat.Pax, leaveOpen: false);

        var files = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories);

        foreach (string file in files) {

            if (printFiles) { Console.WriteLine(file); }
            string entryName = file.Substring(sourceDirectory.Length + 1);

            PaxTarEntry entry = new PaxTarEntry(TarEntryType.RegularFile, entryName.Replace('\\', '/'));

            entry.DataStream = File.OpenRead(file);

            UnixFileMode? attributes = file2Attributes(file);
            if (attributes.HasValue) {
                entry.Mode = attributes.Value;
                Console.WriteLine($"{file} => {attributes.Value}");
            }

            archive.WriteEntry(entry);
        }
    }
}
