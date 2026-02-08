using System;

namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 允许匿名访问的标记（忽略 AuthorityFilterAttribute 的认证检查）
/// </summary>
/// <see cref="AuthorityFilterAttribute{TUserInfo}"/>
public class AllowAnonymousFlagAttribute : DomainFlagAttribute
{
    // 可选：添加唯一 TypeId（用于 Filter 匹配）
    public override object TypeId => new Guid("00000000-0000-0000-0000-000000000001");  // 固定 Guid
}