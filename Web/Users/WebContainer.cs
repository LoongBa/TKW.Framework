namespace TKW.Framework.Web.Users
{
    public class WebContainer(WebContainerType type = WebContainerType.UnSet)
    {
        public WebContainerType Type { get; internal set; } = type;

        public bool IsWechatApp => (Type & WebContainerType.WechatApp) == WebContainerType.WechatApp;
        public bool IsAliPayApp => (Type & WebContainerType.AliPayApp) == WebContainerType.AliPayApp;
        public bool IsTkwAppShell => (Type & WebContainerType.TkwAppShell) == WebContainerType.TkwAppShell;
        public bool IsDingDingApp => (Type & WebContainerType.DingDingApp) == WebContainerType.DingDingApp;
        public bool IsICBCApp => (Type & WebContainerType.ICBC_Elink) == WebContainerType.ICBC_Elink;
    }
}