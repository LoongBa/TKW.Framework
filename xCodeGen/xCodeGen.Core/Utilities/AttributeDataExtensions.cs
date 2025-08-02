using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;
using xCodeGen.Abstractions.Attributes;

namespace xCodeGen.Core.Utilities
{
    /// <summary>
    /// 特性数据扩展方法
    /// </summary>
    public static class AttributeDataExtensions
    {
        /// <summary>
        /// 获取特性参数值
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="attributeData">特性数据</param>
        /// <param name="index">参数索引</param>
        /// <returns>参数值</returns>
        public static T GetArgumentValue<T>(this AttributeData attributeData, int index)
        {
            if (attributeData == null)
                throw new ArgumentNullException(nameof(attributeData));

            if (index < 0 || index >= attributeData.ConstructorArguments.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            var argument = attributeData.ConstructorArguments[index];
            if (argument.Value is T value)
                return value;

            return default;
        }

        /// <summary>
        /// 应用特性到构造函数
        /// </summary>
        /// <typeparam name="T">特性类型</typeparam>
        /// <param name="attributeData">特性数据</param>
        /// <returns>特性实例</returns>
        public static T ApplyToConstructor<T>(this AttributeData attributeData) where T : Attribute
        {
            if (attributeData == null)
                throw new ArgumentNullException(nameof(attributeData));

            var args = attributeData.ConstructorArguments
                .Select(arg => arg.Value)
                .ToArray();

            return (T)Activator.CreateInstance(typeof(T), args);
        }

        /// <summary>
        /// 获取类的 Generate 特性信息
        /// </summary>
        /// <param name="compilation">编译信息</param>
        /// <param name="classSymbol">类符号</param>
        /// <param name="typeFullName">特性类型全名</param>
        /// <returns>特性数据，如果未找到则返回 null</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static AttributeData GetGenerateAttribute(
            this Compilation compilation,
            INamedTypeSymbol classSymbol,
            string typeFullName)
        {
            if (classSymbol == null)
                throw new ArgumentNullException(nameof(classSymbol));
            if (string.IsNullOrWhiteSpace(typeFullName))
                throw new ArgumentNullException(nameof(typeFullName));

            // 获取类的所有特性
            var attributes = classSymbol.GetAttributes();

            // 查找指定类型的特性
            var attribute = attributes.FirstOrDefault(attr =>
                attr.AttributeClass?.ToDisplayString() == typeFullName);

            return attribute;
        }

        /// <summary>
        /// 扩展方法：获取 Generate 特性信息
        /// </summary>
        /// <param name="classSymbol">类符号</param>
        /// <param name="typeFullName">特性类型全名</param>
        /// <returns>特性数据，如果未找到则返回 null</returns>
        public static AttributeData GetGenerateAttribute(
            this INamedTypeSymbol classSymbol,
            string typeFullName)
        {
            if (classSymbol == null)
                throw new ArgumentNullException(nameof(classSymbol));
            if (string.IsNullOrWhiteSpace(typeFullName))
                throw new ArgumentNullException(nameof(typeFullName));

            return classSymbol.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == typeFullName);
        }

        /// <summary>
        /// 检查类型是否具有 GenerateCode 特性
        /// </summary>
        /// <param name="typeSymbol">类型符号</param>
        /// <returns>如果类型具有 GenerateCode 特性返回 true，否则返回 false</returns>
        public static bool HasGenerateCodeAttribute(this INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
                return false;

            return typeSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() == GenerateCodeAttribute.TypeFullName);
        }

        /// <summary>
        /// 检查类声明是否具有 GenerateCode 特性
        /// </summary>
        /// <param name="classDeclaration">类声明语法节点</param>
        /// <param name="semanticModel">语义模型</param>
        /// <returns>如果类具有 GenerateCode 特性返回 true，否则返回 false</returns>
        public static bool HasGenerateCodeAttribute(this ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
        {
            if (classDeclaration == null)
                throw new ArgumentNullException(nameof(classDeclaration));

            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            return semanticModel.GetDeclaredSymbol(classDeclaration) is INamedTypeSymbol symbol &&
                   symbol.HasGenerateCodeAttribute();
        }

    }
}