using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Web.Attributes
{
    public enum ContentTypeSupportedType
    {
        [Display(Name = "仅支持Json")]
        JsonOnly = 0,
        [Display(Name = "仅支持Xml")]
        XmlOnly = 1,
        [Display(Name = "同时支持Json和Xml")]
        JsonAndXml = 2,
    }
}
