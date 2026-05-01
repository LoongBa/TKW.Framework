using System;
using xCodeGen.Abstractions.Templates;
using xCodeGen.Core.Configuration;
using xCodeGen.Core.IO;

namespace xCodeGen.Core;

public static class EngineFactory
{
    public static Engine Create(
        CodeGenConfig config,
        ITemplateEngine templateEngine,
        IFileWriter fileWriter)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        var incrementalChecker = new IncrementalChecker(fileWriter);
        return new Engine(templateEngine, fileWriter, incrementalChecker);
    }
}