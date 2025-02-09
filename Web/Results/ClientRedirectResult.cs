using Microsoft.AspNetCore.Mvc;

namespace TKW.Framework.Web.Results
{
    /// <summary>
    /// 浏览器跳转-客户端上使用JS执行
    /// </summary>
    public class ClientRedirectResult : ContentResult
    {
        public ClientRedirectResult(string redirectUrl)
        {
            Content = $"<script language=\"javascript\">location.href='{redirectUrl}';</script>";
            ContentType = "text/html";
            //ContentEncoding = Encoding.UTF8;
        }
    }
}
