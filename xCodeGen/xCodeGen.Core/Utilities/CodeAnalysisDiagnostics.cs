using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;

namespace xCodeGen.SourceGenerator
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
        /// 检查语法节点是否是候选类型声明（支持 public/internal 的 class 和 record）
        /// </summary>
        public static bool IsCandidateClass(SyntaxNode node)
        {
            if (!(node is TypeDeclarationSyntax type))
                return false;

            // 1. 依然排除抽象类
            if (type.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
                return false;

            // 2. --- 核心修改：允许 public 或 internal ---
            // 这样控制器声明为 internal partial 才能被 SG 探测到
            bool isVisible = type.Modifiers.Any(m =>
                m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.InternalKeyword));

            if (!isVisible)
                return false;

            // 3. 检查 DomainGenerateCodeAttribute (实体/视图)
            var hasGenerateAttr = type.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString().Contains("DomainGenerateCode"));

            if (hasGenerateAttr) return true;

            // 4. 基于名称的启发式判断 (Service/Controller/Decorator)
            var name = type.Identifier.Text;
            if (!string.IsNullOrEmpty(name) && (
                name.EndsWith("DataService", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("Decorator", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("Service", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // 5. 基于基类列表的判断
            if (type.BaseList != null)
            {
                foreach (var bt in type.BaseList.Types)
                {
                    var baseName = bt.Type.ToString();
                    if (string.IsNullOrEmpty(baseName)) continue;

                    if (baseName.IndexOf("IDomainService", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        baseName.IndexOf("DomainDataServiceBase", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        baseName.IndexOf("DomainReadOnlyDataServiceBase", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        baseName.IndexOf("DomainServiceBase", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        baseName.IndexOf("DomainControllerBase", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsSpecialMethod(IMethodSymbol method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            // 检查是否为构造函数、属性访问器、事件访问器或运算符
            if (method.MethodKind == MethodKind.Constructor ||
                method.MethodKind == MethodKind.StaticConstructor ||
                method.MethodKind == MethodKind.PropertyGet ||
                method.MethodKind == MethodKind.PropertySet ||
                method.MethodKind == MethodKind.EventAdd ||
                method.MethodKind == MethodKind.EventRemove ||
                method.MethodKind == MethodKind.UserDefinedOperator ||
                method.MethodKind == MethodKind.BuiltinOperator)
            {
                return true;
            }

            // 只有当你真的想排除“显式接口实现”（如 IInterface.Method）时才检查这个
            // 对于 AOP 契约生成，通常建议保留它们，或者使用 .Length > 0 判断
            if (!method.ExplicitInterfaceImplementations.IsEmpty)
            {
                // 如果你需要拦截显式实现的方法，这里应该返回 false
                // 如果不需要，则返回 true
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