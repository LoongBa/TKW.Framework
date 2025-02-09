using System;

namespace TKW.Framework.Web.Users
{
    [Flags]
    public enum WebContainerType
    {
        /// <summary>
        /// 未设置
        /// </summary>
        UnSet = 0x00,
        /// <summary>
        /// 微信
        /// </summary>
        WechatApp = 0x01,
        /// <summary>
        /// 支付宝钱包
        /// </summary>
        AliPayApp = 0x02,
        /// <summary>
        /// Tkw AppShell
        /// </summary>
        TkwAppShell = 0x04,
        /// <summary>
        /// Pc 浏览器
        /// </summary>
        PCWebBrowser = 0x08,
        /// <summary>
        /// 手机浏览器
        /// </summary>
        MobileWebBrowser = 0x10,
        /// <summary>
        /// 钉钉客户端
        /// </summary>
        DingDingApp = 0x20,
        /// <summary>
        /// 微信桌面端
        /// </summary>
        WechatPCWebBrowser = 0x40,
        /// <summary>
        /// 工行融e联
        /// </summary>
        ICBCELink = 0x80,
        /// <summary>
        /// 未知
        /// </summary>
        Unknown = 0x40000,
        /// <summary>
        /// 定制
        /// </summary>
        Customized = 0x80000,

        All = 0xFFFFF,
    }
}