using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TKW.Framework.Web.Attributes;

/// <summary>
/// WebApi 支持的格式限定属性
/// </summary>
public class ContentTypeSupportedFilterAttribute(ContentTypeSupportedType type = ContentTypeSupportedType.JsonOnly)
    : ActionFilterAttribute
{
    public ContentTypeSupportedType Type { get; } = type;

    #region Overrides of ActionFilterAttribute

    /// <inheritdoc />
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        MakeSureContentTypeMatch(context);
        base.OnActionExecuting(context);
    }

    /// <inheritdoc />
    public override Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        MakeSureContentTypeMatch(context);
        return base.OnActionExecutionAsync(context, next);
    }

    private void MakeSureContentTypeMatch(ActionExecutingContext context)
    {
        var accepts = context.HttpContext.Request.Headers["Accept"];
        var contentType = "application/json";
        switch (Type)
        {
            case ContentTypeSupportedType.JsonOnly:
                if (!accepts.Any(a => a.Contains(contentType)))
                    throw new UnsupportedContentTypeException($"仅支持：'{contentType}'，不支持的类型：'{accepts}'");
                break;
            case ContentTypeSupportedType.XmlOnly:
                contentType = "text/xml";
                if (!accepts.Any(a => a.Contains(contentType) || a.Contains("application/xhtml+xml")))
                    throw new UnsupportedContentTypeException($"仅支持：'{contentType}'，不支持的类型：'{accepts}'");
                break;
            case ContentTypeSupportedType.JsonAndXml:
                if (!accepts.Any(a => a.Contains(contentType) || a.Contains("text/xml")))
                    throw new UnsupportedContentTypeException(
                        $"仅支持：'{contentType}'或'text/xml'，不支持的类型：'{accepts}'");
                break;
            default:
                throw new NotSupportedException($"{nameof(ContentTypeSupportedType)}.{Type}");
        }
    }

    #endregion
}