using System.Text;

namespace TKW.Framework.Common.Tools.BatchId;

/// <summary>
/// 批次ID生成器工厂实现
/// </summary>
public class BatchIdGeneratorFactory : IBatchIdGeneratorFactory
{
    public BatchIdGenerator Create(string prefix = "") => new(prefix);
}