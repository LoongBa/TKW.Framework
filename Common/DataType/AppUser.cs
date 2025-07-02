using TKW.Framework.Common.Enumerations;

namespace TKW.Framework.Common.DataType
{
    /// <summary>
    /// App 用户信息
    /// </summary>
    public class AppUser
    {
        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public AppUser()
        {
            AppId = string.Empty;
            EnumAppType = EnumAppType.Unset;
            Nickname = string.Empty;
            AvatarUrl = string.Empty;

        }

        /// <summary>
        /// App 类型
        /// </summary>
        public EnumAppType EnumAppType { get; set; }
        /// <summary>
        /// 用户 Id
        /// </summary>
        public string AppId { get; set; }
        /// <summary>
        /// 昵称
        /// </summary>
        public string Nickname { get; set; }
        /// <summary>
        /// 头像 Url
        /// </summary>
        public string AvatarUrl { get; set; }
    }
}