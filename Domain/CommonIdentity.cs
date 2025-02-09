using System.Security.Principal;

namespace TKW.Framework.Domain
{
    /// <inheritdoc />
    /// <summary>
    /// 用户身份基类
    /// </summary>
    public class CommonIdentity : IIdentity
    {
        public CommonIdentity(string name, string authenticationType, bool isAuthenticated)
        {
            AuthenticationType = authenticationType;
            IsAuthenticated = isAuthenticated;
            Name = name;
        }

        public string AuthenticationType { get; }

        public string Name { get; }

        public bool IsAuthenticated { get; }

    }
}