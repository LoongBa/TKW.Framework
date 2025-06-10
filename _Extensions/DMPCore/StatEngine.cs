using System.Diagnostics;
using System.Reflection;
using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core;

/// <summary>
/// 统计引擎（支持多计算器并行处理、事件系统、进度跟踪与错误处理）
/// </summary>
public class StatEngine : IStatEngine
{
    #region 事件定义
    /// <summary>处理开始事件</summary>
    public event EventHandler<ProcessStartEventArgs>? ProcessStarted;
    /// <summary>处理完成事件</summary>
    public event EventHandler<ProcessCompleteEventArgs>? ProcessCompleted;
    /// <summary>进度更新事件</summary>
    public event EventHandler<ProgressEventArgs>? ProgressUpdated;
    /// <summary>错误发生事件</summary>
    public event EventHandler<ErrorEventArgs>? ErrorOccurred;
    #endregion

    #region 私有字段
    private readonly Dictionary<Type, Dictionary<string, Type>> _componentTypes = new();
    private readonly Dictionary<(Type dataType, string name), object> _loaders = new();
    private readonly Dictionary<(Type dataType, string name), object> _preprocessors = new();
    private readonly Dictionary<(Type resultType, string name), object> _exporters = new();
    private readonly IMetricCalculatorFactory _calculatorFactory;
    private readonly EngineConfig _engineConfig;
    #endregion

    #region 构造函数
    public StatEngine(IMetricCalculatorFactory calculatorFactory, EngineConfig engineConfig)
    {
        _calculatorFactory = calculatorFactory ?? throw new ArgumentNullException(nameof(calculatorFactory));
        _engineConfig = engineConfig ?? throw new ArgumentNullException(nameof(engineConfig));

        if (_engineConfig.DynamicLoading.EnableDynamicLoading)
        {
            LoadComponentsDynamically();
        }
    }
    #endregion

    #region 组件注册方法
    public void RegisterLoader<T>(string name, IDataLoader<T> loader) where T : class, new()
    {
        var key = (typeof(T), name);
        if (!_loaders.TryAdd(key, loader))
            throw new InvalidOperationException($"加载器已存在: {name} 类型: {typeof(T).Name}");
    }

    public void RegisterPreprocessor<T>(string name, IDataPreprocessor<T> preprocessor) where T : class
    {
        var key = (typeof(T), name);
        if (!_preprocessors.TryAdd(key, preprocessor))
            throw new InvalidOperationException($"预处理器已存在: {name} 类型: {typeof(T).Name}");
    }

    public void RegisterExporter<T>(string name, IDataExporter<T> exporter) where T : class, new()
    {
        var key = (typeof(T), name);
        if (!_exporters.TryAdd(key, exporter))
            throw new InvalidOperationException($"导出器已存在: {name} 类型: {typeof(T).Name}");
    }

    public void RegisterComponentType<TInterface>(string name, Type implementationType) where TInterface : class
    {
        var interfaceType = typeof(TInterface);
        if (!interfaceType.IsAssignableFrom(implementationType))
            throw new ArgumentException($"类型 {implementationType.Name} 未实现接口 {interfaceType.Name}");

        if (!_componentTypes.TryGetValue(interfaceType, out var components))
        {
            components = new Dictionary<string, Type>();
            _componentTypes[interfaceType] = components;
        }

        if (!components.TryAdd(name, implementationType))
            throw new InvalidOperationException($"类型已注册: {name} 接口: {interfaceType.Name}");
    }
    #endregion

    #region 类型查询方法
    public Type? GetRegisteredType<TInterface>(string name) where TInterface : class
    {
        var interfaceType = typeof(TInterface);
        if (_componentTypes.TryGetValue(interfaceType, out var components) &&
            components.TryGetValue(name, out var type))
            return type;
        return null;
    }
    #endregion

