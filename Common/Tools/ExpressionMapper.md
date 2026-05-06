# ExpressionMapper 2.0 说明文档

## 一、 核心特性说明

### 1. 三级自动降级执行路径 (核心演进)

这是本工具最显著的特性，确保了在不同 .NET 运行时环境下的最佳性能与兼容性：

| **路径**        | **执行方式**                  | **触发条件**                       | **性能**    | **AOT 兼容性** |
| ------------- | ------------------------- | ------------------------------ | --------- | ----------- |
| **Fast Path** | **Source Generator (SG)** | 目标类实现了 `ICopyValuesFrom<T>` 接口 | 极高 (原生代码) | 完美支持        |
| **JIT Path**  | **Expression Tree**       | 环境支持动态编译 (Standard JIT)        | 高 (编译后委托) | 不支持         |
| **Fallback**  | **高性能反射**                 | Native AOT 环境且无预生成代码           | 中 (带缓存优化) | 支持          |

### 2. 缓存策略体系 (灵活配置)

| **策略类型**                | **用途**         | **使用示例**                                 |
| ----------------------- | -------------- | ---------------------------------------- |
| `CachePolicy.Default`   | 默认策略 (24 小时过期) | 普通业务对象映射                                 |
| `CachePolicy.Permanent` | 永不过期           | 核心基础数据 (商户/用户/订单)                        |
| 自定义策略                   | 按需配置           | `new CachePolicy(TimeSpan.FromHours(8))` |

---

## 二、 业务代码调用示例

### 1. 基础调用 (零成本迁移)

完全兼容原有语法，支持现有对象拷贝与新对象创建。

```csharp
// 场景 A：拷贝值到现有对象 (Update)
existingMerchant.CopyValuesFrom(updateDto);

// 场景 B：创建新对象并拷贝 (Create)
var newMerchant = dto.CopyToNew<MerchantInfo>();
```

### 2. 配合 Source Generator (极致性能)

在 .NET 10 项目中，只需让类实现接口，工具会自动切换到无反射路径。

```csharp
// 领域对象实现接口
public partial class MerchantInfo : ICopyValuesFrom<MerchantCreateDto>
{
    // SG 会自动生成 CopyValuesFrom 的实现逻辑
}

// 调用端代码无需改变，内部自动走最快路径
merchant.CopyValuesFrom(dto); 
```

---

## 三、 关键设计细节

1. **Native AOT 完美适配**：
   
   - 自动探测 `RuntimeFeature.IsDynamicCodeCompiled`。
   
   - 在 AOT 环境下自动降级为反射路径，配合 `[DynamicallyAccessedMembers]` 特性防止元数据被裁剪，确保程序不崩溃。

2. **零锁高性能并发**：
   
   - 移除传统的 `ReaderWriterLockSlim`，改用 `ConcurrentDictionary` 配合 `Interlocked.Exchange` 原子操作更新时间戳。
   
   - 极大降低了高并发场景下缓存访问的竞争开销。

3. **智能内存治理**：
   
   - **自动化清理**：内置后台 `Timer` 每小时扫描一次，释放不活跃的缓存项。
   
   - **时间戳同步**：每次访问均通过 `long` 刻度更新最后访问时间，确保活跃业务缓存不被误删。

4. **SG 接口契约**：
   
   - 引入 `ICopyValuesFrom<in TSource>` 接口。作为框架的标准契约，不仅可用于 SG 自动代码生成，也方便了单元测试和 mock 操作。

---

## 四、 默认行为与“傻瓜式”运行

即使不进行任何配置，工具类也会按照“最优原则”运行：

| **维度**    | **默认行为**           | **设计目的**                                     |
| --------- | ------------------ | -------------------------------------------- |
| **环境自适应** | 自动选择 SG > JIT > 反射 | 保证在 Windows 服务器、Linux 容器或 AOT 移动端均能正确运行。     |
| **缓存策略**  | 自动使用 24 小时过期策略     | 在保证响应速度的同时，防止内存随运行时间无限膨胀。                    |
| **按需编译**  | 首次调用时触发编译          | 避免系统启动时加载大量无用缓存，按需分配资源。                      |
| **元数据保护** | 静态标记属性访问           | 确保在单文件发布 (SingleFile) 和压缩 (Trimmed) 模式下功能完整。 |

### 什么时候需要手动配置？

- **核心热点类型**：建议使用 `CachePolicy.Permanent` 或配合 Source Generator，消除 JIT 编译产生的首词冷启动耗时。

- **内存受限环境**：可调低 `CleanupInterval` 或设置更短的 `ExpirationTime`。

---

## 五、 总结：核心要点

1. **三位一体**：它是目前业界少数同时完美支持 SG 静态生成、JIT 动态编译和 AOT 反射兜底的映射工具。

2. **工业级稳定**：内置完善的资源释放和异常捕获机制，监听进程退出信号。

3. **面向 .NET 10**：充分利用了 C# 13/14 的底层性能特性（如 `Unsafe`、`ref` 优化），是领域自治框架最稳健的基础组件。
