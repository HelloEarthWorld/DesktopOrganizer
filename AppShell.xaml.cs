using System.Collections.ObjectModel;
using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DesktopOrganizer;

/// <summary>
/// 承载全部界面内容和交互逻辑的 UserControl。
/// 之所以不直接写在 MainWindow(Window) 里，是因为 WinUI3 的 Window 类不是
/// FrameworkElement，直接在 Window 的 XAML 里大量用 x:Bind 会导致编译器生成的
/// 绑定基础设施出错（CS1503: 无法从 Window 转换为 FrameworkElement）。
/// 放进 UserControl 就有了正经的 FrameworkElement 根，问题就没有了。
/// </summary>
public sealed partial class AppShell : UserControl
{
    private readonly GroupStorageService _storage = new();
    private readonly ObservableCollection<ItemGroup> _groups = new();
    private readonly ObservableCollection<DesktopItem> _searchResults = new();

    // 拖拽时临时记录：拖的是哪个条目、来自哪个分组
    private DesktopItem? _draggedItem;
    private ItemGroup? _draggedFromGroup;

    // 按下时的高亮背景：优先用主题画笔，拿不到就退回半透明灰（深浅主题都能看见）
    private static readonly Brush PressBrush = CreatePressBrush();

    private static Brush CreatePressBrush()
    {
        try
        {
            if (Application.Current.Resources.TryGetValue("ControlFillColorSecondaryBrush", out var value)
                && value is Brush brush)
                return brush;
        }
        catch { /* 拿不到画笔就退回兜底颜色，不影响功能 */ }
        return new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.35 };
    }

    /// <summary>给 MainWindow 调用 SetTitleBar 用的拖拽区域引用</summary>
    public Border DragRegion => DragRegionElement;

    public AppShell()
    {
        this.InitializeComponent();

        GroupsItemsControl.ItemsSource = _groups;
        SearchResultsList.ItemsSource = _searchResults;
        RoundedIconsToggle.IsOn = AppSettingsService.RoundedIconsEnabled;

        // 浅色模式卡片更透：主题变化时更新背景画笔并重建一次绑定
        VisualSettings.ApplyTheme(ActualTheme);
        ActualThemeChanged += (_, _) =>
        {
            VisualSettings.ApplyTheme(ActualTheme);
            SearchResultsPanel.Background = VisualSettings.SearchPanelBrush;
            RefreshIconsVisual();
        };

        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var savedGroups = _storage.Load();
            var scanResult = await AppAggregatorService.ScanAsync();
            var systemShortcuts = await SystemShortcuts.BuildAsync();

            var allItems = scanResult.Apps.Concat(scanResult.Files).ToList();
            // 用 GroupBy 而不是直接 ToDictionary：万一某个来源出现重复路径/ID，
            // 只是悄悄保留第一个，不会让整个加载流程崩掉
            var itemsByPath = allItems
                .GroupBy(i => i.FullPath)
                .ToDictionary(g => g.Key, g => g.First());
            var assignedPaths = new HashSet<string>();

            _groups.Clear();

            // 系统固定分组：此电脑 / 控制面板 / 回收站 / 设置，永远在最前面，不允许删除
            var systemGroup = savedGroups.FirstOrDefault(g => g.IsSystem)
                ?? new ItemGroup { Name = "系统", IsSystem = true };
            systemGroup.Items.Clear();
            foreach (var sc in systemShortcuts) systemGroup.Items.Add(sc);
            _groups.Add(systemGroup);

            foreach (var g in savedGroups.Where(g => !g.IsSystem))
            {
                g.Items.Clear();
                foreach (var path in g.ItemPaths)
                {
                    if (itemsByPath.TryGetValue(path, out var item))
                    {
                        g.Items.Add(item);
                        assignedPaths.Add(path);
                    }
                    // 如果文件已被用户从桌面删除/移走，或程序被卸载，静默跳过，不报错打扰用户
                }
                _groups.Add(g);
            }

            // 新出现、还没分组的条目：应用归到"应用"分组，其它文件归到"文件"分组
            var unassignedApps = scanResult.Apps.Where(i => !assignedPaths.Contains(i.FullPath)).ToList();
            var unassignedFiles = scanResult.Files.Where(i => !assignedPaths.Contains(i.FullPath)).ToList();

            if (unassignedApps.Count > 0)
            {
                var appsGroup = _groups.FirstOrDefault(g => g.Name == "应用" && !g.IsSystem);
                if (appsGroup is null)
                {
                    appsGroup = new ItemGroup { Name = "应用" };
                    _groups.Add(appsGroup);
                }
                foreach (var item in unassignedApps) appsGroup.Items.Add(item);
            }

            if (unassignedFiles.Count > 0)
            {
                var filesGroup = _groups.FirstOrDefault(g => g.Name == "文件" && !g.IsSystem);
                if (filesGroup is null)
                {
                    filesGroup = new ItemGroup { Name = "文件" };
                    _groups.Add(filesGroup);
                }
                foreach (var item in unassignedFiles) filesGroup.Items.Add(item);
            }

            SaveGroups();
        }
        catch (Exception ex)
        {
            // 加载失败时把具体错误信息直接显示在界面上（而不只是写到调试输出窗口），
            // 这样不用挂调试器也能立刻看到问题出在哪
            System.Diagnostics.Debug.WriteLine($"[LoadDataAsync] 加载失败: {ex}");
            _groups.Clear();
            _groups.Add(new ItemGroup { Name = $"加载出错：{ex.GetType().Name} - {ex.Message}（点右上角刷新重试）" });
        }
    }

    public void SaveGroups() => _storage.Save(_groups);

    // ================= 标题栏按钮 =================

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadDataAsync();

    private void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        _groups.Add(new ItemGroup { Name = "新分组" });
        SaveGroups();
    }

    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ItemGroup group })
        {
            if (group.IsSystem) return; // 系统分组（此电脑/控制面板/设置）不允许删除

            // 分组里的文件不会被删除，只是解散分组，条目并回"未分组"
            var target = _groups.FirstOrDefault(g => g.Name == "未分组" && g != group && !g.IsSystem);
            if (target is null)
            {
                target = new ItemGroup { Name = "未分组" };
                _groups.Add(target);
            }
            foreach (var item in group.Items)
                target.Items.Add(item);

            _groups.Remove(group);
            SaveGroups();
        }
    }

    private void RoundedIconsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        AppSettingsService.SetRoundedIconsEnabled(RoundedIconsToggle.IsOn);
        RefreshIconsVisual();
    }

    private async void ClearCacheButton_Click(object sender, RoutedEventArgs e)
    {
        IconCacheService.ClearCache();
        await LoadDataAsync();
    }

    /// <summary>强制刷新图标网格里跟"全局设置/搜索词"相关的转换器绑定（这两个不是数据变化，得手动触发一次）</summary>
    private void RefreshIconsVisual()
    {
        var current = GroupsItemsControl.ItemsSource;
        GroupsItemsControl.ItemsSource = null;
        GroupsItemsControl.ItemsSource = current;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchState.Query = SearchBox.Text;
        var query = SearchState.Query.Trim();

        // Spotlight 风格：有搜索词时把全部命中条目收集到单独一列紧凑列表里，
        // 分组卡片整个收起来，不用一个个分组翻；没搜索词就恢复原视图
        _searchResults.Clear();
        if (query.Length > 0)
        {
            foreach (var g in _groups)
            {
                foreach (var item in g.Items)
                {
                    if (item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                        && !_searchResults.Contains(item))
                        _searchResults.Add(item);
                }
            }
        }

        var searching = query.Length > 0;
        SearchResultsPanel.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        GroupsItemsControl.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
        SearchResultsHeader.Text = searching ? $"搜索结果（{_searchResults.Count} 项）" : string.Empty;

        if (!searching) RefreshIconsVisual();
    }

    private void SearchResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DesktopItem item) LaunchItem(item);
    }

    // ================= 图标点击：Tapped 启动 + 悬停/按下/闪动反馈 =================
    // 不用 GridView 的 ItemClick（开了拖拽后它经常收不到），Tapped 是手势事件，
    // 无论容器怎么处理指针都稳定触发；右键是 RightTapped，不会误触发启动

    private void ItemIcon_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Panel fe || fe.DataContext is not DesktopItem item) return;

        FlashItem(fe);
        LaunchItem(item);
    }

    private void ItemIcon_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Panel fe && fe.DataContext is not null) fe.Opacity = 0.85;
    }

    private void ItemIcon_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Panel fe) return;
        fe.Opacity = 0.55;
        fe.Background = PressBrush;
    }

    private void ItemIcon_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Panel fe) RestoreItemVisual(fe);
    }

    private static void RestoreItemVisual(Panel fe)
    {
        fe.Opacity = 1.0;
        fe.Background = null;
    }

    /// <summary>点击后快速闪一下，保证任何输入方式下都能看到"点到了"的反馈</summary>
    private static async void FlashItem(Panel fe)
    {
        await Task.Delay(90);
        RestoreItemVisual(fe);
    }

    private void ContextOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: DesktopItem item })
            LaunchItem(item);
    }

    private void ContextUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: DesktopItem item }) return;
        if (string.IsNullOrWhiteSpace(item.UninstallCommand)) return;

        try
        {
            // UninstallString 常见形如 "C:\Path\unins000.exe" 或 MsiExec.exe /X{GUID}，
            // 统一交给 cmd /c 处理，不用自己解析可执行文件名和参数
            var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c {item.UninstallCommand}")
            {
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Uninstall] 启动卸载程序失败: {ex.Message}");
        }
    }

    private async void ContextViewShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: DesktopItem item }) return;

        var dialog = new ContentDialog
        {
            Title = "快捷方式位置",
            Content = string.IsNullOrEmpty(item.ShortcutTargetPath) ? "无法解析目标路径" : item.ShortcutTargetPath,
            CloseButtonText = "关闭",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void ContextDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: DesktopItem item }) return;

        var owningGroup = _groups.FirstOrDefault(g => g.Items.Contains(item));

        // 已安装程序/Steam游戏/系统快捷方式：不是真实桌面文件，删除只从视图里移除，
        // 不会去卸载真实程序（下次刷新如果还是已安装状态，会重新出现在"应用"分组）
        if (item.IsSystemShortcut || item.IsInstalledApp)
        {
            owningGroup?.Items.Remove(item);
            SaveGroups();
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "删除到回收站？",
            Content = $"将「{item.DisplayName}」移动到回收站，可以从回收站还原。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        if (RecycleBinHelper.SendToRecycleBin(item.FullPath))
        {
            owningGroup?.Items.Remove(item);
            SaveGroups();
        }
    }

    private void LaunchItem(DesktopItem item)
    {
        var target = string.IsNullOrEmpty(item.LaunchTarget) ? item.FullPath : item.LaunchTarget;
        if (string.IsNullOrEmpty(target)) return;

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            // 文件被移动/程序被卸载等情况下启动失败，不弹窗打扰，写日志方便排查
            System.Diagnostics.Debug.WriteLine($"[LaunchItem] 启动失败 {target}: {ex.Message}");
        }
    }

    // ================= 拖拽：把图标从一个分组移动到另一个分组 =================

    private void ItemsGridView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (sender is GridView { Tag: ItemGroup group } && e.Items.Count > 0
            && e.Items[0] is DesktopItem item)
        {
            _draggedItem = item;
            _draggedFromGroup = group;
        }
    }

    private void ItemsGridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        _draggedItem = null;
        _draggedFromGroup = null;
    }

    private void ItemsGridView_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private void ItemsGridView_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (sender is not GridView { Tag: ItemGroup targetGroup }) return;
        if (_draggedItem is null || _draggedFromGroup is null) return;
        if (ReferenceEquals(targetGroup, _draggedFromGroup)) return; // 同分组内排序交给 CanReorderItems 处理

        _draggedFromGroup.Items.Remove(_draggedItem);
        if (!targetGroup.Items.Contains(_draggedItem))
            targetGroup.Items.Add(_draggedItem);

        SaveGroups();
        _draggedItem = null;
        _draggedFromGroup = null;
    }
}
