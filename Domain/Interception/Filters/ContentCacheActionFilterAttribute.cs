using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Interception.Filters
{
    public class ContentCacheActionFilterAttribute : DomainActionFilterAttribute
    {
        #region Overrides of DomainActionFilterAttribute

        public override bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext context)
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

        public override void PreProceed(DomainInvocationWhereType method, DomainContext context)
        {
            //判断是否有缓存
        }

        public override void PostProceed(DomainInvocationWhereType method, DomainContext context)
        {
            //更新缓存
        }

        #endregion
    }
    public class DebugLoggingActionFilterAttribute : DomainActionFilterAttribute
    {
        private readonly string _FormatString;
        private readonly ILogger _Logger;
        private readonly LogLevel _LogLevel;

        public DebugLoggingActionFilterAttribute(string formatString, LogLevel logLevel = LogLevel.Debug)
        {
            _FormatString = formatString;
            _LogLevel = logLevel;
        }

        #region Overrides of DomainActionFilterAttribute

        public override bool CanWeGo(DomainInvocationWhereType invocationWhere, DomainContext context)
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

        public override void PreProceed(DomainInvocationWhereType method, DomainContext context)
        {
            if (_FormatString.HasValue())
            {
                //开始时间
            }
        }

        public override void PostProceed(DomainInvocationWhereType method, DomainContext context)
        {
            if (_FormatString.HasValue())
            {
                //结束时间
            }
        }

        #endregion
    }
}