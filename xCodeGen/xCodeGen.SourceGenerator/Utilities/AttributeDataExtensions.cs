#nullable disable
using Microsoft.CodeAnalysis;
using xCodeGen.Abstractions.Attributes;

namespace xCodeGen.SourceGenerator.Utilities
{
    /// <summary>
    /// 特性数据扩展方法 (C# 7.3 兼容版)
    /// </summary>
    public static class AttributeDataExtensions
    {
        /// <summary>
        /// 检查是否具有指定的代码生成特性
        /// </summary>
        public static bool HasGenerateCodeAttribute(this INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
                return false;

            foreach (var attr in typeSymbol.GetAttributes())
            {
                if (attr.AttributeClass != null && attr.AttributeClass.ToDisplayString() == DomainGenerateCodeAttribute.TypeFullName)
                    return true;
            }

            return false;
        }
    }
}