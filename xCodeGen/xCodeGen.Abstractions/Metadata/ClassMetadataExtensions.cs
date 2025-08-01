using System.Collections.Generic;
using System.Linq;

namespace xCodeGen.Abstractions.Metadata
{
    public static class ClassMetadataExtensions
    {
        /// <summary>
        /// 检查是否包含指定方法
        /// </summary>
        public static bool HasMethod(this ClassMetadata metadata, string methodName)
        {
            return metadata.Methods.Any(m => m.Name == methodName);
        }

        /// <summary>
        /// 获取所有公共方法
        /// </summary>
        public static IEnumerable<MethodMetadata> GetPublicMethods(this ClassMetadata metadata)
        {
            return metadata.Methods.Where(m => m.AccessModifier == "public");
        }
    }
}