using System;
using System.Linq;

namespace TKW.Framework.Domain.Interception.Filters
{
    /// <summary>
    /// 记录历史（忽略 IgnoreEntityHistoryAttribute）
    /// </summary>
    /// <see cref="IgnoreEntityHistoryAttribute"/>
    public class EntityHistoryActionFilterAttribute : DomainActionFilterAttribute
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

        public override void PreProceed(DomainInvocationWhereType method, DomainContext context) { }
        public override void PostProceed(DomainInvocationWhereType method, DomainContext context) { }

        #endregion
    }
}