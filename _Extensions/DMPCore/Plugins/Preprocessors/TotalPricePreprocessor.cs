using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Plugins.Preprocessors;

/// <summary>
/// 计算总价的预处理器
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class TotalPricePreprocessor<T>(
    Func<T, decimal> priceSelector,
    Func<T, int> quantitySelector,
    Action<T, decimal> totalPriceSetter)
    : IDataPreprocessor<T>
    where T : class
{
    // 从配置创建处理器
    public static TotalPricePreprocessor<T> CreateFromConfig(MetricConfig config)
    {
        var priceProperty = config.PropertyMap.GetValueOrDefault("PriceField", "Price");
        var quantityProperty = config.PropertyMap.GetValueOrDefault("QuantityField", "Quantity");
        var totalPriceProperty = config.PropertyMap.GetValueOrDefault("TotalPriceField", "TotalPrice");

        return new TotalPricePreprocessor<T>(
            PropertyAccessorFactory.Create<T, decimal>(priceProperty),
            PropertyAccessorFactory.Create<T, int>(quantityProperty),
            PropertyAccessorFactory.CreateSetter<T, decimal>(totalPriceProperty));
    }

    public IEnumerable<T> Process(IEnumerable<T> data)
    {
        foreach (var item in data)
        {
            var totalPrice = priceSelector(item) * quantitySelector(item);
            totalPriceSetter(item, totalPrice);
            yield return item;
        }
    }
}