using xCodeGen.Abstractions;

namespace xCodeGen.Core.Templates;
public abstract class TemplatePageBase<T> : RazorLight.TemplatePage<T>
{
    // 返回 HtmlString，仅为了模板内书写方便
    public Microsoft.AspNetCore.Html.HtmlString ToMd(string? text) 
        => new(MdHelper.Clean(text));

    public Microsoft.AspNetCore.Html.HtmlString ToMdQuote(string? text)
        => new(MdHelper.ToQuote(text));
}