## ExpressionMapper

# 一、核心特性说明

## 1. 缓存策略体系（灵活配置）

| 策略类型                    | 用途              | 使用示例                                                         |
| ----------------------- | --------------- | ------------------------------------------------------------ |
| `CachePolicy.Default`   | 全局默认策略（24 小时过期） | 普通业务类型                                                       |
| `CachePolicy.Permanent` | 永不过期            | 核心类型（商户 / 订单 / 用户）                                           |
| `CachePolicy.ShortTerm` | 短期策略（1 小时过期）    | 临时类型 / 第三方接口 DTO                                             |
| 自定义策略                   | 按需配置过期时间        | `new CachePolicy { ExpirationTime = TimeSpan.FromHours(8) }` |

## 2. 预缓存使用示例

```csharp
// Program.cs 系统启动时调用
public static void Main(string[] args)
{
    // 1. 全局清理配置（可选）
    ExpressionMapperExtensions.CleanupConfig.CleanupInterval = TimeSpan.FromMinutes(30);

    // 2. 预缓存核心类型（永不过期）
    ExpressionMapperExtensions.PreCacheMapper<MerchantCreateDto, MerchantInfo>(CachePolicy.Permanent);
    ExpressionMapperExtensions.PreCacheMapper<MerchantUpdateDto, MerchantInfo>(CachePolicy.Permanent);

    // 3. 预缓存临时类型（短期过期）
    ExpressionMapperExtensions.PreCacheMapper<ThirdPartyOrderDto, OrderInfo>(CachePolicy.ShortTerm);

    // 4. 批量预缓存（统一策略）
    var batchTypes = new List<(Type, Type)>
    {
        (typeof(UserCreateDto), typeof(UserInfo)),
        (typeof(UserUpdateDto), typeof(UserInfo))
    };
    ExpressionMapperExtensions.BatchPreCacheMapper(batchTypes, CachePolicy.Permanent);

    // 5. 自定义策略预缓存
    var customPolicy = new CachePolicy { ExpirationTime = TimeSpan.FromHours(8) };
    ExpressionMapperExtensions.PreCacheMapper<CustomDto, CustomInfo>(customPolicy);

    // 启动应用...
}
```

## 3. 业务代码调用示例（完全兼容原有语法）

```csharp
// 创建场景
var merchantDto = new MerchantCreateDto { Name = "测试商户", CreditCode = "91110108MA00000000" };
var merchant = merchantDto.CopyToNew<MerchantInfo>();

// 编辑场景
var updateDto = new MerchantUpdateDto { UId = "123", ContactPhone = "13800138000" };
var existingMerchant = await _freeSql.Select<MerchantInfo>().Where(m => m.UId == "123").FirstAsync();
existingMerchant.CopyValuesFrom(updateDto);
```

## 关键设计

1. **策略隔离**：不同类型对可配置不同的过期策略，核心类型永不过期，临时类型短期过期；
2. **线程安全**：使用 `ConcurrentDictionary` + `ReaderWriterLockSlim` 保证高并发下的缓存安全；
3. **资源释放**：监听应用退出事件，自动释放 Timer 和锁资源，避免内存泄漏；
4. **容错机制**：清理时加锁超时保护，异常捕获，不影响主线程运行；
5. **兼容性**：完全兼容原有 `CopyValuesFrom`/`CopyToNew` 调用方式，零成本迁移；
6. **可监控**：清理日志输出，便于运维监控缓存使用情况。

# 二、开箱即用

## 默认策略

1. **默认策略兜底**：无手动配置时，所有缓存项均使用 `CachePolicy.Default`（24 小时过期）；
2. **按需编译缓存**：无预缓存时，首次转换自动编译并缓存委托，后续直接复用；
3. **智能自动清理**：默认 1 小时清理一次，仅删除 24 小时未访问的缓存项，活跃缓存不会被清理；
4. **零配置可用**：默认行为覆盖绝大多数场景，无需任何配置即可直接使用，兼顾性能和内存安全。

简单来说，默认情况下工具类是 “傻瓜式” 可用的 —— 无需关心预缓存、策略配置等细节，直接调用 `CopyToNew`/`CopyValuesFrom` 即可，工具类会自动处理缓存的创建、更新和清理，同时保证性能和内存占用的平衡。

## 默认行为

**未调用任何预缓存方法**、**未手动指定 CachePolicy** 时，工具类会按照以下 “兜底规则” 运行，全程无需任何配置，开箱即用：

