namespace TKW.Framework.Web.Users
{
    public class WebContainer
    {
        public WebContainer(WebContainerType type = WebContainerType.UnSet)
        {
            Type = type;
        }

        public WebContainerType Type { get; internal set; }

        public bool IsWechatApp => (Type & WebContainerType.WechatApp) == WebContainerType.WechatApp;
        public bool IsAliPayApp => (Type & WebContainerType.AliPayApp) == WebContainerType.AliPayApp;
        public bool IsTkwAppShell => (Type & WebContainerType.TkwAppShell) == WebContainerType.TkwAppShell;
        public bool IsDingDingApp => (Type & WebContainerType.DingDingApp) == WebContainerType.DingDingApp;
        public bool IsICBCApp => (Type & WebContainerType.ICBCELink) == WebContainerType.ICBCELink;
    }
}