using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace DesktopOrganizer.Services;

/// <summary>
/// 提取文件/文件夹/DLL 资源里的系统图标，比逐个打开文件读取图标快得多，
/// 也是资源管理器本身的做法。所有结果都会缓存到本地文件夹（见 IconCacheService）。
/// </summary>
public static class IconExtractor
{
    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0; // 32x32
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10; // 允许对不存在的路径（如 ::{CLSID} 特殊文件夹）按属性提取图标
    private const uint SHGFI_PIDL = 0x8; // 按已解析的 Shell 项 PIDL 提取图标
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    /// <summary>把 "::{CLSID}" 这类 Shell 命名空间路径解析成 PIDL（跟资源管理器地址栏同一个解析器）</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string pszName, IntPtr pbc,
        out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    /// <summary>按 PIDL 取 Shell 项图标（SHGFI_PIDL），跟资源管理器显示完全一致</summary>
    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfo")]
    private static extern IntPtr SHGetFileInfoPidl(IntPtr pidl, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    // LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE：把 .mun 这类纯资源文件映射进来，不执行代码
    private const uint LoadLibraryAsDataFile = 0x00000002;
    private const uint LoadLibraryAsImageResource = 0x00000020;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(string lpszFile, int nIconIndex,
        IntPtr[] phiconLarge, IntPtr[]? phiconSmall, int nIcons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// 提取文件/文件夹图标并转换为 WinUI 可用的 BitmapImage。优先读本地缓存，
    /// 对不存在或不可访问的路径返回 null，调用方应回退到占位图标。
    /// </summary>
    public static Task<BitmapImage?> GetIconAsync(string path, bool isFolder)
        => GetIconCoreAsync(cacheKey: path, extract: () =>
        {
            var info = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES;
            uint attrs = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
            var result = SHGetFileInfo(path, attrs, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            return (result == IntPtr.Zero) ? IntPtr.Zero : info.hIcon;
        });

    /// <summary>
    /// 按索引从 DLL/EXE 资源里提取图标（比如 %SystemRoot%\System32\SHELL32.dll 的第 22 个图标）。
    /// 索引不存在或提取失败时返回 null，调用方应回退到别的图标来源。
    /// </summary>
    public static Task<BitmapImage?> GetIconFromDllAsync(string dllPath, int index)
        => GetIconCoreAsync(cacheKey: $"{dllPath}!{index}", extract: () =>
        {
            var large = new IntPtr[1];
            int count = ExtractIconEx(dllPath, index, large, null, 1);
            return (count <= 0) ? IntPtr.Zero : large[0];
        });

    /// <summary>
    /// 提取 Shell 特殊项（"::{CLSID}"，如 此电脑/控制面板/回收站）的真实图标。
    /// 先 SHParseDisplayName 走 Shell 命名空间解析，再按 PIDL 取图标——跟资源管理器
    /// 显示完全一致，不受系统 DLL 图标索引在不同版本里挪动的影响。
    /// </summary>
    public static Task<BitmapImage?> GetShellItemIconAsync(string clsidPath)
        => GetIconCoreAsync(cacheKey: $"pidl:{clsidPath}", extract: () =>
        {
            if (SHParseDisplayName(clsidPath, IntPtr.Zero, out var pidl, 0, out _) != 0 || pidl == IntPtr.Zero)
                return IntPtr.Zero;

            try
            {
                var info = new SHFILEINFO();
                var result = SHGetFileInfoPidl(pidl, 0, ref info,
                    (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_PIDL | SHGFI_ICON | SHGFI_LARGEICON);
                return (result == IntPtr.Zero) ? IntPtr.Zero : info.hIcon;
            }
            finally
            {
                CoTaskMemFree(pidl);
            }
        });

    /// <summary>
    /// 直接从资源文件（含 .mun，如 C:\Windows\SystemResources\shell32.dll.mun）按资源 ID 取图标。
    /// Win10 1809+ 系统图标实际都搬到了 .mun 里，System32 的同名 dll 只是桩，ExtractIconEx 经常取不到。
    /// </summary>
    public static Task<BitmapImage?> GetIconFromResourceAsync(string modulePath, int resourceId)
        => GetIconCoreAsync(cacheKey: $"res:{modulePath}!{resourceId}",
            extract: () =>
            {
                var hModule = LoadLibraryEx(modulePath, IntPtr.Zero, LoadLibraryAsDataFile | LoadLibraryAsImageResource);
                if (hModule == IntPtr.Zero) return IntPtr.Zero;
                try
                {
                    return LoadIcon(hModule, (IntPtr)resourceId);
                }
                finally
                {
                    FreeLibrary(hModule);
                }
            },
            // LoadIcon 返回的是系统共享图标，不能 DestroyIcon（跟 ExtractIconEx/GetFileInfo 的私有句柄不同）
            destroyIcon: false);

    private static async Task<BitmapImage?> GetIconCoreAsync(string cacheKey, Func<IntPtr> extract, bool destroyIcon = true)
    {
        var cachePath = IconCacheService.GetCachePath(cacheKey);
        if (File.Exists(cachePath))
        {
            var cached = await LoadBitmapFromFileAsync(cachePath);
            if (cached != null) return cached;
            try { File.Delete(cachePath); } catch { /* 忽略，走下面重新提取 */ }
        }

        var hIcon = extract();
        if (hIcon == IntPtr.Zero) return null;

        try
        {
            using var icon = System.Drawing.Icon.FromHandle(hIcon);
            using var bitmap = icon.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            // 存进本地缓存文件夹（./icodata），下次直接读文件，不用再提取
            try { await File.WriteAllBytesAsync(cachePath, ms.ToArray()); } catch { /* 缓存失败不影响本次显示 */ }
            ms.Position = 0;

            var bitmapImage = new BitmapImage();
            using var randomStream = new InMemoryRandomAccessStream();
            // 用真正的异步 WinRT 流复制，避免同步阻塞导致 UI 线程死锁
            await RandomAccessStream.CopyAsync(ms.AsInputStream(), randomStream.GetOutputStreamAt(0));
            randomStream.Seek(0);
            await bitmapImage.SetSourceAsync(randomStream);
            return bitmapImage;
        }
        finally
        {
            if (destroyIcon) DestroyIcon(hIcon);
        }
    }

    private static async Task<BitmapImage?> LoadBitmapFromFileAsync(string filePath)
    {
        try
        {
            using var fileStream = File.OpenRead(filePath);
            using var randomStream = new InMemoryRandomAccessStream();
            await RandomAccessStream.CopyAsync(fileStream.AsInputStream(), randomStream.GetOutputStreamAt(0));
            randomStream.Seek(0);

            var bitmapImage = new BitmapImage();
            await bitmapImage.SetSourceAsync(randomStream);
            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }
}
