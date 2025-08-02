using System;
using System.Collections.Generic;
using System.IO;

namespace xCodeGen.Abstractions.Metadata
{
    /// <summary>
    /// 基于接口的扩展方法（解决预定义问题）
    /// </summary>
    public static class ProjectMetaContextExtensions
    {
        /// <summary>
        /// 按基类查找元数据
        /// </summary>
        public static IEnumerable<ClassMetadata> FindByBaseType(
            this IProjectMetaContext context, string baseTypeFullName)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (baseTypeFullName == null) throw new ArgumentNullException(nameof(baseTypeFullName));

            foreach (var metadata in context.AllMetadatas)
            {
                if (string.Equals(metadata.BaseType, baseTypeFullName, StringComparison.Ordinal))
                {
                    yield return metadata;
                }
            }
        }

        /// <summary>
        /// 按实现的接口查找元数据
        /// </summary>
        public static IEnumerable<ClassMetadata> FindByImplementedInterface(
            this IProjectMetaContext context, string interfaceFullName)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (interfaceFullName == null) throw new ArgumentNullException(nameof(interfaceFullName));

            foreach (var metadata in context.AllMetadatas)
            {
                if (metadata.ImplementedInterfaces.Contains(interfaceFullName))
                {
                    yield return metadata;
                }
            }
        }
        /// <summary>
        /// 计算生成文件目录相对于项目目录的相对路径
        /// </summary>
        public static string GetRelativeGeneratedPath(this ProjectConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            return PathUtility.GetRelativePath(
                config.ProjectDirectory,
                config.GeneratedFilesDirectory
            );
        }

        /// <summary>
        /// 确保生成文件目录存在（仅在运行时调用）
        /// </summary>
        public static void EnsureGeneratedDirectoryExists(this ProjectConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!Directory.Exists(config.GeneratedFilesDirectory))
            {
                Directory.CreateDirectory(config.GeneratedFilesDirectory);
            }
        }

        /// <summary>
        /// 计算完整的生成文件路径
        /// </summary>
        public static string GetGeneratedFilePath(this ProjectConfiguration config, string fileName)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentException("文件名不能为空", nameof(fileName));

            return Path.Combine(config.GeneratedFilesDirectory, fileName);
        }
    }
}