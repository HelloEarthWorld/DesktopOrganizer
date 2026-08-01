using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DesktopOrganizer;

public partial class App : Application
{
    // 保持强引用，防止窗口被 GC 回收
    public static Window? MainAppWindow { get; private set; }

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            System.IO.File.AppendAllText(CrashLog, $"[AppDomain] {e.ExceptionObject}{Environment.NewLine}");
    }

    /// <summary>崩溃现场日志文件，方便排查启动即崩的问题</summary>
    public static readonly string CrashLog =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopOrganizer_crash.log");

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainAppWindow = new MainWindow();

        #region 設定視窗左上角ICON
        // 取得視窗底層句柄
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainAppWindow);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        // 填入你的ico檔名
        appWindow.SetIcon("1cppn-vroyb-001.ico");
        #endregion

        MainAppWindow.Activate();
        // 窗口激活后再设尺寸，激活前 Resize 会被系统按默认尺寸覆盖
        (MainAppWindow as MainWindow)?.ApplyInitialSize();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        System.IO.File.AppendAllText(CrashLog, $"[Xaml] {e.Exception}{Environment.NewLine}");
        // 生产环境建议写日志文件而不是吞掉异常；这里先标记已处理，避免整个进程崩溃
        e.Handled = true;
        System.Diagnostics.Debug.WriteLine($"[UnhandledException] {e.Message}");
    }
}