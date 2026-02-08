namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 忽略历史记录标识（针对 EntityHistoryActionFilterAttribute）
/// </summary>
/// <see cref="EntityHistoryFilterAttribute{TUserInfo}"/>
public class IgnoreEntityHistoryAttribute : DomainFlagAttribute { }