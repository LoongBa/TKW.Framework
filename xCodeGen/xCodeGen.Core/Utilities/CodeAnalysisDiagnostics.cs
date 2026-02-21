using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;

namespace xCodeGen.Core.Utilities
{
    /// <summary>
    /// 提供代码分析和诊断相关的工具方法
    /// </summary>
    public static class CodeAnalysisDiagnostics
    {
        private static readonly string[] _collectionTypeNames =
        [
            "IEnumerable",
            "ICollection",
            "IList",
            "List",
            "Array",
            "HashSet",
            "Dictionary",
            "Queue",
            "Stack"
        ];

        public static bool HasAttribute(this INamedTypeSymbol type, string attributeFullName)
        {
            return type?.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() == attributeFullName) ?? false;
        }

        public static T GetAttribute<T>(this INamedTypeSymbol type) where T : Attribute
        {
            return type?.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == typeof(T).FullName)?
                .ApplyToConstructor<T>();
        }

        /// <summary>
        /// 检查语法节点是否是带有 GenerateCode 特性的类型声明（支持 class 和 record）
        /// </summary>
        public static bool IsCandidateClass(SyntaxNode node)
        {
            // 核心修改：改为 TypeDeclarationSyntax 以支持 class/record/struct
            if (!(node is TypeDeclarationSyntax typeDeclaration))
                return false;

            return typeDeclaration.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString().Contains("GenerateCode"));
        }

        public static bool IsSpecialMethod(IMethodSymbol method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            if (method.MethodKind == MethodKind.Constructor ||
                method.MethodKind == MethodKind.StaticConstructor ||
                method.MethodKind == MethodKind.PropertyGet ||
                method.MethodKind == MethodKind.PropertySet ||
                method.MethodKind == MethodKind.EventAdd ||
                method.MethodKind == MethodKind.EventRemove ||
                method.ExplicitInterfaceImplementations != null ||
                method.MethodKind == MethodKind.UserDefinedOperator ||
                method.MethodKind == MethodKind.BuiltinOperator)
            {
                return true;
            }

            return false;
        }

        public static string GetAccessModifier(Accessibility accessibility)
        {
            return accessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Internal => "internal",
                Accessibility.Private => "private",
                Accessibility.Protected => "protected",
                Accessibility.ProtectedAndInternal => "private protected",
                Accessibility.ProtectedOrInternal => "protected internal",
                _ => "private",
            };
        }

        public static string GetCollectionItemType(ITypeSymbol type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (type is IArrayTypeSymbol arrayType) return arrayType.ElementType.ToDisplayString();
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var interfaces = namedType.AllInterfaces.Where(i => i.ToDisplayString().Contains("IEnumerable")).ToList();
                if (interfaces.Count > 0)
                {
                    return namedType.TypeArguments.FirstOrDefault()?.ToDisplayString();
                }
            }
            return null;
        }

        public static bool IsNullable(ITypeSymbol type)
        {
            if (type == null) return false;
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) return true;
            }
            return type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        public static bool IsCollectionType(ITypeSymbol type)
        {
            if (type == null) return false;
            if (type.TypeKind == TypeKind.Array) return true;
            if (type is INamedTypeSymbol namedType)
            {
                return namedType.AllInterfaces.Any(i => i.ToDisplayString().Contains("IEnumerable")) ||
                       _collectionTypeNames.Any(name => namedType.ToDisplayString().Contains(name));
            }
            return false;
        }

        public static bool HasSpecialMethod(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol == null) return false;
            return typeSymbol.GetMembers().OfType<IMethodSymbol>().Any(IsSpecialMethod);
        }
    }
}