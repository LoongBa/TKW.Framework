using TKWF.DMPCore.Interfaces;
using TKWF.DMPCore.Models;

namespace TKWF.DMPCore;

public class StatEngine : IStatEngine
{
    private readonly IConfigManager _configManager;
    private readonly ITimeProcessor _timeProcessor;
    private readonly IPluginLoader<IDataLoader> _dataLoaderLoader;
    private readonly IPluginLoader<IPreprocessor> _preprocessorLoader;
    private readonly IPluginLoader<IMetricCalculator> _metricCalculatorLoader;
    private readonly IPluginLoader<IResultExporter> _resultExporterLoader;

    public StatEngine(
        IConfigManager configManager,
        ITimeProcessor timeProcessor,
        IPluginLoader<IDataLoader> dataLoaderLoader,
        IPluginLoader<IPreprocessor> preprocessorLoader,
        IPluginLoader<IMetricCalculator> metricCalculatorLoader,
        IPluginLoader<IResultExporter> resultExporterLoader)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<FrozenMetricResult>> ExecuteAsync()
    {
        throw new NotImplementedException();
    }
}