| 维度   | 默认行为                                         | 设计目的                          |
| ---- | -------------------------------------------- | ----------------------------- |
| 缓存策略 | 自动使用 `CachePolicy.Default`（过期时间 24 小时，非永不过期） | 平衡 “性能” 和 “内存占用”，适配绝大多数普通业务场景 |
| 首次转换 | 自动编译 Expression 委托 + 缓存属性信息（均绑定默认策略），无预缓存开销  | 按需编译，避免启动时无意义的资源消耗            |
| 缓存访问 | 每次访问缓存（转换对象）时，自动更新 “最后访问时间”                  | 保证活跃的缓存项不被误清理                 |
| 自动清理 | 按 `CleanupConfig` 全局配置执行（默认 1 小时清理一次）        | 定期释放超过 24 小时未访问的缓存项，防止内存膨胀    |
| 异常处理 | 清理时加锁超时保护、异常捕获，不影响主线程                        | 保证工具类的健壮性，即使清理失败也不影响业务转换      |

## 1. 缓存策略的默认值（无手动配置时）

`CachePolicy.Default` 是所有未指定策略的缓存项的兜底策略，其固定默认值为：

```csharp
// CachePolicy 类中定义的默认策略
public static readonly CachePolicy Default = new() 
{
    ExpirationTime = TimeSpan.FromHours(24), // 24小时过期
    NeverExpire = false // 非永不过期
};
```

- 无论是 “首次转换自动缓存” 还是 “预缓存时未传 policy 参数”，都会使用这个策略；
- 核心逻辑：`var actualPolicy = policy ?? CachePolicy.Default;`（所有策略相关方法的兜底）。

## 2. 首次转换的默认流程（无预缓存）

当你第一次执行 `dto.CopyToNew<MerchantInfo>()` 时，工具类的执行链路：

1. 检查 `_mapperDelegateCache` 是否有 `(MerchantCreateDto, MerchantInfo)` 的缓存项 → 无；
2. 自动编译 Expression 委托（生成 `Func<MerchantCreateDto, MerchantInfo>`）；
3. 将委托、当前时间（最后访问时间）、`CachePolicy.Default` 绑定，存入缓存容器；
4. 同时自动缓存 `MerchantCreateDto` 和 `MerchantInfo` 的属性信息（均绑定默认策略）；
5. 执行委托，完成对象转换并返回结果；
6. 后续再执行该类型对的转换时，直接从缓存取委托（无编译开销），仅更新最后访问时间。

## 3. 自动清理的默认逻辑（无预缓存）

清理线程默认 1 小时执行一次，核心判断逻辑：

```csharp
// 仅清理：非永不过期 + 最后访问时间 + 24小时 < 当前时间
.Where(kv => !kv.Value.Policy.NeverExpire && 
             kv.Value.LastAccessTime.Add(kv.Value.Policy.ExpirationTime) < now)
```

- 示例：如果某个类型对的转换只执行过一次，且之后 24 小时内未再访问 → 会被清理；
- 示例：如果某个类型对频繁访问（如每小时转换 100 次）→ 最后访问时间持续更新，永远不会被清理；

## 4. 全局清理配置的默认值

`CleanupConfig` 是控制清理频率的全局配置，默认值为：

```csharp
public static class CleanupConfig
{
    public static TimeSpan CleanupInterval = TimeSpan.FromHours(1); // 1小时清理一次
    public static bool EnableAutoCleanup = true; // 开启自动清理
}
```

- 无需手动修改，工具类静态构造函数会自动初始化 Timer 并按此配置运行；

- 如需调整，可在系统启动时全局修改（如改为 30 分钟清理一次）：
  
  ```csharp
  ExpressionMapperExtensions.CleanupConfig.CleanupInterval = TimeSpan.FromMinutes(30);
  ```

## 默认行为的适用场景

默认行为是为**90% 的普通业务场景**设计的，尤其适合：

1. 中小型项目，无需复杂的缓存策略配置；
2. 转换频率中等的业务类型（如普通的商户 / 订单 / 用户转换）；
3. 快速开发、开箱即用的场景；

只有以下场景需要手动配置预缓存 / 自定义策略：

- 核心业务类型（如商户创建 / 编辑）：预缓存 + `CachePolicy.Permanent`（永不过期），消除首次转换的编译开销；
- 临时 / 第三方类型（如临时接口 DTO）：预缓存 + `CachePolicy.ShortTerm`（1 小时过期），快速释放内存；
- 特殊过期需求：自定义 `CachePolicy`（如 8 小时过期）。

# 三、总结：核心要点

1. **预缓存能力**：支持单个 / 批量预编译核心类型对，消除首次执行的编译开销；
2. **自定义策略**：预缓存时可指定专属过期策略，兼顾核心类型和临时类型的不同需求；
3. **自动清理**：基于最后访问时间 + 策略的智能清理，防止缓存膨胀，控制内存占用；
4. **高性能**：Expression 树编译委托，后续执行无反射开销，性能接近手动赋值；
5. **健壮性**：完善的线程安全、资源释放、容错机制，适配生产环境使用。

生产环境级别的最终实现，既满足高性能需求，又兼顾灵活性和可维护性。
