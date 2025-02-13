using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace TKW.Framework.Web.Middlewares
{
    public abstract class AbstractMiddleware(RequestDelegate next)
    {
        protected RequestDelegate Next { get; set; } = next;

        /// <summary>
        /// 需要进行的操作：注意 use 中间件的顺序
        /// </summary>
        public abstract Task Invoke(HttpContext context);
    }
}