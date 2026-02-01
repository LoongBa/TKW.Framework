using TKW.Framework.Common.Tools;
using TKW.Framework.Domain;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Web.Users;

public abstract class WebDomainUser : DomainUser, IWebUser
{
    protected WebDomainUser(DomainUser domainUser) : base(domainUser.DomainHostFactory)
    {
        Container = new WebContainer { Type = WebContainerType.UnSet };
        // 复制 DomainUser 信息
        this.CopyValuesFrom(domainUser);
    }

    #region Implementation of IWebUser

    public WebContainer Container { get; protected set; }

    public virtual void SetContainer(string userAgent)
    {
        UserAgent = userAgent;

        if (string.IsNullOrWhiteSpace(userAgent))
        {
            Container.Type = WebContainerType.Unknown;
            return;
        }

        //TODO: 判断容器类型
        if (userAgent.Contains("MicroMessenger"))
        {
            Container.Type = WebContainerType.WechatApp;
            return;
        }
        if (userAgent.Contains("WindowsWechat"))
        {
            Container.Type = WebContainerType.WechatPCWebBrowser;
            return;
        }
        if (userAgent.ToUpper().Contains("ICBC"))
        {
            Container.Type = WebContainerType.ICBC_Elink;
            return;
        }
        else if (userAgent.Contains("Windows Phone"))
            Container.Type = WebContainerType.MobileWebBrowser;

    }

    public string UserAgent { get; protected set; }

    #endregion
}