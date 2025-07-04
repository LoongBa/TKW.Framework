using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Web
{
    public static class WebExtensions
    {
        /// <summary>
        /// 根据是否启用了 Session 返回可用的 Session 实例，否则返回 null
        /// </summary>
        public static ISession SafeSession(this HttpContext httpContext)
        {
            var sessionFeature = httpContext.Features.Get<ISessionFeature>();
            return sessionFeature == null ? null : httpContext.Session;
        }

        /// <summary>
        /// 添加领域（将领域实例注入到容器）
        /// </summary>
        public static IServiceCollection AddDomainHelper<TDomainHelper>(this IServiceCollection left, TDomainHelper domainHelper)
        where TDomainHelper : DomainHelperBase
        {
            return left.AddSingleton(domainHelper);
        }
    }
}
