namespace DesktopOrganizer.Services;

/// <summary>当前的搜索关键字（Spotlight风格过滤，只在已加载的图标范围内查找）</summary>
public static class SearchState
{
    public static string Query { get; set; } = string.Empty;
}
