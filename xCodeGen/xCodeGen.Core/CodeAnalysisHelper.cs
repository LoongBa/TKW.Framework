// 注意：本文件需遵循 C# 7.3 语法标准，请勿使用更高版本特性（如模式匹配、空值判断运算符等）
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;
using xCodeGen.Abstractions.Attributes;

namespace xCodeGen.Core
{
    /// <summary>
    /// 代码分析辅助工具类，封装公共的语法分析逻辑
    /// </summary>
    public static class CodeAnalysisHelper
    {
        /// <summary>
        /// 从特性数据中提取生成相关参数
        /// </summary>
        public static (string Type, string TemplateName, bool Overwrite) ExtractGenerateAttributeParams(AttributeData attribute)
        {
            string type = null;
            var templateName = "Default";
            var overwrite = false;

            foreach (var arg in attribute.NamedArguments)
            {
                switch (arg.Key)
                {
                    case "Type":
                        type = arg.Value.Value?.ToString();
                        break;
                    case "TemplateName":
                        templateName = arg.Value.Value?.ToString() ?? "Default";
                        break;
                    case "Overwrite":
                        overwrite = arg.Value.Value is bool b && b;
                        break;
                }
            }

            return (type, templateName, overwrite);
        }

        /// <summary>
        /// 将访问修饰符转换为字符串表示
        /// </summary>
        public static string GetAccessModifier(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.Public:
                    return "public";
                case Accessibility.Private:
                    return "private";
                case Accessibility.Protected:
                    return "protected";
                case Accessibility.Internal:
                    return "internal";
                case Accessibility.ProtectedOrInternal:
                    return "protected internal";
                case Accessibility.ProtectedAndInternal:
                    return "private protected";
                default:
                    return "private";
            }
        }

        /// <summary>
        /// 获取集合元素类型（数组元素类型或泛型集合的元素类型）
        /// </summary>
        public static string GetCollectionItemType(ITypeSymbol type)
        {
            // 数组类型
            if (type is IArrayTypeSymbol arrayType)
                return arrayType.ElementType.ToDisplayString();

            // 泛型集合类型
            var namedType = type as INamedTypeSymbol;
            return namedType?.TypeArguments.Length > 0
                ? namedType.TypeArguments[0].ToDisplayString()
                : null;
        }

        /// <summary>
        /// 获取属性值
        /// </summary>
        public static string GetAttributeValue(AttributeData attribute, string propertyName)
        {
            var namedArg = attribute.NamedArguments.FirstOrDefault(a => a.Key == propertyName);
            return namedArg.Value.Value?.ToString();
        }

        /// <summary>
        /// 获取属性的 bool 值
        /// </summary>
        public static bool GetAttributeBoolValue(AttributeData attribute, string propertyName)
        {
            var namedArg = attribute.NamedArguments.FirstOrDefault(a => a.Key == propertyName);
            return namedArg.Value.Value is bool && (bool)namedArg.Value.Value;
        }

        /// <summary>
        /// 从语法上下文获取类符号并记录调试信息
        /// </summary>
        public static INamedTypeSymbol GetClassSymbolWithDebug(GeneratorSyntaxContext context, Action<string> logDebug)
        {
            ClassDeclarationSyntax classDecl = context.Node as ClassDeclarationSyntax;
            if (classDecl == null)
                return null;

            INamedTypeSymbol classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
            if (classSymbol == null)
            {
                if (logDebug != null)
                    logDebug($"⚠️ 无法获取 {classDecl.Identifier.Text} 的符号信息");
                return null;
            }

            if (logDebug != null)
                logDebug($"🔅 发现类: {GetTypeFullName(classSymbol)}");
            return classSymbol;
        }

        /// <summary>
        /// 获取生成特性的符号信息
        /// </summary>
        public static AttributeData GetGenerateAttribute(Compilation compilation, INamedTypeSymbol classSymbol, string attributeFullName)
        {
            var attributeType = compilation.GetTypeByMetadataName(attributeFullName);
            if (attributeType == null)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 无法找到特性类型: {attributeFullName}");
                return null;
            }

            return classSymbol.GetAttributes()
                .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
        }

        /// <summary>
        /// 获取类型的完全限定名（包含命名空间）
        /// </summary>
        public static string GetTypeFullName(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.ContainingNamespace.IsGlobalNamespace)
                return typeSymbol.Name;
            return $"{typeSymbol.ContainingNamespace}.{typeSymbol.Name}";
        }

        /// <summary>
        /// 检查类是否标记了[GenerateCode]特性
        /// </summary>
        public static bool HasGenerateCodeAttribute(Compilation compilation, ClassDeclarationSyntax classDecl)
        {
            var semanticModel = compilation.GetSemanticModel(classDecl.SyntaxTree);
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

            return classSymbol?.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() == GenerateCodeAttribute.TypeFullName) ?? false;
        }

        /// <summary>
        /// 判断语法节点是否为候选类（类声明且包含特性）
        /// </summary>
        public static bool IsCandidateClass(SyntaxNode node)
        {
            var isCandidate = node is ClassDeclarationSyntax classDecl && classDecl.AttributeLists.Any();
            System.Diagnostics.Debug.WriteLine($"IsCandidateClass: {node.GetType().Name} -> {isCandidate}");
            return isCandidate;
        }

        /// <summary>
        /// 判断类型是否为集合类型（数组或实现了IEnumerable）
        /// </summary>
        public static bool IsCollectionType(ITypeSymbol type)
        {
            // 数组类型
            if (type is IArrayTypeSymbol) return true;

            // 实现了 IEnumerable<T> 的类型
            var namedType = type as INamedTypeSymbol;
            return namedType?.AllInterfaces.Any(i =>
                i.ToDisplayString() == "System.Collections.Generic.IEnumerable`1") ?? false;
        }

        /// <summary>
        /// 判断类型是否为可空类型
        /// </summary>
        public static bool IsNullableType(ITypeSymbol type)
        {
            // 处理 Nullable<T> 类型
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType &&
                namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return true;

            // 处理可为空引用类型（C# 8.0+）
            return type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        /// <summary>
        /// 判断方法是否为特殊方法（属性访问器、事件访问器、运算符、构造函数等）
        /// </summary>
        public static bool IsSpecialMethod(IMethodSymbol method)
        {
            if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")) return true; // 属性访问器
            if (method.Name.StartsWith("add_") || method.Name.StartsWith("remove_")) return true; // 事件访问器
            if (method.Name.StartsWith("op_")) return true; // 运算符重载
            if (method.MethodKind == MethodKind.Constructor || method.MethodKind == MethodKind.Destructor) return true; // 构造/析构函数
            return false;
        }
    }
}