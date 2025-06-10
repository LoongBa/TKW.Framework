using TKWF.DMP.Core.Interfaces;

namespace TKWF.DMP.Core.Plugins.Calculators
{
    // 配置类

    // 

    // 属性访问器工厂（表达式树缓存）

    // 复购率计算器实现
    public class RepurchaseRateCalculator<T> : IMetricCalculator<T>
        where T : class
    {
        private readonly Func<T, string> _UserIdSelector;

        public RepurchaseRateCalculator(string metricName, Func<T, string> userIdSelector)
        {
            Name = metricName ?? throw new ArgumentNullException(nameof(metricName));
            _UserIdSelector = userIdSelector ?? throw new ArgumentNullException(nameof(userIdSelector));
        }

        public RepurchaseRateCalculator(string metricName, string userIdPropertyName)
        {
            Name = metricName ?? throw new ArgumentNullException(nameof(metricName));

            if (string.IsNullOrEmpty(userIdPropertyName))
                throw new ArgumentNullException(nameof(userIdPropertyName));

            _UserIdSelector = PropertyAccessorFactory.Create<T, string>(userIdPropertyName);
        }

        public string Name { get; }

        public bool IsThreadSafe => true;

        public Dictionary<string, object> Calculate(IEnumerable<T> groupedData)
        {
            var enumerable = groupedData.ToList();
            if (enumerable.Count == 0)
                return new Dictionary<string, object> { { "value", 0 } };

            var userOrders = enumerable
                .GroupBy(_UserIdSelector)
                .Where(g => g.Count() > 1)
                .ToList();

            var totalUsers = enumerable.Select(_UserIdSelector).Distinct().Count();
            var repurchaseUsers = userOrders.Count;
            var rate = totalUsers == 0 ? 0 : (double)repurchaseUsers / totalUsers;

            return new Dictionary<string, object> { { "value", rate } };
        }
    }
}