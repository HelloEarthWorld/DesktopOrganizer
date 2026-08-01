using Microsoft.UI.Xaml;
using Microsoft.UI.Composition.SystemBackdrops;
using DesktopOrganizer.Services;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.UI;

namespace DesktopOrganizer;

/// <summary>
/// 只负责窗口外壳：亚克力背景、自定义标题栏拖拽区、初始窗口尺寸（4:3 横屏）。
/// 不再有托盘/后台常驻——关闭窗口就是正常退出进程（即开即关）。
/// 界面内容和数据逻辑都在 AppShell（见 AppShell.xaml/.cs）里，规避了
/// WinUI3 里 Window 不是 FrameworkElement、直接用 x:Bind 会编译报错的问题。
/// </summary>
public sealed partial class MainWindow : Window
{
    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _backdropConfig;
    // 缓存根元素引用：窗口关闭后 Activated 仍可能触发一次，
    // 此时再访问 Window.Content 会抛 COMException(0x800710DD "操作标识符不正确")
    private readonly FrameworkElement? _themeRoot;

    public MainWindow()
    {
        this.InitializeComponent();

        LegacyCleanup.RemoveOldAutoStartEntry();

        _themeRoot = Content as FrameworkElement;
        ApplyBackdrop();
        // 不要 ExtendsContentIntoTitleBar=true：那会把内容顶到系统标题栏位置，
        // 自定义标题栏（Apps窗口 文字/按钮）会被系统按钮吃掉，整个上方栏消失
        SetTitleBar(Shell.DragRegion);

        this.Closed += (_, _) =>
        {
            if (_acrylicController != null)
            {
                _acrylicController.Dispose();
                _acrylicController = null;
                _backdropConfig = null;
            }
        };
    }

    /// <summary>
    /// 亚克力模糊背景（透明、类似高斯模糊）。DesktopAcrylicBackdrop 是封装好的成品，
    /// 连 TintOpacity 都不让调，改用 DesktopAcrylicController 手动控制，
    /// TintOpacity 调低更透、LuminosityOpacity 调高模糊更明显。
    /// 不支持时退回 Mica。窗口级亚克力只糊一层，卡片不要再叠应用内亚克力，
    /// 否则双重模糊拖慢渲染、增加显存占用。
    /// </summary>
    private void ApplyBackdrop()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            _acrylicController = new DesktopAcrylicController
            {
                TintOpacity = 0.3f,
                LuminosityOpacity = 0.8f,
            };
            _backdropConfig = new SystemBackdropConfiguration();
            _acrylicController.AddSystemBackdropTarget(WinRT.CastExtensions.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>(this));
            _acrylicController.SetSystemBackdropConfiguration(_backdropConfig);
            this.Activated += OnWindowActivated;
            UpdateBackdropTheme();
        }
        else if (MicaController.IsSupported())
        {
            this.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_backdropConfig == null || _acrylicController == null || _themeRoot is null) return;
        _backdropConfig.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;
        UpdateBackdropTheme();
    }

    private void UpdateBackdropTheme()
    {
        if (_backdropConfig == null || _acrylicController == null || _themeRoot is null) return;
        var dark = _themeRoot.ActualTheme == ElementTheme.Dark;
        _backdropConfig.Theme = dark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;
        // 浅色背景下对比弱，tint 再调低点才看得出模糊；深色保持原值
        _acrylicController.TintOpacity = dark ? 0.3f : 0.2f;
        // 亚克力默认 tint 是近白色，深色模式下不清掉会整窗发白（"深色模式显示白色"的来源之一）
        _acrylicController.TintColor = dark
            ? Color.FromArgb(255, 44, 44, 44)
            : Color.FromArgb(255, 252, 252, 252);
    }

    /// <summary>启动时按屏幕可用高度定窗口尺寸，保持 4:3 横屏比例（激活后由 OnLaunched 调用，激活前会被默认尺寸覆盖）</summary>
    public void ApplyInitialSize()
    {
        try
        {
            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            var height = (int)(workArea.Height * 0.75);
            var width = (int)(height * 4.0 / 3.0);
            if (width > workArea.Width) width = (int)(workArea.Width * 0.85);
            AppWindow.Resize(new SizeInt32(width, height));
        }
        catch
        {
            // 拿不到屏幕信息就用一个保守的固定尺寸，不影响使用
            AppWindow.Resize(new SizeInt32(1200, 900));
        }
    }
}
