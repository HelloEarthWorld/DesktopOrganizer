using System.Runtime.InteropServices;

namespace DesktopOrganizer.Services;

/// <summary>
/// 把文件/文件夹发送到回收站（可还原），而不是永久删除。
/// 用经典的 SHFileOperation，文件和文件夹通用，不需要额外依赖。
/// </summary>
public static class RecycleBinHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;      // 发送到回收站而不是永久删除
    private const ushort FOF_NOCONFIRMATION = 0x0010; // 我们自己在 UI 里弹确认框了，不用系统再弹一次
    private const ushort FOF_SILENT = 0x0004;         // 不显示系统的复制/删除进度条

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);

    /// <summary>返回是否删除成功（用户在系统确认框里取消也算失败）</summary>
    public static bool SendToRecycleBin(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            // pFrom 必须以两个 \0 结尾（Shell API 的老规矩，用来支持多文件用单个\0分隔）
            pFrom = path + '\0' + '\0',
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
        };

        int result = SHFileOperation(ref op);
        return result == 0 && !op.fAnyOperationsAborted;
    }
}
