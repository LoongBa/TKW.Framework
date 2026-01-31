namespace TKW.Framework.Web.Users;

public interface IWebUser
{
    WebContainer Container { get; }
    string UserAgent { get; }

    void SetContainer(string userAgent);
}