    #region 核心执行流程（支持多计算器）
    public TResult Execute<T, TResult>(
        string loaderName,
        List<string> calculatorTypes,
        string exporterName)
        where T : class, new()
        where TResult : class, new()
    {
        // 触发处理开始事件
        var processId = Guid.NewGuid().ToString();
        OnProcessStarted("ETL", processId, new { Loader = loaderName, Calculators = calculatorTypes, Exporter = exporterName });

        var stopwatch = Stopwatch.StartNew();
        Dictionary<string, object> results = new();

        try
        {
            // 1. 加载数据
            var data = LoadData<T>(loaderName);
            var enumerable = data as T[] ?? data.ToArray();
            OnProgressUpdated("Loading", enumerable.Length, 0, 0, 0);

            // 2. 预处理数据
            var processedData = ApplyPreprocessors(enumerable);

            // 3. 执行多计算器并行计算
            results = ExecuteCalculators(processedData, calculatorTypes);

            // 4. 导出结果
            if (!string.IsNullOrEmpty(exporterName))
            {
                ExportResult(results, exporterName);
            }

            stopwatch.Stop();
            OnProcessCompleted("ETL", processId, results, stopwatch.Elapsed);

            return results as TResult ?? throw new InvalidCastException($"结果类型不匹配: 期望 {typeof(TResult).Name}");
        }
        catch (Exception ex)
        {
            if (!OnErrorOccurred("ETL", "StatEngine", ex))
                throw; // 不跳过则抛出异常

            stopwatch.Stop();
            OnProcessCompleted("ETL", processId, results, stopwatch.Elapsed);
            return null!;
        }
    }
    #endregion

    #region 动态加载逻辑
    private void LoadComponentsDynamically()
    {
        if (_engineConfig.DynamicLoading.ScanAssemblies)
            LoadComponentsByAssemblyScanning();

        LoadComponentsByExplicitMapping();
    }

    private void LoadComponentsByAssemblyScanning()
    {
        foreach (var path in _engineConfig.DynamicLoading.AssemblyPaths)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"程序集不存在: {path}");

