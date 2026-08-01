using System.Text.Json.Serialization;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// System.Text.Json 的源生成上下文。
/// 项目开了 PublishTrimmed（裁剪优化）之后，反射式 JSON 序列化会被自动禁用
/// （报错 "Reflection-based serialization has been disabled"），
/// 必须用这种源生成方式代替，跟裁剪/AOT 都兼容。
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<ItemGroup>))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
