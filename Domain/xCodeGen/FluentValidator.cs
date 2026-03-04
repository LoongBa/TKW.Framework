using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using TKW.Framework.Common.Exceptions;
using TKW.Framework.Domain.Interfaces;
using xCodeGen.Abstractions;
using xCodeGen.Abstractions.Metadata;

namespace TKW.Framework.Domain.xCodeGen;

/// <summary>
/// 统一的流式验证器，支持 DTO 和 Model 场景，集成了策略引擎与元数据驱动逻辑
/// </summary>
/// <typeparam name="T">目标对象类型</typeparam>
public class FluentValidator<T>(
    T target,
    EnumSceneFlags scene,
    IReadOnlyDictionary<string, PropertyMetadata> meta,
    ValidationModeEnum checkType)
where T : ISupportPersistenceState
{
    private readonly List<ValidationResult> _results = [];

    #region 基础校验入口

    /// <summary>
    /// 核心校验判定（高性能版本：由模板代码调用，避免运行时 Lambda 编译）
    /// </summary>
    public FluentValidator<T> Check<TValue>(
        string propName,
        Func<T, TValue> getter,
        Func<TValue, bool> predicate,
        string errorMessage)
    {
        meta.TryGetValue(propName, out var pMeta);

        // 调用决策引擎判定当前字段在当前模式下是否应被验证
        if (!CodeGenPolicy.CanProcess(pMeta, scene, target.IsFromPersistentSource, checkType))
            return this;

        if (!predicate(getter(target)))
        {
            _results.Add(new ValidationResult(errorMessage, [propName]));
        }
        return this;
    }

    /// <summary>
    /// 核心校验判定（易用性版本：支持开发者在 Logic 文件中通过表达式调用）
    /// </summary>
    public FluentValidator<T> Check<TValue>(
        Expression<Func<T, TValue>> expression,
        Func<TValue, bool> predicate,
        string errorMessage)
    {
        var propName = GetPropertyName(expression);
        var getter = expression.Compile();
        return Check(propName, getter, predicate, errorMessage);
    }

    #endregion

    #region 常用规则重载

    public FluentValidator<T> Required<TValue>(string propName, Func<T, TValue> getter)
    {
        return Check(propName, getter, val =>
        {
            if (val == null) return false;
            if (val is string s) return !string.IsNullOrWhiteSpace(s);
            return true;
        }, $"{GetDisplayName(propName)} 不能为空");
    }

    public FluentValidator<T> Required<TValue>(Expression<Func<T, TValue>> expression)
        => Required(GetPropertyName(expression), expression.Compile());

    public FluentValidator<T> MaxLength(string propName, int max, Func<T, string> getter)
    {
        return Check(propName, getter, s => (s?.Length ?? 0) <= max,
            $"{GetDisplayName(propName)} 长度不能超过 {max}");
    }

    public FluentValidator<T> MaxLength(Expression<Func<T, string>> expression, int max)
        => MaxLength(GetPropertyName(expression), max, expression.Compile());

    #endregion

    #region 辅助与结果处理

    private string GetDisplayName(string propName)
    {
        if (meta.TryGetValue(propName, out var p))
        {
            var dtoAttr = p.Attributes.FirstOrDefault(a => a.TypeFullName.Contains("DtoFieldAttribute"));
            var dn = CodeGenPolicy.GetStringProp(dtoAttr, "DisplayName");
            if (!string.IsNullOrEmpty(dn)) return dn;
            if (!string.IsNullOrEmpty(p.Summary)) return p.Summary;
        }
        return propName;
    }

    private static string GetPropertyName<TValue>(Expression<Func<T, TValue>> expression)
    {
        if (expression.Body is MemberExpression member) return member.Member.Name;
        if (expression.Body is UnaryExpression { Operand: MemberExpression m }) return m.Member.Name;
        throw new ArgumentException("无法解析属性表达式", nameof(expression));
    }

    public List<ValidationResult> GetResults() => _results;

    public void ThrowIfInvalid()
    {
        if (_results.Any()) throw new ValidationResultsException(_results);
    }

    #endregion
}