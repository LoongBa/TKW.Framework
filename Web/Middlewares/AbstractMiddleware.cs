using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace TKW.Framework.Web.Middlewares
{
    public abstract class AbstractMiddleware
    {
        protected RequestDelegate Next { get; set; }
        protected AbstractMiddleware(RequestDelegate next)
        {
            Next = next;
        }

        /// <summary>
        /// 需要进行的操作：注意 use 中间件的顺序
        /// </summary>
        public abstract Task Invoke(HttpContext context);
    }
}