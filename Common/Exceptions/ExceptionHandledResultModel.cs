using System;
using System.Text.Json.Serialization;

namespace TKW.Framework.Exceptions
{
    /// <summary>
    /// 异常处理结果模型
    /// </summary>
    public class ExceptionHandledResultModel
    {
        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public ExceptionHandledResultModel(ExceptionHandled exceptionHandled, string customErrorViewName = null, string redirect2Url = null)
        {
            ExceptionHandled = exceptionHandled ?? throw new ArgumentNullException(nameof(exceptionHandled));
            if (!string.IsNullOrEmpty(customErrorViewName))
                CustomErrorViewName = customErrorViewName;
            Redirect2Url = string.IsNullOrEmpty(redirect2Url) ? string.Empty : redirect2Url;
        }
        public ExceptionHandledResultModel(Exception e, string customErrorViewName = null, string redirect2Url = null)
            : this(new ExceptionHandled(e), customErrorViewName, redirect2Url)
        {
        }

        [JsonIgnore]
        public string CustomErrorViewName { get; set; }
        [JsonIgnore]
        public bool IsRedirect2Url { get; set; }
        [JsonIgnore]
        public string Redirect2Url { get; set; }
        public ExceptionHandled ExceptionHandled { get; }
    }
}