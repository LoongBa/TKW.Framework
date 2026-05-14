# DomainMapFrom 高性能映射生成器使用指南

## 1. 核心简介

`DomainMapFrom` 是领域框架内置的 **编译时（Source Generator）代码生成器**。它通过在编译阶段生成纯静态的属性赋值代码，实现了 **零反射、零 JIT 开销、完美兼容 Native AOT** 的极速对象映射（Mapping）。

底层扩展方法 `CopyValuesFrom` 、`CopyToNew`、`Clone` 会自动探测生成代码，若未生成则平滑降级为表达式树或反射执行。

## 2. 基础使用：DTO 映射到实体

只需两步，即可享受极致性能：

1. 给目标类加上 `[DomainMapFrom(typeof(源类型))]` 特性。

2. 确保目标类声明了 **`partial`** 关键字。

```csharp
using TKW.Framework.Attributes;

// 1. 加上特性并声明 partial
[DomainMapFrom(typeof(MerchantCreateDto))]
public partial class MerchantInfo
{
    public string Name { get; set; }
    public string CreditCode { get; set; }
}

// 2. 业务代码中直接调用（无需改变任何习惯）
var dto = new MerchantCreateDto { Name = "测试商户" };

// 创建新对象
var merchant = dto.CopyToNew<MerchantInfo>(); 

// 覆盖现有对象
merchant.CopyValuesFrom(dto);
```

## 3. 高阶用法

### A. 一对多映射

一个实体通常对应多个 DTO（如创建、更新），你可以在特性中传入多个类型：

```csharp
[DomainMapFrom(typeof(MerchantCreateDto), typeof(MerchantUpdateDto))]
public partial class MerchantInfo
{
    // SG 会同时生成支持这两个 DTO 的映射逻辑
}
```

### B. 高性能深拷贝 (Clone)

如果不给特性传任何参数，生成器会默认生成针对类自身的拷贝逻辑，实现极速 Clone：

```csharp
[DomainMapFrom] // 不传参数
public partial class UserSnapshot
{
    public string RoleName { get; set; }
}

// 业务调用：
var clone = originalSnapshot.CopyToNew<UserSnapshot>();
```

### C. 自定义特殊映射逻辑 (手写覆盖)

如果某个 DTO 到实体的映射逻辑很特殊（如需要字段合并、脱敏等），你只需要直接手写该方法。**生成器会自动让路，不产生冲突**。

```csharp
[DomainMapFrom(typeof(SpecialDto))]
public partial class OrderInfo
{
    public string OrderNo { get; set; }

    // 手写方法：生成器检测到后会自动跳过该类型的生成
    public void CopyValuesFrom(SpecialDto source)
    {
        if (source == null) return;
        this.OrderNo = "PREFIX_" + source.RawNo; // 特殊逻辑
    }
}
```

## 4. 常见问题排查 (FAQ)

**Q: 为什么 IDE 里类名下面出现了 `TKWMAP001` 黄色警告？** A: 因为你加了 `[DomainMapFrom]` 特性，但忘记给类加上 `partial` 关键字。此时 SG 无法注入代码。虽然程序依然能运行（会自动降级为表达式树），但无法享受最高性能。加上 `partial` 警告即可消失。

**Q: 如果我不加这个特性，还能用 `CopyToNew` 吗？** A: **完全可以。** 框架支持“无感兜底”。没有特性的类会自动走动态表达式树或反射机制。建议仅在**核心业务类型、高频次调用、或者准备发布 AOT 时**，显式加上该特性以提升性能。
