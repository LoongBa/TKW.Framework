using System;
using Microsoft.AspNetCore.Http;

namespace TKW.Framework.Web;

/// <summary>
/// 首次请求捕获事件的参数类，包含首次请求的相关信息
/// </summary>
public class FirstRequestCapturedEventArgs : EventArgs
{
    /// <summary>
    /// 首次请求的完整URL
    /// </summary>
    public string InitialUrl { get; init; }

    /// <summary>
    /// 服务器根路径（协议+主机）
    /// </summary>
    public string ServerRoot { get; init; }

    /// <summary>
    /// 首次请求的上下文对象，包含完整的请求信息
    /// </summary>
    public HttpContext HttpContext { get; init; }
}