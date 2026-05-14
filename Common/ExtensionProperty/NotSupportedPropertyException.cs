using System;
using TKW.Framework.Extensions;

namespace TKW.Framework.ExtensionProperty {
    public class NotSupportedPropertyException : Exception
    {
        public string Namespace { get; }
        public string Version { get; }

        public NotSupportedPropertyException(string nameSpace, string version)
        {
            nameSpace.EnsureHasValue(nameof(nameSpace));
            version.EnsureHasValue(nameof(version));
            Namespace = nameSpace;
            Version = version;
        }
    }
}