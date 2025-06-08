using Microsoft.Extensions.Localization;

namespace TKWF.ExcelImporter;

public static class LocalizerExtensions
{
    public static string GetLocalizedErrorWithFormat(this IStringLocalizer? localizer, string key, string format, params object[] args)
    {
        var msg = localizer?[key, args]?.ToString();
        if (!string.IsNullOrEmpty(msg))
            return msg;

        if (string.IsNullOrEmpty(format))
            return localizer.GetLocalizedError(key, args);

        return string.Format(format, args);
    }
    /// <summary>
    /// 自动生成 fallback 的简洁版本
    /// </summary>
    public static string GetLocalizedError(this IStringLocalizer? localizer, string key, params object[] args)
    {
        var msg = localizer?[key, args]?.ToString();
        if (!string.IsNullOrEmpty(msg))
            return msg;

        var argPlaceholders = args.Length > 0
            ? string.Join(" ", args.Select((_, i) => $"{{{i}}}"))
            : string.Empty;
        var fallback = string.IsNullOrEmpty(argPlaceholders)
            ? key
            : $"{key}: {argPlaceholders}";

        return string.Format(fallback, args);
    }
}