using System.IO;
using System.Linq;
using Castle.DynamicProxy;
using TKW.Framework.Domain.Interception;

namespace DomainTest.Services
{
    public class FirstLoggerInterceptor : BaseInterceptor
    {
        private readonly TextWriter _Output;

        public FirstLoggerInterceptor(TextWriter output)
        {
            _Output = output;
        }

        protected override void Initial(IInvocation invocation) { }

        protected override void PreProceed(IInvocation invocation)
        {
            _Output.WriteLine("方法名：{0} 参数：{1}... ",
                invocation.Method.Name,
                string.Join(", ", invocation.Arguments.Select(a => (a ?? "").ToString()).ToArray()));
        }
        protected override void PostProceed(IInvocation invocation)
        {
            _Output.WriteLine("完成，结果为 {0}", invocation.ReturnValue);
        }

        protected override void OnException(InterceptorExceptionContext context) { }
    }
}