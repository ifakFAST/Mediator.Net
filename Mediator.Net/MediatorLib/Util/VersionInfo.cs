using System.Reflection;

namespace Ifak.Fast.Mediator.Util;

public static class VersionInfo
{
    public static string ifakFAST_Str() {
        var v = ifakFAST();
        if (v == null) return "0";
        return v.ToString();
    }

    public static Version? ifakFAST() {

        Assembly assembly = typeof(Timestamp).Assembly;

        string? versionInfo = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        bool dev = versionInfo != null && versionInfo.Contains("-");
        System.Version? version = assembly.GetName().Version;

        if (version == null) return null;
        
        return new Version() {
            Major = version.Major,
            Minor = version.Minor,
            Build = version.Build,
            Dev = dev
        };
    }
}

public sealed class Version
{
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Build { get; set; }
    public bool Dev { get; set; }

    public override string ToString() {
        string dev = Dev ? "dev" : "";
        return $"{Major}.{Minor}.{Build}{dev}";
    }
}

