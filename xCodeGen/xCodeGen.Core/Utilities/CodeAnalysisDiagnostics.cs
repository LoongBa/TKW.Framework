using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;

namespace xCodeGen.Core.Utilities
{
    /// <summary>
    /// 提供代码分析和诊断相关的工具方法
    /// </summary>
    /// <remarks>
    /// 这个类包含用于检查类型特性、方法类型、访问修饰符等静态方法。
    /// 所有方法都是线程安全的，可以在多线程环境中使用。
    /// </remarks>
    public static class CodeAnalysisDiagnostics
    {
        /// <summary>
        /// 常见集合类型名称数组
        /// </summary>
        /// <remarks>
        /// 用于快速判断一个类型是否为集合类型。
        /// 包含常见的集合接口和实现类名称。
        /// </remarks>
        private static readonly string[] CollectionTypeNames = new string[]
        {
            "IEnumerable",
            "ICollection",
            "IList",
            "List",
            "Array",
            "HashSet",
            "Dictionary",
            "Queue",
            "Stack"
        };

        /// <summary>
        /// 检查类型是否具有指定特性
        /// </summary>
        /// <param name="type">类型符号</param>
        /// <param name="attributeFullName">特性全名</param>
        /// <returns>如果类型具有指定特性返回 true，否则返回 false</returns>
        public static bool HasAttribute(this INamedTypeSymbol type, string attributeFullName)
        {
            return type?.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() == attributeFullName) ?? false;
        }

        /// <summary>
        /// 获取指定类型的特性
        /// </summary>
        /// <typeparam name="T">特性类型</typeparam>
        /// <param name="type">类型符号</param>
        /// <returns>特性实例</returns>
        public static T GetAttribute<T>(this INamedTypeSymbol type) where T : Attribute
        {
            return type?.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == typeof(T).FullName)?
                .ApplyToConstructor<T>();
        }

        /// <summary>
        /// 检查语法节点是否是带有 GenerateCode 特性的类声明
        /// </summary>
        /// <param name="node">语法节点</param>
        /// <returns>如果是带有 GenerateCode 特性的类声明返回 true，否则返回 false</returns>
        public static bool IsCandidateClass(SyntaxNode node)
        {
            if (!(node is ClassDeclarationSyntax classDeclaration))
                return false;

            return classDeclaration.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() == "GenerateCode");
        }

        /// <summary>
        /// 检查方法是否为特殊方法（如构造函数、析构函数、属性访问器等）
        /// </summary>
        /// <param name="method">方法符号</param>
        /// <returns>如果方法是特殊方法返回 true，否则返回 false</returns>
        public static bool IsSpecialMethod(IMethodSymbol method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            // 检查是否为构造函数
            if (method.MethodKind == MethodKind.Constructor ||
                method.MethodKind == MethodKind.StaticConstructor)
            {
                return true;
            }

            // 检查是否为属性访问器
            if (method.MethodKind == MethodKind.PropertyGet ||
                method.MethodKind == MethodKind.PropertySet)
            {
                return true;
            }

            // 检查是否为事件访问器
            if (method.MethodKind == MethodKind.EventAdd ||
                method.MethodKind == MethodKind.EventRemove)
            {
                return true;
            }

            // 检查是否为显式实现的接口方法
            if (method.ExplicitInterfaceImplementations != null)
            {
                return true;
            }

            // 检查是否为重载运算符
            if (method.MethodKind == MethodKind.UserDefinedOperator ||
                method.MethodKind == MethodKind.BuiltinOperator)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 将访问修饰符转换为字符串
        /// </summary>
        /// <param name="accessibility">访问修饰符</param>
        /// <returns>访问修饰符的字符串表示</returns>
        public static string GetAccessModifier(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.Public:
                    return "public";
                case Accessibility.Internal:
                    return "internal";
                case Accessibility.Private:
                    return "private";
                case Accessibility.Protected:
                    return "protected";
                case Accessibility.ProtectedAndInternal:
                    return "private protected";
                case Accessibility.ProtectedOrInternal:
                    return "protected internal";
                default:
                    return "private";
            }
        }

        /// <summary>
        /// 获取集合元素的类型
        /// </summary>
        /// <param name="type">集合类型</param>
        /// <returns>集合元素类型的字符串表示，如果不是集合类型则返回 null</returns>
        public static string GetCollectionItemType(ITypeSymbol type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            // 处理数组类型
            if (type is IArrayTypeSymbol arrayType)
            {
                return arrayType.ElementType.ToDisplayString();
            }

            // 处理泛型集合类型
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var interfaces = namedType.AllInterfaces
                    .Where(i => i.ToDisplayString().Contains("IEnumerable"))
                    .ToList();

                if (interfaces.Count > 0)
                {
                    var genericType = namedType.TypeArguments.FirstOrDefault();
                    if (genericType != null)
                    {
                        return genericType.ToDisplayString();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 检查类型是否为可空类型
        /// </summary>
        /// <param name="type">要检查的类型</param>
        /// <returns>
        /// 如果类型是可空类型返回 true，否则返回 false。
        /// 支持可空值类型（如 int?、DateTime?）和可空引用类型（C# 8.0+）
        /// </returns>
        public static bool IsNullable(ITypeSymbol type)
        {
            if (type == null)
                return false;

            // 处理可空值类型（如 int?、DateTime?）
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var originalDefinition = namedType.OriginalDefinition;
                if (originalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    return true;
                }
            }

            // 处理可空引用类型（C# 8.0+）
            return type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        /// <summary>
        /// 检查类型是否为集合类型
        /// </summary>
        /// <param name="type">要检查的类型</param>
        /// <returns>
        /// 如果类型是集合类型返回 true，否则返回 false。
        /// 支持数组、IEnumerable 接口和常见集合类型
        /// </returns>
        public static bool IsCollectionType(ITypeSymbol type)
        {
            if (type == null)
                return false;

            // 检查数组类型
            if (type.TypeKind == TypeKind.Array)
                return true;

            // 检查泛型集合类型
            if (type is INamedTypeSymbol namedType)
            {
                return namedType.AllInterfaces.Any(i => i.ToDisplayString().Contains("IEnumerable")) ||
                       CollectionTypeNames.Any(name => namedType.ToDisplayString().Contains(name));
            }

            return false;
        }

        /// <summary>
        /// 检查类型是否包含特殊方法
        /// </summary>
        /// <param name="typeSymbol">类型符号</param>
        /// <returns>
        /// 如果类型包含特殊方法（构造函数、属性访问器、事件访问器等）返回 true，否则返回 false
        /// </returns>
        public static bool HasSpecialMethod(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
                return false;

            return typeSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(IsSpecialMethod);
        }
    }
}
