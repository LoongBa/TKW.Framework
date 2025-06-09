using TKWF.DMPCore.Interfaces;

namespace TKWF.DMPCore.Plugins.Preprocessores;
/// <summary>
/// 预处理插件示例 - 总价计算
/// </summary>
public class TotalPricePreprocessor : IPreprocessor
{
    public string Name => "TotalPricePreprocessor";

    public void Process(Dictionary<string, object> dataItem)
    {
        if (dataItem.TryGetValue("unit_price", out var unitPriceObj) &&
            dataItem.TryGetValue("quantity", out var quantityObj) &&
            unitPriceObj is decimal unitPrice &&
            quantityObj is int quantity)
            dataItem["total_price"] = unitPrice * quantity;
    }
}