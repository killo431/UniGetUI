using System.Collections.Concurrent;
using UniGetUI.PackageEngine.Interfaces;

namespace UniGetUI.PackageEngine.Classes.Packages.Classes;

public static class PackageCacher
{
    private static readonly ConcurrentDictionary<string, long> _downloadCache = new();

    private static string Key(string managerName, string packageId) =>
        $"{managerName}\\{packageId}";

    public static long GetDownloadCount(IPackage package) =>
        GetDownloadCount(package.Manager.Name, package.Id);

    public static long GetDownloadCount(string managerName, string packageId) =>
        _downloadCache.TryGetValue(Key(managerName, packageId), out long count) ? count : -1;

    public static void SetDownloadCount(string managerName, string packageId, long count) =>
        _downloadCache[Key(managerName, packageId)] = count;
}
