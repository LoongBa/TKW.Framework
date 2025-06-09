using TKWF.DMP.Core.Interfaces;

namespace TKWF.DMP.Core.Plugins.Calculatores;

public class RepurchaseRateCalculator : IMetricCalculator
{
    public string Name => "RepurchaseRateCalculator";
    public bool IsThreadSafe => true;

    public Dictionary<string, object> Calculate(IEnumerable<Dictionary<string, object>> groupedData)
    {
        if (!groupedData.Any())
            return new Dictionary<string, object> { { "repurchase_rate", 0 } };

        var userOrders = groupedData
            .GroupBy(d => d["user_id"].ToString())
            .Where(g => g.Count() > 1)
            .ToList();

        var totalUsers = groupedData.Select(d => d["user_id"]).Distinct().Count();
        var repurchaseUsers = userOrders.Count;
        var rate = totalUsers == 0 ? 0 : (double)repurchaseUsers / totalUsers;

        return new Dictionary<string, object> { { "repurchase_rate", rate } };
    }
}