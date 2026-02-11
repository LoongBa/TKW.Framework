using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception.Filters;

/// <summary>
/// 全局/方法级日志过滤器
/// 自动记录方法进入/退出、耗时、用户、参数（脱敏）
/// </summary>
public class LoggingFilterAttribute<TUserInfo>(EnumDomainLogLevel level) : DomainFilterAttribute<TUserInfo>
    where TUserInfo : class, IUserInfo, new()
{
    private readonly Stopwatch _Stopwatch = new();

    public override bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext<TUserInfo> context)
    {
        // 如果级别为 None，则直接跳过整个过滤器
        if (level == EnumDomainLogLevel.None)
            return false;

        return !context.MethodFlags.Any(f => f is DisableLoggingAttribute);
    }

    public override async Task PreProceedAsync(DomainInvocationWhereType where, DomainContext<TUserInfo> context)
    {
        if (level == EnumDomainLogLevel.None)
            return;

        _Stopwatch.Restart();

        var logger = context.Logger;

        var user = context.DomainUser;
        var method = context.Invocation.Method.Name;

        // 根据级别决定是否记录参数
        var argsInfo = level >= EnumDomainLogLevel.Verbose
            ? string.Join(", ", context.Invocation.Arguments.Select(SafeToString))
            : "参数已省略（非 Verbose 模式）";

        logger?.LogInformation(
            "领域方法进入 - 方法: {Method} - 用户: {UserName} - 参数: {Args} - 来源: {Where}",
            method, user.UserInfo?.UserName ?? "Anonymous", argsInfo, where);

        await Task.CompletedTask;
    }

    public override async Task PostProceedAsync(DomainInvocationWhereType where, DomainContext<TUserInfo> context)
    {
        if (level == EnumDomainLogLevel.None)
            return;

        _Stopwatch.Stop();

        var logger = context.Logger;

        var method = context.Invocation.Method.Name;
        var durationMs = _Stopwatch.ElapsedMilliseconds;

        // 可根据级别决定日志级别（Warning for slow calls）
        var logLevel = durationMs > 500 ? LogLevel.Warning : LogLevel.Information;

        logger?.Log(logLevel,
            "领域方法完成 - 方法: {Method} - 耗时: {DurationMs}ms - 来源: {Where}",
            method, durationMs, where);

        await Task.CompletedTask;
    }

    private static string SafeToString(object? obj)
    {
        if (obj == null) return "null";
        var str = obj.ToString() ?? string.Empty;
        return str.Length > 50 ? string.Concat(str.AsSpan(0, 47), "...") : str;
    }
}