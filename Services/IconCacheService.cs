namespace DesktopOrganizer.Services;

/// <summary>
/// 把提取出来的图标缓存到本地文件夹（%LOCALAPPDATA%\DesktopOrganizer\IconCache），
/// 下次同一个文件/程序不用再走一次 Shell 图标提取，加载明显更快。
/// </summary>
public static class IconCacheService
{
    /// <summary>图标来源/渲染逻辑变化时递增这个版本号，旧缓存自动失效重建，避免显示旧图标</summary>
    private const string CacheVersion = "6";

    private const string VersionFileName = "cache.version";

    public static readonly string CacheDir = Path.Combine(AppContext.BaseDirectory, "icodata");

    static IconCacheService()
    {
        Directory.CreateDirectory(CacheDir);
        var versionFile = Path.Combine(CacheDir, VersionFileName);
        if (File.Exists(versionFile) && File.ReadAllText(versionFile).Trim() == CacheVersion) return;
        ClearCache();
        try { File.WriteAllText(versionFile, CacheVersion); } catch { /* 写不了版本文件就每次清理一次，不影响功能 */ }
    }

    /// <summary>清空所有缓存的图标文件，下次会重新从系统提取</summary>
    public static void ClearCache()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(CacheDir))
                File.Delete(file);
        }
        catch { /* 个别文件占用/权限问题不影响整体 */ }
    }

    /// <summary>用路径的哈希做缓存文件名，避免特殊字符/路径过长问题</summary>
    public static string GetCachePath(string sourcePath)
    {
        var hash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(sourcePath.ToLowerInvariant()));
        return Path.Combine(CacheDir, Convert.ToHexString(hash) + ".png");
    }
}
