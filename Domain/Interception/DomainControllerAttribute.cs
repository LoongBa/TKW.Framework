using System;

namespace TKW.Framework.Domain.Interception;

/// <summary>领域控制器属性</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class DomainControllerAttribute : Attribute
{
        /// <summary>是否启用自动生成的装饰器，默认为 true</summary>
        public bool EnableAutoDecorator { get; set; } = true;
}