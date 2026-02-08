using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TKW.Framework.Common.Exceptions;

namespace TKW.Framework.Web.Filters;

/// <summary>
/// 全局异常 GlobalExceptionFilterAttribute
/// </summary>
public class GlobalExceptionFilterAttribute : ExceptionFilterAttribute
{
    /// <summary>
    /// 错误处理的Controller
    /// </summary>
    public string CustomErrorViewName { get; }

    /// <summary>
    /// 错误处理的Action
    /// </summary>
    public string DefaultLoginUrl { get; }

    public ExceptionHandlerDelegate ExceptionHandler { get; }

    /// <summary>
    /// 全局异常 IExceptionFilter
    /// </summary>
    /// <param name="exceptionHandler"></param>
    /// <param name="customCustomErrorViewName"></param>
    /// <param name="defaultLoginUrl"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public GlobalExceptionFilterAttribute(ExceptionHandlerDelegate exceptionHandler, string customCustomErrorViewName = "Error", string defaultLoginUrl = "/login.html")
    {
        if (string.IsNullOrWhiteSpace(defaultLoginUrl)) throw new ArgumentNullException(nameof(defaultLoginUrl));
        if (string.IsNullOrWhiteSpace(customCustomErrorViewName)) throw new ArgumentNullException(nameof(customCustomErrorViewName));

        ExceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
        DefaultLoginUrl = defaultLoginUrl;
        CustomErrorViewName = customCustomErrorViewName;
    }
    private readonly Dictionary<string, bool> _CustomViewChecked = [];

    /// <exception cref="Exception">A delegate callback throws an exception.</exception>
    /// <exception cref="NotImplementedException"></exception>
    public override void OnException(ExceptionContext context)
    {
        //交给 WebTools 处理常见异常
        var resultModel = new ExceptionHandledResultModel(new ExceptionHandled(context.Exception), CustomErrorViewName, DefaultLoginUrl);
        try
        {
            resultModel = WebTools.HandleException(context, ExceptionHandler, resultModel);
        }
        catch (Exception e) //项目异常处理代码中的异常
        {
            resultModel = new(new ExceptionHandled(e), CustomErrorViewName, DefaultLoginUrl);
        }

        //后续处理
        var accepts = context.HttpContext.Request.Headers["accept"];
        IActionResult contextResult;
        if (!string.IsNullOrEmpty(accepts)
            && accepts.Any(
                a => a.Equals("application/json", StringComparison.OrdinalIgnoreCase)))
        {
            //客户端请求的是Json格式（对应WebApi）
            contextResult = new JsonResult(resultModel) { StatusCode = 500 };
        }
        else
        {
            if (resultModel.IsRedirect2Url) //转向到指定 Url：HTTP 302
            {
                var redirectUrl = resultModel.Redirect2Url;
                if (string.IsNullOrEmpty(redirectUrl))
                    redirectUrl = DefaultLoginUrl;
                contextResult = new RedirectResult(redirectUrl);
            }
            else //返回指定视图
            {
                var viewName = resultModel.CustomErrorViewName;
                var viewResult = new ViewResult { ViewName = viewName, StatusCode = 200 };
/*
                    //TODO: 错误处理：判断视图是否存在——不知为何这里无法获取到 ViewEngine
                    if (!CheckViewExists(context, viewResult.ViewEngine, viewName)) //对应视图不存在
                    {
                        if (context.HttpContext.RequestServices.GetService<IHostingEnvironment>().IsDevelopment())
                            //注意：如果抛出异常，则只能由外层的 MiddleWare 捕获
                            // ReSharper disable once UnthrowableException
                            throw new Exception($"无法在指定位置找到对应的视图，请检查：{viewName}");
                        //非开发者模式
                        context.Result = new NotFoundObjectResult($"{viewName}");
                        context.ExceptionHandled = true;
                        return;
                    }
*/

                var modelMetadataProvider = (IModelMetadataProvider)context.HttpContext.RequestServices.GetService(typeof(IModelMetadataProvider));
                viewResult.ViewData = new(modelMetadataProvider, context.ModelState)
                {
                    Model = resultModel
                };
                contextResult = viewResult;
            }
        }
        context.Result = contextResult;
        context.ExceptionHandled = true;
    }

    private bool CheckViewExists(ActionContext context, IViewEngine viewEngine, string viewName)
    {
        if (!_CustomViewChecked.ContainsKey(viewName))
            //检查、记录视图存在状态
            _CustomViewChecked.Add(viewName, viewEngine.FindView(context, viewName, false) != null);
        return _CustomViewChecked[viewName];
    }
}