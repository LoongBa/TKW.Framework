#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TKW.Framework.Tools.Tags;

/// <summary>
/// 标签服务 (提供给业务层的统一门面)
/// </summary>
public class TagService(TagExtractionPipeline pipeline)
{
    private List<TagRule> _Rules = [];

    // 注入底层流水线

    /// <summary>
    /// 初始化/更新规则
    /// </summary>
    public void LoadRules(IEnumerable<TagRule>? rules)
    {
        _Rules = rules?.Where(r => r.IsEnabled).ToList() ?? [];
    }

    /// <summary>
    /// 获取文本的标签结果
    /// </summary>
    /// <param name="text">输入文本</param>
    public IReadOnlyList<TagHit> GetTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || _Rules.Count == 0)
            return [];

        // 将文本和内部持有的规则送入流水线处理
        return pipeline.Extract(text, _Rules);
    }
}