namespace TKW.Framework.Common.Abstractions;

/// <summary>
/// 标识对象支持从源对象拷贝值（由 Source Generator 实现）
/// </summary>
public interface ICopyValuesFrom<in TSource>
    where TSource : class
{
    void CopyValuesFrom(TSource source);
}

public interface ICopyValuesToNew<out TTarget>
    where TTarget : class, new()
{
    TTarget CopyToNew();
}