            try
            {
                var assembly = Assembly.LoadFrom(path);
                RegisterComponentsFromAssembly(assembly);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载程序集 {path} 失败", ex);
            }
        }
    }

    private void RegisterComponentsFromAssembly(Assembly assembly)
    {
        RegisterComponents<IDataLoader<object>, IDataLoader<object>>("Loader", assembly);
        RegisterComponents<IDataPreprocessor<object>, IDataPreprocessor<object>>("Preprocessor", assembly);
        RegisterComponents<IDataExporter<object>, IDataExporter<object>>("Exporter", assembly);
    }

    private void RegisterComponents<TInterface, TGeneric>(string suffix, Assembly assembly)
        where TInterface : class
        where TGeneric : class
    {
        foreach (var type in assembly.GetExportedTypes()
                     .Where(t => !t.IsAbstract && ImplementsGenericInterface(t, typeof(TGeneric))))
        {
            var componentName = type.Name.Replace(suffix, "").ToLowerInvariant();
            RegisterComponentType<TInterface>(componentName, type);
        }
    }

    private void LoadComponentsByExplicitMapping()
    {
        foreach (var (name, typeName) in _engineConfig.DynamicLoading.ComponentNameToTypeMap)
        {
            var type = Type.GetType(typeName);
            if (type == null)
                throw new TypeLoadException($"未找到类型: {typeName}");

            if (ImplementsGenericInterface(type, typeof(IDataLoader<>)))
                RegisterComponentType<IDataLoader<object>>(name, type);
            else if (ImplementsGenericInterface(type, typeof(IDataPreprocessor<>)))
                RegisterComponentType<IDataPreprocessor<object>>(name, type);
            else if (ImplementsGenericInterface(type, typeof(IDataExporter<>)))
                RegisterComponentType<IDataExporter<object>>(name, type);
            else
                throw new NotSupportedException($"类型 {typeName} 未实现支持的接口");
        }
    }

    private bool ImplementsGenericInterface(Type type, Type genericInterface)
    {
        return type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterface);
    }
    #endregion

    #region 数据处理方法
    private IEnumerable<T> LoadData<T>(string loaderName) where T : class, new()
    {
        var explicitKey = (typeof(T), loaderName);
        if (_loaders.TryGetValue(explicitKey, out var loaderObj) && loaderObj is IDataLoader<T> loader)
            return loader.Load(_engineConfig.DataLoading);

        var type = GetRegisteredType<IDataLoader<T>>(loaderName);
        if (type == null)
            throw new NotSupportedException($"未找到加载器: {loaderName}");

        return ((IDataLoader<T>)Activator.CreateInstance(type)!).Load(_engineConfig.DataLoading);
    }

    private IEnumerable<T> ApplyPreprocessors<T>(IEnumerable<T> data) where T : class
    {
        var enumerable = data.ToList();
        var total = enumerable.Count;
        long processed = 0, success = 0, failed = 0;
        var result = new List<T>();

        foreach (var item in enumerable)
        {
            try
            {
                var processedItem = item;

                // 遍历预处理器名称时创建本地副本
                foreach (var ppName in _engineConfig.Processing.PreprocessorNames.ToList())
                {
                    var preprocessorName = ppName; // 创建本地副本
                    try
                    {
                        processedItem = ProcessWithPreprocessor(preprocessorName, processedItem);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        if (!OnErrorOccurred("Preprocessing", preprocessorName, ex))
                            throw;
                    }
                }

                result.Add(processedItem);
                Interlocked.Increment(ref success);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                if (!OnErrorOccurred("Preprocessing", "ItemProcessing", ex))
                    throw;
            }
            finally
            {
                if (Interlocked.Increment(ref processed) % 100 == 0)
                {
                    OnProgressUpdated("Preprocessing", total, processed, success, failed);
                }
            }
        }

        OnProgressUpdated("Preprocessing", total, processed, success, failed);
        return result;
    }

    private T ProcessWithPreprocessor<T>(string preprocessorName, T item) where T : class
    {
        var explicitKey = (typeof(T), preprocessorName);
        if (_preprocessors.TryGetValue(explicitKey, out var preprocessorObj) && preprocessorObj is IDataPreprocessor<T> preprocessor)
            return preprocessor.Process([item]).First();

        var type = GetRegisteredType<IDataPreprocessor<T>>(preprocessorName);
        if (type == null)
            throw new NotSupportedException($"未找到预处理器: {preprocessorName}");

        return ((IDataPreprocessor<T>)Activator.CreateInstance(type)!).Process([item]).First();
    }

    private Dictionary<string, object> ExecuteCalculators<T>(IEnumerable<T> data, List<string> calculatorTypes) where T : class
    {
        var calculators = new List<IMetricCalculator<T>>();
        foreach (var calcType in calculatorTypes)
        {
            var calculator = _calculatorFactory.Create<T>(calcType);
            calculators.Add(calculator);
        }

        var results = new Dictionary<string, object>();
        var tasks = calculators.Select(calc => Task.Run(() =>
        {
            try
            {
                var calcResult = calc.Calculate(data);
                var calcName = GetCalculatorName(calc);
                results[calcName] = calcResult;
            }
            catch (Exception ex)
            {
                OnErrorOccurred("Calculation", GetCalculatorName(calc), ex);
            }
        })).ToList();

        Task.WaitAll(tasks.ToArray());
        return results;
    }

    private string GetCalculatorName<T>(IMetricCalculator<T> calculator) where T : class
    {
        var attr = calculator.GetType().GetCustomAttribute<CalculatorNameAttribute>();
        return attr?.Name ?? calculator.GetType().Name.Replace("Calculator", "").ToLower();
    }

    private void ExportResult<T>(T result, string exporterName) where T : class, new()
    {
        var explicitKey = (typeof(T), exporterName);
        if (_exporters.TryGetValue(explicitKey, out var exporterObj) && exporterObj is IDataExporter<T> exporter)
        {
            exporter.Export(result, _engineConfig.Export);
            return;
        }

        var type = GetRegisteredType<IDataExporter<T>>(exporterName);
        if (type == null)
            throw new NotSupportedException($"未找到导出器: {exporterName}");

        ((IDataExporter<T>)Activator.CreateInstance(type)!).Export(result, _engineConfig.Export);
    }
    #endregion

    #region 事件触发方法
    protected virtual void OnProcessStarted(string stage, string processId, object parameters)
    {
        ProcessStarted?.Invoke(this, new ProcessStartEventArgs
        {
            Stage = stage,
            ProcessId = processId,
            Parameters = parameters,
            Timestamp = DateTime.UtcNow
        });
    }

    protected virtual void OnProcessCompleted(string stage, string processId, Dictionary<string, object> results, TimeSpan duration)
    {
        ProcessCompleted?.Invoke(this, new ProcessCompleteEventArgs
        {
            Stage = stage,
            ProcessId = processId,
            Results = results,
            Duration = duration,
            Timestamp = DateTime.UtcNow
        });
    }

    protected virtual void OnProgressUpdated(string stage, long total, long processed, long success, long failed)
    {
        ProgressUpdated?.Invoke(this, new ProgressEventArgs
        {
            Stage = stage,
            TotalItems = total,
            ProcessedItems = processed,
            SuccessfulItems = success,
            FailedItems = failed,
            Timestamp = DateTime.UtcNow
        });
    }

    protected virtual bool OnErrorOccurred(string stage, string component, Exception ex)
    {
        var args = new ErrorEventArgs
        {
            Stage = stage,
            Component = component,
            Exception = ex,
            Timestamp = DateTime.UtcNow
        };
        ErrorOccurred?.Invoke(this, args);
        return args.SkipAndContinue;
    }
    #endregion

    #region 辅助方法
    public ProcessingConfig GetProcessingConfig() => _engineConfig.Processing;
    #endregion
}