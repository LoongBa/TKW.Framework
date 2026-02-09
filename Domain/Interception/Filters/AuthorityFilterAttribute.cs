using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 权限验证过滤器（认证 + 角色授权）
/// 支持全局注册、类/接口级、方法级控制
/// </summary>
public class AuthorityFilterAttribute<TUserInfo> : DomainFilterAttribute<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    public override bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext<TUserInfo> context)
    {
        // 优先级最高：显式禁用全局权限检查 DisableGlobalAuthorityFilterAttribute
        if (context.MethodFlags.Concat(context.ControllerFlags)
            .Any(f => f is DisableGlobalAuthorityFilterAttribute))
        {
            return false; // 完全跳过本次权限检查
        }

        // 方法级显式允许匿名 → 跳过
        if (context.MethodFlags.Any(f => f is AllowAnonymousFlagAttribute))
        {
            return false;
        }

        // 类/接口级允许匿名 → 也跳过（但方法级优先已在上方处理）
        if (context.ControllerFlags.Any(f => f is AllowAnonymousFlagAttribute))
        {
            return false;
        }

        // 默认：需要检查
        return true;
    }

    public override async Task PreProceedAsync(DomainInvocationWhereType where, DomainContext<TUserInfo> context)
    {
        var user = context.DomainUser;
        var logger = context.Logger; // ← 这里最安全，从上下文取

        // 1. 认证检查
        if (!user.IsAuthenticated)
        {
            logger?.LogWarning(
                "未认证访问尝试 - 方法: {Method} - 用户: {UserName} - 来源: {Where}",
                context.Invocation.Method.Name,
                user.UserInfo.UserName,
                where);

            throw new AuthenticationException(
                $"用户 '{user.UserInfo.UserName}' 未认证，无法访问受保护资源。");
        }

        // 2. 收集所有角色要求标记
        var allRequireFlags = context.MethodFlags
            .Concat(context.ControllerFlags)
            .OfType<RequireRoleFlagAttribute>()
            .ToList();

        if (allRequireFlags.Count == 0)
        {
            return; // 无角色要求，直接通过
        }

        // 3. 检查每一组角色要求
        foreach (var flag in allRequireFlags)
        {
            var passThisGroup = flag.Logic switch
            {
                RoleLogic.Any => flag.Roles.Any(role => user.IsInRole(role)),
                RoleLogic.All => flag.Roles.All(role => user.IsInRole(role)),
                _ => false
            };

            if (!passThisGroup)
            {
                var required = string.Join(", ", flag.Roles);
                var logicDesc = flag.Logic == RoleLogic.Any ? "任一" : "全部";

                logger?.LogWarning(
                    "权限不足 - 方法: {Method} - 用户: {UserName} - 需要{Logic}角色: {Roles} - 来源: {Where}",
                    context.Invocation.Method.Name,
                    user.UserInfo.UserName,
                    logicDesc, required, where);

                throw new UnauthorizedAccessException(
                    $"权限不足。需要 {logicDesc} 满足以下角色之一组：{required}");
            }
        }

        // 全部通过
        await Task.CompletedTask;
    }

    public override async Task PostProceedAsync(DomainInvocationWhereType where, DomainContext<TUserInfo> context)
    {
        // 可选：记录成功访问（生产环境建议采样或关闭）
        // context.Logger?.LogInformation(
        //     "授权通过 - 方法: {Method} - 用户: {UserName} - 来源: {Where}",
        //     context.Invocation.Method.Name,
        //     context.DomainUser.UserInfo?.UserName,
        //     where);

        await Task.CompletedTask;
    }
}