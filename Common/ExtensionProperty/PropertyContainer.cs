using System.Text.Json.Serialization;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.ExtensionProperty {
    public class PropertyContainer<T>
        where T : IProperty<T>, new()
    {
        public string Namespace { get; private set; }
        public string Version { get; private set; }

        public T Content { get; private set; }

        [JsonIgnore]
        public string ContentString { get; private set; }

        [JsonIgnore]
        public string PropertyContainerString { get; }

        public PropertyContainer(string propertyContainerString)
        {
            propertyContainerString.AssertNotNull();
            PropertyContainerString = propertyContainerString;

            Namespace = Version = ContentString = string.Empty;

            //反序列化，得到 NameSpace、Version、ContentString
            var bag = PropertyContainerString.ToObjectFromJson<PropertyContainer<T>>();
            CopyValues(bag);

            var property = new T();
            property.FromPropertyString(ContentString);
            Content = property;
            
            //判断命名空间、版本
            if (!property.NamespaceSupported.Equals(Namespace))
                throw new NotSupportedPropertyException(Namespace, Version);

            if (!property.VersionSupported.Equals(Version))
                throw new NotSupportedPropertyException(Namespace, Version);
        }
        public void CopyValues(PropertyContainer<T> bag)
        {
            Namespace = bag.Namespace;
            Version = bag.Version;
            ContentString = bag.ContentString;
            Content = bag.Content;
        }

        public PropertyContainer(T property)
        {
            Namespace = property.NamespaceSupported;
            Version = property.VersionSupported;
            ContentString = Content.ToPropertyString();
            Content = property;
        }
    }
}