namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 允许匿名标识（针对 AuthorityActionFilterAttribute）
/// </summary>
/// <see cref="AuthorityActionFilterAttribute"/>
public class AllowAnonymousAttribute : DomainFlagAttribute { }