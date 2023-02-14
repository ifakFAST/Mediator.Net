namespace Publish;

using System;
using System.Globalization;
using System.IO;

using static Helper;
using static System.Console;

internal class Program {

    static void Main(string[] args) {

        if (args.Length != 1) {
            Error.WriteLine("Expected root folder argumnet");
            return;
        }
        
        string root = Path.GetFullPath(args[0]);
        if (!Directory.Exists(root)) {
            Error.WriteLine($"Root folder '{root}' not found!");
            return;
        }

        try {
            Run(root);
        }
        catch(Exception exp) { 
            Error.WriteLine(exp.Message);
            Error.WriteLine(exp.StackTrace);
            return;
        }
    }

    static void Run(string root) {

        Directory.SetCurrentDirectory(root);

        string lib_proj = VerifyFileExists("Mediator.Net/MediatorLib/MediatorLib.csproj");
        string version  = ExtractFromFile(lib_proj, regex: "<Version>(.+)</Version>");

        WriteLine($"Mediator version: {version}");
        Write("Continue? (y/n): ");
        if (ReadChar() != 'y') {
            return;
        }

        Write("Build Portable or Win64 or Linux64 or All (p/w/l/a): ");
        char mode = ReadChar();

        switch (mode) {
            case 'a': 
                MakeBuild(root, 'p', version);
                MakeBuild(root, 'w', version);
                MakeBuild(root, 'l', version);
                break;
            case 'p':
            case 'w':
            case 'l':
                MakeBuild(root, mode, version);
                break;
            default:
                Error.WriteLine("Invalid choice!");
                return;
        }
    }

