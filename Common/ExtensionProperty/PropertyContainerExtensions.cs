using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.ExtensionProperty {
    public static class PropertyContainerExtensions
    {
        public static T ToProperty<T>(this string left)
            where T : IProperty<T>, new()
        {
            left.AssertNotNull(name: nameof(left));
            return new PropertyContainer<T>(left).Content;
        }
        public static PropertyContainer<T> ToPropertyContainer<T>(this string left)
            where T : IProperty<T>, new()
        {
            return new PropertyContainer<T>(left.AssertNotNull());
        }

        public static PropertyContainer<T> ToPropertyContainer<T>(this T left)
            where T : IProperty<T>, new()
        {
            return new PropertyContainer<T>(left);
        }
    }
}