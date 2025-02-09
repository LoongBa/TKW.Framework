using System;

namespace TKW.Framework.Common.Exceptions
{
    /// <summary>
    /// 处理过的异常
    /// </summary>
    public class ExceptionHandled
    {
        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        private ExceptionHandled()
        {
            CustomErrorCode = 0;
            Prompt = ExceptionTypeString = ExceptionMessage = string.Empty;
            ExceptionEnumType = null;
            InnerExceptionHandled = null;
        }

        public ExceptionHandled(int customErrorCode, Exception exception) : this(exception)
        {
            CustomErrorCode = customErrorCode;
        }
        public ExceptionHandled(Exception exception) : this()
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            ExceptionMessage = exception.Message;
            ExceptionTypeString = exception.GetType().FullName;

            var e = exception as ITKWException;
            if (e?.ErrorType != null)
            {
                IsTkwException = true;
                ExceptionEnumType = new ExceptionEnumType(e);
            }

            if (exception.InnerException != null)
                InnerExceptionHandled = new ExceptionHandled(exception.InnerException);
        }

        public int CustomErrorCode { get; set; }
        public string ExceptionMessage { get; }
        public string ExceptionTypeString { get; }
        public bool IsTkwException { get; }
        public string Prompt { get; set; }
        public ExceptionEnumType ExceptionEnumType { get; }
        public ExceptionHandled InnerExceptionHandled { get; }
    }
}