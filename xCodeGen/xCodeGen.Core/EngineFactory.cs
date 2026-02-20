using System;
using System.Collections.Generic;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Templates;
using xCodeGen.Core.Configuration;
using xCodeGen.Core.IO;
using xCodeGen.Core.Metadata;
using xCodeGen.Core.Services;

namespace xCodeGen.Core;

public static class EngineFactory
{
    public static Engine Create(
        CodeGenConfig config,
        IEnumerable<IMetaDataExtractor> extractors,
        ITemplateEngine templateEngine,
        IFileWriter fileWriter)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        // 1. 初始化命名服务
        var namingService = new NamingService(config.NamingRules);

        // 2. 初始化元数据转换器
        var converter = new MetadataConverter(namingService);

        // 3. 修复：正确初始化增量检查器，传入 IFileWriter 实例
        var incrementalChecker = new IncrementalChecker(fileWriter);

        // 4. 构建并返回引擎
        return new Engine(
            extractors,
            converter,
            templateEngine,
            fileWriter,
            namingService,
            incrementalChecker
        );
    }
}