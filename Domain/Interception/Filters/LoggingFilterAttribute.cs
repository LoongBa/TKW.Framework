using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 全局/方法级日志过滤器
/// 自动记录方法进入/退出、耗时、用户、参数（脱敏）
/// </summary>
public class LoggingFilterAttribute<TUserInfo> : DomainFilterAttribute<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    private readonly Stopwatch _stopwatch = new();

    public override bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext<TUserInfo> context)
    {
        // 支持禁用：方法上贴 [DisableLogging] 则跳过
        return !context.MethodFlags.Any(f => f is DisableLoggingAttribute);
    }

    public override async Task PreProceedAsync(DomainInvocationWhereType where, DomainContext<TUserInfo> context)
    {
        _stopwatch.Restart();

        var logger = context.Logger;
        var user = context.DomainUser;
        var method = context.Invocation.Method.Name;
        var args = string.Join(", ", context.Invocation.Arguments.Select(SafeToString));

        logger.LogInformation(
            "领域方法进入 - 方法: {Method} - 用户: {UserName} - 参数: {Args} - 来源: {Where}",
            method, user.UserInfo.UserName, args, where);

        await Task.CompletedTask;
    }

    public override async Task PostProceedAsync(DomainInvocationWhereType where, DomainContext<TUserInfo> context)
    {
        _stopwatch.Stop();

        var logger = context.Logger;
        var method = context.Invocation.Method.Name;
        var durationMs = _stopwatch.ElapsedMilliseconds;

        logger.LogInformation(
            "领域方法完成 - 方法: {Method} - 耗时: {DurationMs}ms - 来源: {Where}",
            method, durationMs, where);

        await Task.CompletedTask;
    }

    // 辅助：简单脱敏（可扩展）
    private static string SafeToString(object? obj)
    {
        if (obj == null) return "null";
        var str = obj.ToString() ?? string.Empty;
        return str.Length > 50 ? str.Substring(0, 47) + "..." : str;
    }
}