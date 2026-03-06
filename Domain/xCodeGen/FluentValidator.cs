using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using TKW.Framework.Domain.Interfaces;
using xCodeGen.Abstractions;

namespace TKW.Framework.Domain.xCodeGen;

/// <summary>
/// 深度优化版流式验证器：Readonly Struct 架构，实现零堆分配验证流程
/// </summary>
public readonly struct FluentValidator<T>(
    T target,
    EnumSceneFlags scene,
    ValidationModeEnum mode,
    List<ValidationResult> results)
where T : ISupportPersistenceState
{
    /// <summary>
    /// 核心校验判定（高性能版本：由模板代码调用）
    /// </summary>
    public FluentValidator<T> Check<TValue>(
        string propName,
        Func<T, TValue> getter,
        Func<TValue, bool> predicate,
        string errorMessage)
    {
        // 直接从静态泛型缓存获取元数据，无需外部传递字典
        ValidationCache<T>.Meta.TryGetValue(propName, out var pMeta);

        // 调用决策引擎判定 (V1.31)
        if (!CodeGenPolicy.CanProcess(pMeta, scene, target.IsFromPersistentSource, mode))
            return this;

        if (!predicate(getter(target)))
        {
            results.Add(new ValidationResult(errorMessage, new[] { propName }));
        }
        return this;
    }

    /// <summary>
    /// 核心校验判定（易用性版本：支持手写代码调用，自动缓存编译结果）
    /// </summary>
    public FluentValidator<T> Check<TValue>(
        Expression<Func<T, TValue>> expression,
        Func<TValue, bool> predicate,
        string errorMessage)
    {
        var propName = GetPropertyName(expression);
        var getter = ValidationCache<T>.GetOrCompile(expression); // 自动获取缓存的 Func
        return Check(propName, getter, predicate, errorMessage);
    }

    #region 常用规则 DSL

    public FluentValidator<T> Required<TValue>(string propName, Func<T, TValue> getter)
    {
        return Check(propName, getter, val =>
        {
            if (val == null) return false;
            if (val is string s) return !string.IsNullOrWhiteSpace(s);
            return true;
        }, $"{GetDisplayName(propName)} 不能为空");
    }

    public FluentValidator<T> MaxLength(string propName, int max, Func<T, string> getter)
    {
        return Check(propName, getter, s => (s?.Length ?? 0) <= max,
            $"{GetDisplayName(propName)} 长度不能超过 {max}");
    }

    #endregion

    #region 辅助处理

    private string GetDisplayName(string propName)
    {
        if (ValidationCache<T>.Meta.TryGetValue(propName, out var p))
        {
            var dtoAttr = p.Attributes.FirstOrDefault(a => a.TypeFullName.Contains("DtoFieldAttribute"));
            var dn = CodeGenPolicy.GetStringProp(dtoAttr, "DisplayName");
            if (!string.IsNullOrEmpty(dn)) return dn;
            return !string.IsNullOrEmpty(p.Summary) ? p.Summary : propName;
        }
        return propName;
    }

    private static string GetPropertyName<TValue>(Expression<Func<T, TValue>> expression)
    {
        if (expression.Body is MemberExpression member) return member.Member.Name;
        if (expression.Body is UnaryExpression { Operand: MemberExpression m }) return m.Member.Name;
        throw new ArgumentException("无法解析属性表达式");
    }

    #endregion
}