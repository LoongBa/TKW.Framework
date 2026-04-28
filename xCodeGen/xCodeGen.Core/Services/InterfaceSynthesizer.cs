using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace xCodeGen.Core.Services
{
    public class InterfaceSynthesizer
    {
        public string Synthesize(string className, string serviceNamespace, string manualText, string generatedText)
            => Synthesize(className, serviceNamespace, manualText, generatedText, attributeWhitelist: null);

        public string Synthesize(string className, string serviceNamespace, string manualText, string generatedText, IEnumerable<string>? attributeWhitelist)
        {
            var interfaceName = $"I{className}Service";
            var interfaceNamespace = serviceNamespace.Replace(".Services", ".Interfaces");

            var whitelist = attributeWhitelist as string[] ?? attributeWhitelist?.ToArray();
            var manualMethods = ExtractMethods(manualText, isGenerated: false, whitelist);
            var generatedMethods = ExtractMethods(generatedText, isGenerated: true, whitelist);

            var allMethods = manualMethods.Concat(generatedMethods).ToList();

            var usings = CollectUsings(manualText, generatedText);
            usings.Add("using TKW.Framework.Domain.Interfaces;");

            return BuildFinalCode(interfaceNamespace, interfaceName, usings, allMethods);
        }

        private List<string> ExtractMethods(string source, bool isGenerated, IEnumerable<string>? attributeWhitelist)
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();
            var result = new List<string>();

            HashSet<string>? whitelist = null;
            if (attributeWhitelist != null)
            {
                whitelist = new HashSet<string>(attributeWhitelist
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s =>
                    {
                        var n = s.Trim();
                        if (n.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase))
                            n = n.Substring(0, n.Length - "Attribute".Length);
                        return n.ToLowerInvariant();
                    }));
            }

            var methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                            !m.Modifiers.Any(SyntaxKind.InternalKeyword));

            foreach (var method in methods)
            {
                var hasIncludedAttribute = MethodHasIncludedAttribute(method, whitelist);
                if (isGenerated && !IsDtoRelatedMethod(method) && !hasIncludedAttribute)
                    continue;

                var xmlDocBlock = ExtractXmlDocComment(method);
                if (!string.IsNullOrWhiteSpace(xmlDocBlock))
                    xmlDocBlock = xmlDocBlock.TrimEnd() + "\n";

                var attributesLines = BuildFilteredAttributeListLines(method.AttributeLists, whitelist);

                var parameterList = BuildFilteredParameterListText(method.ParameterList, whitelist);

                var modifiers = method.Modifiers
                    .Where(m => !m.IsKind(SyntaxKind.AsyncKeyword) &&
                                !m.IsKind(SyntaxKind.VirtualKeyword) &&
                                !m.IsKind(SyntaxKind.OverrideKeyword) &&
                                !m.IsKind(SyntaxKind.StaticKeyword) &&
                                !m.IsKind(SyntaxKind.PublicKeyword))
                    .Select(m => m.Text);
                var modifierStr = string.Join(" ", modifiers).Trim();
                if (!string.IsNullOrEmpty(modifierStr)) modifierStr += " ";

                var returnType = method.ReturnType.ToString().Trim();
                var methodName = method.Identifier.Text;
                var signatureLine = $"{modifierStr}{returnType} {methodName}({parameterList});";

                // 组合：xml doc + attributes (each in its own line) + signature (own line)
                var blockLines = new List<string>();
                if (!string.IsNullOrWhiteSpace(xmlDocBlock))
                {
                    // xmlDocBlock already contains newlines; split and add each
                    blockLines.AddRange(xmlDocBlock.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0));
                }
                if (attributesLines.Any())
                {
                    blockLines.AddRange(attributesLines);
                }
                blockLines.Add(signatureLine);

                // 统一缩进（接口体内：4 spaces）
                var indented = blockLines.Select(l => "    " + l).ToList();
                var combined = string.Join("\n", indented);

                result.Add(combined + "\n");
            }
            return result;
        }

        private bool MethodHasIncludedAttribute(MethodDeclarationSyntax method, HashSet<string>? whitelist)
        {
            if (!method.AttributeLists.Any())
            {
                // 检查参数级特性
                foreach (var p in method.ParameterList.Parameters)
                {
                    if (p.AttributeLists.Any()) return whitelist == null || whitelist.Count == 0 || p.AttributeLists.SelectMany(a => a.Attributes).Any(attr => whitelistContains(attr, whitelist));
                }
                return false;
            }
            if (whitelist == null || whitelist.Count == 0) return true;

            bool whitelistContains(AttributeSyntax attr, HashSet<string> w)
            {
                var n = GetAttributeSimpleName(attr).ToLowerInvariant();
                return w != null && w.Contains(n);
            }

            foreach (var al in method.AttributeLists)
            {
                foreach (var attr in al.Attributes)
                {
                    if (whitelistContains(attr, whitelist)) return true;
                }
            }
            foreach (var p in method.ParameterList.Parameters)
            {
                foreach (var pal in p.AttributeLists)
                {
                    foreach (var attr in pal.Attributes)
                    {
                        if (whitelistContains(attr, whitelist)) return true;
                    }
                }
            }
            return false;
        }

        private string GetAttributeSimpleName(AttributeSyntax attr)
        {
            var name = attr.Name.ToString().Trim();
            if (name.Contains('.')) name = name.Split('.').Last();
            if (name.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - "Attribute".Length);
            return name;
        }

        private List<string> BuildFilteredAttributeListLines(SyntaxList<AttributeListSyntax> attributeLists, HashSet<string>? whitelist)
        {
            var lines = new List<string>();
            foreach (var al in attributeLists)
            {
                var included = new List<string>();
                foreach (var attr in al.Attributes)
                {
                    if (ShouldIncludeAttribute(attr, whitelist))
                        included.Add(attr.ToFullString().Trim());
                }
                if (included.Any())
                {
                    var target = al.Target?.ToFullString() ?? "";
                    var line = $"[{(string.IsNullOrEmpty(target) ? "" : target)}{string.Join(", ", included)}]";
                    lines.Add(line);
                }
            }
            return lines;
        }

        private bool ShouldIncludeAttribute(AttributeSyntax attr, HashSet<string>? whitelist)
        {
            if (whitelist == null || whitelist.Count == 0) return true;
            var name = GetAttributeSimpleName(attr).ToLowerInvariant();
            return whitelist.Contains(name);
        }

        private string BuildFilteredParameterListText(ParameterListSyntax parameterList, HashSet<string>? whitelist)
        {
            var paramStrs = new List<string>();
            foreach (var p in parameterList.Parameters)
            {
                var attrParts = new List<string>();
                foreach (var pal in p.AttributeLists)
                {
                    var included = new List<string>();
                    foreach (var attr in pal.Attributes)
                    {
                        if (ShouldIncludeAttribute(attr, whitelist))
                            included.Add(attr.ToFullString().Trim());
                    }
                    if (included.Any())
                    {
                        var target = pal.Target?.ToFullString() ?? "";
                        attrParts.Add($"[{(string.IsNullOrEmpty(target) ? "" : target)}{string.Join(", ", included)}]");
                    }
                }
                var attrsPrefix = attrParts.Any() ? (string.Join(" ", attrParts) + " ") : "";

                var modifiers = p.Modifiers.ToFullString().Trim();
                var modPart = string.IsNullOrEmpty(modifiers) ? "" : (modifiers + " ");
                var typePart = p.Type?.ToString() ?? "";
                var identifier = p.Identifier.Text;
                var defaultVal = p.Default?.ToFullString() ?? "";
                var paramText = $"{attrsPrefix}{modPart}{typePart} {identifier}{defaultVal}".Trim();
                paramStrs.Add(paramText);
            }
            return string.Join(", ", paramStrs);
        }

        private string ExtractXmlDocComment(MethodDeclarationSyntax method)
        {
            var sb = new StringBuilder();
            foreach (var trivia in method.GetLeadingTrivia())
            {
                var kind = trivia.Kind();
                // 只保留 XML 文档注释形式
                if (kind == SyntaxKind.SingleLineDocumentationCommentTrivia ||
                    kind == SyntaxKind.MultiLineDocumentationCommentTrivia ||
                    trivia.GetStructure() is DocumentationCommentTriviaSyntax)
                {
                    var s = trivia.ToFullString();
                    if (s.TrimStart().StartsWith("///") || s.Contains("<summary>") || s.Contains("</summary>"))
                    {
                        // 保留原行注释样式
                        foreach (var line in s.Replace("\r\n", "\n").Split('\n'))
                        {
                            var trimmed = line.TrimEnd();
                            if (!string.IsNullOrWhiteSpace(trimmed))
                                sb.AppendLine(trimmed);
                        }
                    }
                }
            }
            return sb.ToString().TrimEnd();
        }

        private string BuildFinalCode(string ns, string name, HashSet<string> usings, List<string> methods)
        {
            var sbBody = new StringBuilder();
            sbBody.AppendLine("#nullable enable");
            foreach (var u in usings.OrderBy(x => x)) sbBody.AppendLine(u);
            sbBody.AppendLine();
            sbBody.AppendLine($"namespace {ns};");
            sbBody.AppendLine();
            sbBody.AppendLine($"public partial interface {name} : IDomainService");
            sbBody.AppendLine("{");
            sbBody.AppendLine();

            foreach (var m in methods)
            {
                sbBody.AppendLine(m);
                sbBody.AppendLine();
            }

            sbBody.AppendLine("}");
            var body = sbBody.ToString();

            // 清理 body 中可能残留的 auto-generated 文件头（从被误拷贝的 leading-trivia）
            body = RemoveInnerAutoGeneratedBlocks(body);

            // 确保只保留一个 #nullable enable（移除重复）
            body = Regex.Replace(body, @"(#nullable\s+enable\s*\r?\n)+", "#nullable enable\n");

            var hash = ComputeSha256Hash(body);

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated> 此文件由 xCodeGen.Cli 自动合成 </auto-generated>");
            sb.AppendLine($"// @[xCodeGen.Hash: {hash}]");
            sb.AppendLine(body);

            return sb.ToString();
        }

        private static string RemoveInnerAutoGeneratedBlocks(string input)
        {
            // 删除从 "// <auto-generated>" 到 "// </auto-generated>" 的所有块（包含两端），防止把原类的头部注释嵌入接口体
            var pattern = @"//\s*<auto-generated>[\s\S]*?//\s*</auto-generated>\s*";
            return Regex.Replace(input, pattern, "", RegexOptions.IgnoreCase);
        }

        private static string ComputeSha256Hash(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(raw);
            var hashBytes = sha.ComputeHash(bytes);
            var sb = new StringBuilder();
            foreach (var b in hashBytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private HashSet<string> CollectUsings(params string[] texts)
        {
            var set = new HashSet<string>();
            foreach (var t in texts)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                var tree = CSharpSyntaxTree.ParseText(t);
                var root = tree.GetCompilationUnitRoot();
                foreach (var u in root.Usings) set.Add(u.ToFullString().Trim());
            }
            return set;
        }

        private bool IsDtoRelatedMethod(MethodDeclarationSyntax m)
        {
            var type = m.ReturnType.ToString();
            if (type.Contains("Dto") || type.Contains("PageResult") || type.Contains("IQueryable")) return true;
            if (type == "Task<bool>" || type == "Task<long>") return true;
            if (type.StartsWith("Task<") && (type.Contains("Dto") || type.Contains("PageResult"))) return true;
            return false;
        }
    }
}