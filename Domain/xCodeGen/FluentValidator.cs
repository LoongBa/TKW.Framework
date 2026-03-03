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
/// 统一的流式验证器，支持 DTO (checkType=1) 和 Model (checkType=2) 场景
/// </summary>
/// <typeparam name="T">验证目标类型（Model 或 DTO）</typeparam>
public class FluentValidator<T>(
    T target,
    EnumSceneFlags scene,
    IReadOnlyDictionary<string, PropertyMetadata> meta,
    ValidationModeEnum checkType)
where T : IIsFromPersistentSource
{
    private readonly List<ValidationResult> _results = [];

    #region 核心验证逻辑

    /// <summary>
    /// 基础校验逻辑（高性能版本：建议由模板生成代码使用，避免运行时编译 Lambda）
    /// </summary>
    public FluentValidator<T> Check<TValue>(
        string propName,
        Func<T, TValue> getter,
        Func<TValue, bool> predicate,
        string errorMessage)
    {
        // 判定是否需要执行校验：1 为 DTO 验证，2 为 Model 验证
        if (!CodeGenPolicy.CanProcess(meta, propName, scene, target.IsFromPersistentSource, checkType))
            return this;

        if (!predicate(getter(target)))
        {
            _results.Add(new ValidationResult(errorMessage, [propName]));
        }
        return this;
    }

    /// <summary>
    /// 基础校验逻辑（表达式版本：建议由开发者在 .Logic.cs 手写扩展时使用，自动提取属性名）
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

    #region 常用校验扩展 (支持高性能与表达式双重载)

    /// <summary> 必填校验 (由模板使用) </summary>
    public FluentValidator<T> Required<TValue>(string propName, Func<T, TValue> getter)
    {
        return Check(propName, getter, val =>
        {
            if (val == null) return false;
            if (val is string s) return !string.IsNullOrWhiteSpace(s);
            return true;
        }, $"{GetDisplayName(propName)} 不能为空");
    }

    /// <summary> 必填校验 (由开发者手写使用) </summary>
    public FluentValidator<T> Required<TValue>(Expression<Func<T, TValue>> expression)
    {
        return Required(GetPropertyName(expression), expression.Compile());
    }

    /// <summary> 最大长度校验 (由模板使用) </summary>
    public FluentValidator<T> MaxLength(string propName, int max, Func<T, string> getter)
    {
        return Check(propName, getter, s => (s?.Length ?? 0) <= max,
            $"{GetDisplayName(propName)} 长度不能超过 {max}");
    }

    /// <summary> 最大长度校验 (由开发者手写使用) </summary>
    public FluentValidator<T> MaxLength(Expression<Func<T, string>> expression, int max)
    {
        return MaxLength(GetPropertyName(expression), max, expression.Compile());
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 从表达式中解析属性名称，避免手动传入 nameof()
    /// </summary>
    private static string GetPropertyName<TValue>(Expression<Func<T, TValue>> expression)
    {
        if (expression.Body is MemberExpression member) return member.Member.Name;
        if (expression.Body is UnaryExpression { Operand: MemberExpression m }) return m.Member.Name;
        throw new ArgumentException("无法从表达式中提取属性名", nameof(expression));
    }

    /// <summary>
    /// 获取友好的显示名称：优先取 DtoFieldAttribute.DisplayName，其次取元数据的 Summary
    /// </summary>
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

    /// <summary> 获取所有验证结果 </summary>
    public List<ValidationResult> GetResults() => _results;

    /// <summary>
    /// 若存在验证错误，则抛出统一的 ValidationResultsException
    /// </summary>
    public void ThrowIfInvalid()
    {
        if (_results.Any()) throw new ValidationResultsException(_results);
    }

    #endregion
}