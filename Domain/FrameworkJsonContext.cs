/*#nullable enable
using System.Text.Json.Serialization;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

// 统一上下文（后期可继续添加其他类型）
[JsonSerializable(typeof(DomainUser))]
[JsonSerializable(typeof(SessionInfo))]
[JsonSerializable(typeof(SimpleUserInfo))]
public partial class FrameworkJsonContext : JsonSerializerContext
{
}*/