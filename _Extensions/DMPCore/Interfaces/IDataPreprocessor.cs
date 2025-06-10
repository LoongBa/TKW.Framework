namespace TKWF.DMP.Core.Interfaces;

/// <summary>
/// 数据预处理器接口，用于在计算指标前对数据进行转换或增强
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public interface IDataPreprocessor<T> where T : class
{
    /// <summary>
    /// 处理数据集合
    /// </summary>
    /// <param name="data">输入数据</param>
    /// <returns>处理后的数据</returns>
    IEnumerable<T> Process(IEnumerable<T> data);
}