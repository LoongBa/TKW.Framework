using TKW.Framework.Domain;

namespace TKW.Framework.Web.Users
{
    public interface IWebUser
    {
        WebContainer Container { get; }
        string UserAgent { get; }

        UserAuthenticationType AuthenticationType { get; }
        void SetContainer(string userAgent);
    }
}