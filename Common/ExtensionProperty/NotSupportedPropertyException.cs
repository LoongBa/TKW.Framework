using System;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.ExtensionProperty {
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