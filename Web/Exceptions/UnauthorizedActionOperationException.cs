using System;

namespace TKW.Framework.Web.Exceptions
{
    /// <summary>
    /// 未经授权的 Action 操作异常
    /// </summary>
    public class UnauthorizedActionOperationException(
        string operationName,
        string controllerName,
        string actionName,
        string message,
        string[] rolesRequired = null)
        : UnauthorizedAccessException(message)
    {
        /// <summary>
        /// 所需的角色名称
        /// </summary>
        public string[] RolesRequired { get; } = rolesRequired;

        /// <summary>
        /// 操作名称
        /// </summary>
        public string OperationName { get; } = operationName;

        /// <summary>
        /// 控制器名称
        /// </summary>
        public string ControllerName { get; } = controllerName;

        /// <summary>
        /// Action 名称
        /// </summary>
        public string ActionName { get; } = actionName;

        public UnauthorizedActionOperationException(string operationName, string controllerName, string actionName, string[] rolesRequired = null) : this(operationName, controllerName, actionName, "", rolesRequired)
        {
        }
    }
}