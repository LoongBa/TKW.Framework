namespace TKW.Framework.Common.ExtensionProperty
{
    public interface IProperty<T>
        where T : IProperty<T>, new()
    {
        string NamespaceSupported { get; }
        string VersionSupported { get; }
        void FromPropertyString(string propertyString);
        string ToPropertyString();
    }
}