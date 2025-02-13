namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 忽略历史记录标识（针对 EntityHistoryActionFilterAttribute）
/// </summary>
/// <see cref="EntityHistoryActionFilterAttribute"/>
public class IgnoreEntityHistoryAttribute : DomainFlagAttribute { }