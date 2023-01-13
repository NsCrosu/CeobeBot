using Konata.Core;
using System.Reflection;

namespace Ceobe.Develop;

public static class BuildStamp
{
    public static string Version { get; } =
        typeof(Bot).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    private static readonly string[] Stamp
        = typeof(Bot).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(x => x.Key is "BuildStamp").Value!.Split(";");
}