    static void MakeBuild(string root, char mode, string version) {

        string baseDir = Directory.GetParent(root)!.FullName;
        string outDir = Path.Combine(baseDir, "Mediator.Export");

        bool windowsOrPortable = mode == 'w' || mode == 'p';
        bool linux = mode == 'l';
        bool selfContained = mode == 'w' || mode == 'l';

        string dir = mode switch {
            'p' => "Portable",
            'w' => "Windows",
            'l' => "Linux",
            _ => throw new Exception()
        };

        string outDirMode = Path.Combine(outDir, dir);

        DeleteDirectory(outDirMode);
        EnsureDirectory(outDirMode);

        EnsureDirectory(outDirMode, "Bin");

        if (windowsOrPortable) {
            string winServiceInDir  = Path.Combine(root, "WindowsService/bin/Release");
            string winServiceOutDir = EnsureDirectory(outDirMode, "Bin", "WinService");
            CopyFileToDir(Path.Combine(winServiceInDir, "WinService.exe"),        winServiceOutDir);
            CopyFileToDir(Path.Combine(winServiceInDir, "WinService.exe.config"), winServiceOutDir);
            CopyFileToDir(Path.Combine(winServiceInDir, "InstallService.bat"),    winServiceOutDir);
            CopyFileToDir(Path.Combine(winServiceInDir, "UninstallService.bat"),  winServiceOutDir);
        }

        string coreOutDir = EnsureDirectory(outDirMode, "Bin", "Mediator");

        string cmdArgs = mode switch {
            'p' => $"publish Mediator.Net/MediatorCore/MediatorCore.csproj -c Release -o \"{coreOutDir}\"",
            'w' => $"publish Mediator.Net/MediatorCore/MediatorCore.csproj -c Release -o \"{coreOutDir}\" -r win-x64   --self-contained",
            'l' => $"publish Mediator.Net/MediatorCore/MediatorCore.csproj -c Release -o \"{coreOutDir}\" -r linux-x64 --self-contained",
            _ => throw new Exception()
        };

        WriteLine(cmdArgs);
        RunCmd("dotnet", cmdArgs);

        string libNetStandardOutDir = EnsureDirectory(outDirMode, "Bin", "MediatorLib", "netstandard2.1");
        RunCmd("dotnet", $"publish Mediator.Net/MediatorLib/MediatorLib.csproj -c Release -f netstandard2.1 -o \"{libNetStandardOutDir}\"");

        string libnet461OutDir = EnsureDirectory(outDirMode, "Bin", "MediatorLib", "net461");
        RunCmd("dotnet", $"publish Mediator.Net/MediatorLib/MediatorLib.csproj -c Release -f net461 -o \"{libnet461OutDir}\"");

        string docOutDir = EnsureDirectory(outDirMode, "Doc");
        CopyFileToDir(Path.Combine(root, "README.pdf"), docOutDir);
        CopyFileToDir(Path.Combine(root, "Doc/HowTo_Modules.pdf"),        docOutDir);
        CopyFileToDir(Path.Combine(root, "Doc/HowTo_AdapterIO.pdf"),      docOutDir);
        CopyFileToDir(Path.Combine(root, "Doc/HowTo_DashboardViews.pdf"), docOutDir);

        if (windowsOrPortable) { 
            string opcSrcDir = Path.Combine(baseDir, "OPC/Adapter/bin/Release");
            string opcOutDir = EnsureDirectory(outDirMode, "Bin", "Mediator", "OPC");
            CopyFileToDir(Path.Combine(opcSrcDir, "OPC_Adapter.exe"),        opcOutDir);
            CopyFileToDir(Path.Combine(opcSrcDir, "OPC_Adapter.exe.config"), opcOutDir);
            CopyFileToDir(Path.Combine(opcSrcDir, "OPC_Client_Net.dll"),     opcOutDir);
            CopyFileToDir(Path.Combine(opcSrcDir, "msvcr120.dll"),           opcOutDir);
            CopyFileToDir(Path.Combine(opcSrcDir, "msvcp120.dll"),           opcOutDir);
            CopyFileToDir(Path.Combine(opcSrcDir, "MediatorLib.dll"),        opcOutDir);
        }

        string webOutDir = EnsureDirectory(outDirMode, "Bin", "WebRoot_Dashboard");
        CopyDir(Path.Combine(root, "Run/DashboardDist"), webOutDir);
        DeleteFile(Path.Combine(webOutDir, "README.md"));

        string configSrcDir = Path.Combine(root, "ExampleConfig");
        string configOutDir = EnsureDirectory(outDirMode, "Config");
        string appConfigFile = selfContained ? "AppConfig_SelfContained.xml" : "AppConfig.xml";
        CopyFileToDir(Path.Combine(configSrcDir, appConfigFile),         configOutDir, "AppConfig.xml");
        CopyFileToDir(Path.Combine(configSrcDir, "Model_Calc.xml"),      configOutDir);
        CopyFileToDir(Path.Combine(configSrcDir, "Model_Dashboard.xml"), configOutDir);
        CopyFileToDir(Path.Combine(configSrcDir, "Model_EventLog.xml"),  configOutDir);
        CopyFileToDir(Path.Combine(configSrcDir, "Model_IO.xml"),        configOutDir);
        CopyFileToDir(Path.Combine(configSrcDir, "Model_Publish.xml"),   configOutDir);
        CopyFileToDir(Path.Combine(configSrcDir, "CSharpLib.cs"),        configOutDir);

        string dataOutDir = EnsureDirectory(outDirMode, "Data");
        WriteToFile(Path.Combine(dataOutDir, "Var_IO.xml"), "<Module name=\"IO\" id=\"IO\" />");
        CreateEmptyFile(Path.Combine(dataOutDir, "DB_IO.db"));
        CreateEmptyFile(Path.Combine(dataOutDir, "DB_EventLog.db"));

        string runCmd = mode switch {
            'p' => "dotnet ./Bin/Mediator/MediatorCore.dll --config=./Config/AppConfig.xml --title=\"ifakFAST Mediator\" --logdir=./Data --logname=\"LogFile\"",
            'w' => ".\\Bin\\Mediator\\MediatorCore.exe --config=./Config/AppConfig.xml --title=\"ifakFAST Mediator\" --logdir=./Data --logname=\"LogFile\"",
            'l' => "./Bin/Mediator/MediatorCore --config=./Config/AppConfig.xml --title=\"ifakFAST Mediator\" --logdir=./Data --logname=\"LogFile\"",
            _ => throw new Exception()
        };

        if (windowsOrPortable) {
            WriteToFile(Path.Combine(outDirMode, "Run.bat"), runCmd);
        }
        else {
            WriteToFile(Path.Combine(outDirMode, "Run.sh"), runCmd);
        }

        if (windowsOrPortable) {
            string args = "--config=./Config/AppConfig.xml --title=\"ifakFAST Mediator\" --logdir=./Data --logname=\"LogFile\" --filestartcomplete=./Data/StartCompleted.info";
            WriteToFile(Path.Combine(outDirMode, "WinServiceArgs.txt"), args);
        }

        string readme = "Dashboard\r\n" +
                        "   URL: http://localhost:8082\r\n" +
                        "   User: ifak\r\n" +
                        "   Pass: fast\r\n";

        WriteToFile(Path.Combine(outDirMode, "ReadMe.txt"), readme);

        string date = DateTime.Now.ToString("yyyy'.'MM'.'dd", CultureInfo.InvariantCulture);

        string strVersion = date + "_" + version;
        WriteToFile(Path.Combine(outDirMode, "Version.txt"), strVersion);

        string archiveFile = mode switch {
            'p' => $"Mediator_v{version}.zip",
            'w' => $"Mediator_v{version}_Win64.zip",
            'l' => $"Mediator_v{version}_Linux64.tar.gz",
            _ => throw new Exception()
        };

        WriteLine($"Creating archive {archiveFile}...");

        if (windowsOrPortable) {

            Zip(outDirMode, Path.Combine(outDir, archiveFile), printFiles: true);
        }
        else {

            UnixFileMode? fileAttributes(string file) {
                if (!linux) {
                    return null;
                }
                if (file.EndsWith("Run.sh") ||
                    file.EndsWith("MediatorCore") ||
                    file.EndsWith("Module_Calc") ||
                    file.EndsWith("Module_Dashboard") ||
                    file.EndsWith("Module_EventLog") ||
                    file.EndsWith("Module_IO") ||
                    file.EndsWith("Module_Publish")) {

                    return UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                           UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                           UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                }
                return null;
            };

            TarGz(outDirMode, Path.Combine(outDir, archiveFile), printFiles: true, fileAttributes);
        }
        
        WriteLine($"{archiveFile} completed.");
    }


}