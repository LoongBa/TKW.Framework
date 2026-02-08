namespace TKW.Framework.Domain.Interception.Filters;

public enum EnumDomainLogLevel
{
    None = 0,       // 完全关闭
    Minimal = 1,    // 只记录异常 + 慢调用（> 500ms）
    Normal = 2,     // 方法进入/退出 + 耗时
    Verbose = 3     // + 参数值（脱敏后） + 返回值摘要
}