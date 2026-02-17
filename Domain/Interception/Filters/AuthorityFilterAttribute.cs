using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 权限验证过滤器（核心 AOP 授权组件）
/// 负责认证状态检查及基于 RoleLogic 的多组角色授权校验。
/// </summary>
public class AuthorityFilterAttribute<TUserInfo> : DomainFilterAttribute<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    /// <summary>
    /// 预检阶段：决定是否执行权限检查逻辑。
    /// </summary>
    public override bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext<TUserInfo> context)
    {
        // 1. 收集所有相关标记（合并方法和类/接口级）
        var allFlags = context.MethodFlags.Cast<Attribute>().Concat(context.ControllerFlags.Cast<Attribute>());

        // 2. 优先级最高：显式禁用全局权限检查
        var attributes = allFlags as Attribute[] ?? allFlags.ToArray();
        if (attributes.Any(f => f is DisableGlobalAuthorityFilterAttribute))
            return false;

        // 3. 匿名访问许可：如果任一层级标记了 AllowAnonymous，则跳过
        if (attributes.Any(f => f is AllowAnonymousFlagAttribute))
            return false;

        return true;
    }

    /// <summary>
    /// 执行授权校验
    /// </summary>
    public override async Task PreProceedAsync(DomainInvocationWhereType where, DomainContext<TUserInfo> context)
    {
        var user = context.DomainUser;
        var logger = context.Logger;

        // 1. 认证强制性检查：未登录用户直接拦截
        if (!user.IsAuthenticated)
        {
            logger?.LogWarning("未认证访问拦截 - 方法: {Method} - 来源: {Where}",
                context.Invocation.Method.Name, where);

            throw new AuthenticationException($"用户未登录或会话已过期，无法访问 {context.Invocation.Method.Name}");
        }

        // 2. 收集角色要求标记
        var roleFlags = context.MethodFlags.Concat(context.ControllerFlags)
            .OfType<RequireRoleFlagAttribute>()
            .ToList();

        if (roleFlags.Count == 0) return; // 仅需登录，无角色限制

        // 3. 逐组检查角色逻辑
        // 注意：多组 RequireRole 标记之间是 "AND" 关系，组内按 Logic 判定
        foreach (var flag in roleFlags)
        {
            bool isPassed = flag.Logic switch
            {
                RoleLogic.Any => flag.Roles.Any(r => user.IsInRole(r)), // 满足其一即可
                RoleLogic.All => flag.Roles.All(r => user.IsInRole(r)), // 必须全部满足
                _ => false
            };

            if (!isPassed)
            {
                var roleInfo = string.Join(",", flag.Roles);
                logger?.LogWarning("角色授权失败 - 方法: {Method} - 用户: {UserName} - 缺少角色组({Logic}): [{Roles}]",
                    context.Invocation.Method.Name, user.UserInfo.UserName, flag.Logic, roleInfo);

                throw new UnauthorizedAccessException($"权限不足。您的角色无法满足 '{flag.Logic}' 策略要求的：{roleInfo}");
            }
        }

        await Task.CompletedTask;
    }

    public override async Task PostProceedAsync(DomainInvocationWhereType where, DomainContext<TUserInfo> context)
        => await Task.CompletedTask;
}