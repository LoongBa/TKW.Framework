using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace xCodeGen.Abstractions.Metadata
{
    public class ClassMetadata
    {
        public string Namespace { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public MetaType Type { get; set; } = MetaType.Other;
        public string DecoratorTypeFullName { get; set; } = string.Empty;
        public bool HasDecoratorCandidate { get; set; } = false;

        public ICollection<MethodMetadata> Methods { get; set; } = new Collection<MethodMetadata>();
        public ICollection<PropertyMetadata> Properties { get; set; } = new Collection<PropertyMetadata>();
        public ICollection<string> ImplementedInterfaces { get; set; } = new Collection<string>();
        public ICollection<string> DependentTypes { get; set; } = new Collection<string>();
        public string BaseType { get; set; } = string.Empty;
        public string BaseUserType { get; set; } = string.Empty;

        public MetadataSource SourceType { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public Dictionary<string, object> GenerateCodeSettings { get; set; } = new Dictionary<string, object>();
        public ICollection<AttributeMetadata> Attributes { get; set; } = new Collection<AttributeMetadata>();

        public string SourceHash { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public bool IsRecord { get; set; }
        public string TypeKind { get; set; } = "class";
        public ClassMetadata Service { get; set; } = null;
        public ClassMetadata Controller { get; set; } = null;
        public ClassMetadata Entity { get; set; } = null;
        public ClassMetadata Interface { get; set; } = null;

        public ClassMetadata() { }
    }

    public enum MetaType
    {
        Other = 0,
        Entity = 1,
        View = 2,
        Service = 3,
        Controller = 4,
        Decorator = 5,
        Interface
    }

    /// <summary>
    /// 领域服务注册信息模型
    /// </summary>
    public class DomainServiceRegistration
    {
        public Type ServiceInterface { get; }
        public Type Implementation { get; }
        public Type ProxyType { get; }
        public MetaType Type { get; }

        /// <summary>
        /// 领域服务注册信息模型
        /// </summary>
        public DomainServiceRegistration(Type serviceInterface,
            Type implementation,
            Type proxyType,
            MetaType type)
        {
            ServiceInterface = serviceInterface;
            Implementation = implementation;
            ProxyType = proxyType;
            Type = type;
        }
    }
}