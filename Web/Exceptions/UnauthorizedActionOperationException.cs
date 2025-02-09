using System;

namespace TKW.Framework.Web.Exceptions
{
    /// <summary>
    /// 未经授权的 Action 操作异常
    /// </summary>
    public class UnauthorizedActionOperationException : UnauthorizedAccessException
    {
        /// <summary>
        /// 所需的角色名称
        /// </summary>
        public string[] RolesRequired { get; }
        /// <summary>
        /// 操作名称
        /// </summary>
        public string OperationName { get; }
        /// <summary>
        /// 控制器名称
        /// </summary>
        public string ControllerName { get; }
        /// <summary>
        /// Action 名称
        /// </summary>
        public string ActionName { get; }

        public UnauthorizedActionOperationException(string operationName, string controllerName, string actionName, string[] rolesRequired = null) : this(operationName, controllerName, actionName, "", rolesRequired)
        {
        }

        public UnauthorizedActionOperationException(string operationName, string controllerName, string actionName, string message, string[] rolesRequired = null) : base(message)
        {
            RolesRequired = rolesRequired;
            OperationName = operationName;
            ControllerName = controllerName;
            ActionName = actionName;
        }
    }
}