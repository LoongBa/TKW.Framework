using System;
using System.Collections.Generic;
using TKW.Framework.Web.Users;

namespace TKW.Framework.Web.Exceptions
{
    public class ContainerNotSupportedException : Exception
    {
        public WebContainerType[] SupportedType { get; }
        public WebContainerType NotSupportedType { get; }

        public ContainerNotSupportedException(
            WebContainerType supportedType,
            WebContainerType notSupportedType) : base($"当前操作不支持此容器类型：{notSupportedType}")
        {
            //展开 supportedType 文字描述，便于理解
            SupportedType = SupportedType2Array(supportedType);
            NotSupportedType = notSupportedType;
        }

        private static WebContainerType[] SupportedType2Array(WebContainerType type)
        {
            var array = new List<WebContainerType>();

            if ((type & WebContainerType.AliPayApp) == WebContainerType.AliPayApp)
                array.Add(WebContainerType.AliPayApp);

            if ((type & WebContainerType.MobileWebBrowser) == WebContainerType.MobileWebBrowser)
                array.Add(WebContainerType.MobileWebBrowser);

            if ((type & WebContainerType.TkwAppShell) == WebContainerType.TkwAppShell)
                array.Add(WebContainerType.TkwAppShell);

            if ((type & WebContainerType.Customized) == WebContainerType.Customized)
                array.Add(WebContainerType.Customized);

            if ((type & WebContainerType.DingDingApp) == WebContainerType.DingDingApp)
                array.Add(WebContainerType.DingDingApp);

            if ((type & WebContainerType.PCWebBrowser) == WebContainerType.PCWebBrowser)
                array.Add(WebContainerType.PCWebBrowser);

            if ((type & WebContainerType.WechatApp) == WebContainerType.WechatApp)
                array.Add(WebContainerType.WechatApp);

            if ((type & WebContainerType.ICBCELink) == WebContainerType.ICBCELink)
                array.Add(WebContainerType.ICBCELink);

            if ((type & WebContainerType.WechatPCWebBrowser) == WebContainerType.WechatPCWebBrowser)
                array.Add(WebContainerType.WechatPCWebBrowser);

            // TODO：当WebContainerType枚举添加明确类型的Type时，在此把该Type添加到数组中

            return array.ToArray();
        }
    }
}