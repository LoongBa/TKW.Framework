using System;
using System.Text.Json.Serialization;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.ExtensionProperty
{
    public abstract class PropertyBase<T> : IProperty<T>
        where T : IProperty<T>, new()
    {
        #region Implementation of IProperty<T>
        protected abstract string SetNamespaceSupported();
        protected abstract string SetVersionSupported();

        [JsonIgnore]
        public string NamespaceSupported => SetNamespaceSupported();

        [JsonIgnore]
        public string VersionSupported => SetVersionSupported();

        public virtual void FromPropertyString(string propertyString)
        {
            propertyString.EnsureNotNull();
            CopyValues(propertyString.ToObjectFromJson<T>());
        }

        public virtual void CopyValues(T property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
        }

        public virtual string ToPropertyString()
        {
            return this.ToJson();
        }
        #endregion
    }
}