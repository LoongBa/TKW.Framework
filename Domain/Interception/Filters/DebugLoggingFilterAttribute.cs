using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception.Filters;

/// <inheritdoc />
public class DebugLoggingFilterAttribute<TUserInfo>(string formatString, ILogger? logger = null, LogLevel logLevel = LogLevel.Debug)
    : DomainFilterAttribute<TUserInfo>
where TUserInfo : class, IUserInfo, new()
{
    private readonly ILogger? _Logger = logger;
    private readonly LogLevel _LogLevel = logLevel;

    #region Overrides of DomainActionFilterAttribute

    public override bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext<TUserInfo> context)
    {
        return invocationWhere switch
        {
            DomainInvocationWhereType.Method => true,
            DomainInvocationWhereType.Controller =>
                !context.MethodFlags.Any(f => f is IgnoreEntityHistoryAttribute),
            DomainInvocationWhereType.Global =>
                !(context.ControllerFlags.Any(f => f is IgnoreEntityHistoryAttribute) ||
                  context.MethodFlags.Any(f => f is IgnoreEntityHistoryAttribute)),
            _ => throw new ArgumentOutOfRangeException(nameof(invocationWhere), invocationWhere, null)
        };
    }

    public override Task PreProceedAsync(DomainInvocationWhereType method, DomainContext<TUserInfo> context)
    {
        if (formatString.HasValue())
        {
            //开始时间
        }
        return Task.CompletedTask;
    }

    public override Task PostProceedAsync(DomainInvocationWhereType method, DomainContext<TUserInfo> context)
    {
        if (formatString.HasValue())
        {
            //结束时间
        }
        return Task.CompletedTask;
    }

    #endregion
}