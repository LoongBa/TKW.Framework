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
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public class AuthorityFilterAttribute<TUserInfo> : DomainFilterAttribute<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    /// <summary>
    /// 预检阶段：决定是否执行权限检查逻辑。
    /// </summary>
    public override bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext<TUserInfo> context)
    {
        // 1. 收集所有相关标记（合并方法和类/接口级）
        var allFlags = context.MethodFlags.Concat(context.ControllerFlags.Cast<Attribute>());

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

        // 1. 认证强制性检查
        if (!user.IsAuthenticated)
        {
            // 调整点：使用 context.Invocation.MethodName
            logger?.LogWarning("未认证访问拦截 - 方法: {Method} - 来源: {Where}",
                context.Invocation.MethodName, where);

            throw new AuthenticationException($"用户未登录或会话已过期，无法访问 {context.Invocation.MethodName}");
        }

        // 2. 收集角色要求标记
        var roleFlags = context.MethodFlags.Concat(context.ControllerFlags)
            .OfType<RequireRoleFlagAttribute>()
            .ToList();

        if (roleFlags.Count == 0) return;

        // 3. 逐组检查角色逻辑
        foreach (var flag in roleFlags)
        {
            bool isPassed = flag.Logic switch
            {
                RoleLogic.Any => flag.Roles.Any(r => user.IsInRole(r)),
                RoleLogic.All => flag.Roles.All(r => user.IsInRole(r)),
                _ => false
            };

            if (!isPassed)
            {
                var roleInfo = string.Join(",", flag.Roles);
                // 调整点：使用 context.Invocation.MethodName
                logger?.LogWarning("角色授权失败 - 方法: {Method} - 用户: {UserName} - 缺少角色组({Logic}): [{Roles}]",
                    context.Invocation.MethodName, user.UserInfo.UserName, flag.Logic, roleInfo);

                throw new UnauthorizedAccessException($"权限不足。您的角色无法满足 '{flag.Logic}' 策略要求的：{roleInfo}");
            }
        }
    }

    public override async Task PostProceedAsync(DomainInvocationWhereType where, DomainContext<TUserInfo> context)
        => await Task.CompletedTask;
}