namespace TKW.Framework.Common.Tools;

/// <summary>
/// 批次ID生成器工厂接口
/// </summary>
public interface IBatchIdGeneratorFactory
{
    /// <summary>
    /// 创建指定前缀的生成器
    /// </summary>
    BatchIdGenerator Create(string prefix);
}