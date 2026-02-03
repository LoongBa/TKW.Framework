namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 允许匿名标识（针对 AuthorityActionFilterAttribute）
/// </summary>
/// <see cref="AuthorityActionFilterAttribute{TUserInfo}"/>
public class AllowAnonymousAttribute : DomainFlagAttribute { }