using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using TKW.Framework.Web.Exceptions;
using TKW.Framework.Web.Users;

namespace TKW.Framework.Web.Filters
{
    /// <summary>
    /// 支持的容器类型（或）
    /// </summary>
    /// <remarks>支持所有容器类型请用：WebContainerType.All</remarks>
    public class ContainerSupportActionFilterAttribute : ActionFilterAttribute
    {
        public WebContainerType Type { get; }

        /// <summary>
        /// 初始化 <see cref="T:System.Web.Mvc.ActionFilterAttribute"/> 类的新实例。
        /// </summary>
        public ContainerSupportActionFilterAttribute(WebContainerType type = WebContainerType.All)
        {
            Type = type;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            //忽略子 Action
            //if (filterContext.IsChildAction) return;
            //忽略非 IWebUser
            if (!(filterContext.HttpContext.User is IWebUser user)) return;

            var type = typeof(ContainerSupportActionFilterAttribute);
            if (!filterContext.ActionDescriptor.GetType().IsDefined(type, false)) return;

            var attribute = (ContainerSupportActionFilterAttribute)filterContext.ActionDescriptor
                                .GetType()
                                .GetCustomAttributes(type, false)
                                .First();

            var supportedType = attribute.Type;
            var currentType = user.Container.Type;

            if (currentType == WebContainerType.UnSet || currentType == WebContainerType.Unknown)
                throw new UnRecognizedContainerException();

            if ((supportedType & currentType) != currentType)
                throw new ContainerNotSupportedException(supportedType, currentType);
        }
    }
}