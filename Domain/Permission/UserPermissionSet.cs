using System.Collections.Generic;

namespace TKW.Framework.Domain.Permission
{
    /// <summary>
    /// 用户权限集合
    /// </summary>
    public class UserPermissionSet
    {
        /// <summary>
        /// 菜单权限集
        /// </summary>
        public IReadOnlyList<MenuPermission> Menus { get; internal set; }
        /// <summary>
        /// 数据权限集
        /// </summary>
        public IReadOnlyList<DataPermission> Datas { get; internal set; }
        /// <summary>
        /// 功能权限集
        /// </summary>
        public IReadOnlyList<FunctionPermission> Functions { get; internal set; }
        /// <summary>
        /// 界面权限集
        /// </summary>
        public IReadOnlyList<UiPermission> Uis { get; internal set; }


        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public UserPermissionSet()
        {
            Menus = new List<MenuPermission>();
            Datas = new List<DataPermission>();
            Functions = new List<FunctionPermission>();
            Uis = new List<UiPermission>();
        }
    }
}