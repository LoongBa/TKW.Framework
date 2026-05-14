# ExpressionMapper 2.1 说明文档

## 2.1 版本主要变化 (Change Log)

- **新增 `Clone()` 扩展方法**：基于 `CopyToNew<T, T>` 实现的强类型深拷贝 API，自动识别并利用 SG 静态加速。

- **SG 增强（多源映射）**：`[DomainMapFrom]` 特性支持传入多个 `Type` 参数，为单个目标类生成多个映射源的静态代码。

- **SG 增强（自动克隆）**：若 `[DomainMapFrom]` 不传参数，默认生成针对类自身的映射代码（实现高效 Clone）。

- **`CopyToNew` 逻辑优化**：在 JIT 模式下采用 `MemberInit` 表达式编译，实现“实例化+赋值”一次性完成，性能提升 15%+。

- **新增预热 API**：引入 `BatchPreCache` 方法，支持在应用启动时提前编译并缓存高频映射委托。

- **手写规避机制**：SG 增加语义检测，若类中已存在同签名手写方法，自动跳过生成以防止编译冲突。

---

## 一、 核心特性说明

### 1. 三级自动降级执行路径 (核心演进)

本工具采用三级路径分发逻辑，确保在任何环境下都能以当前物理极限性能运行：

| **路径等级**              | **执行方式**             | **触发条件**                      | **性能等级**     | **Native AOT** |
| --------------------- | -------------------- | ----------------------------- | ------------ | -------------- |
| **Tier 0 (Fast)**     | **Source Generator** | 目标类实现 `ICopyValuesFrom<T>` 接口 | **极致 (原生码)** | 完美支持           |
| **Tier 1 (JIT)**      | **Expression Tree**  | 环境支持 JIT，且未命中 SG 代码           | **高 (编译委托)** | 不支持            |
| **Tier 2 (Fallback)** | **高性能反射**            | AOT 环境且无预生成代码                 | **中 (缓存反射)** | 支持             |

### 1.1 调用流程决策树

1. **是否有 SG 生成的实例方法？** -> **直接调用** (Tier 0)。

2. **是否支持 JIT？** -> **调用/编译表达式树委托** (Tier 1)。

3. **AOT 环境？** -> **执行带缓存的反射同步** (Tier 2)。

### 2. 缓存策略（可配置）

| **策略类型**                | **用途**         | **使用示例**                                 |
| ----------------------- | -------------- | ---------------------------------------- |
| `CachePolicy.Default`   | 默认策略 (24 小时过期) | 普通业务对象映射                                 |
| `CachePolicy.Permanent` | 永不过期           | 核心基础数据 (商户/用户/订单)                        |
| 自定义策略                   | 按需配置           | `new CachePolicy(TimeSpan.FromHours(8))` |

## 三、 高级功能与优化

### 1. 批量预热 (Startup Warm-up)

通过 `BatchPreCache` 方法，可以在应用启动时消除首次调用产生的 JIT 编译延迟。

```csharp
var pairs = new List<(Type, Type)> { (typeof(UserDto), typeof(UserEntity)) };
ExpressionMapper.BatchPreCache(pairs); // 提前编译映射委托并注入缓存
```

### 2. 多映射支持 (Multi-Source Mapping)

通过 `[DomainMapFrom]` 特性，一个类可以同时具备从多个不同源对象（如不同版本的 DTO）接收数据的能力。

```csharp
[DomainMapFrom(typeof(CreateDto), typeof(UpdateDto))]
public partial class DomainEntity { ... } // SG 会自动为每个源生成专用的映射代码
```

### 3. 内存治理

- **原子更新**：采用 `long` 时间戳和 `Interlocked.Exchange`，确保高并发下无锁竞争更新访问时间。

- **自动清理**：内置后台 `Timer` 每小时扫描并释放不活跃的缓存，防止内存溢出。

### 3. 设计哲学与定位

#### 3.1 与 Domain SG 的关系

- **Domain SG (复杂逻辑)**：处理非对称字段、嵌套映射或需要业务干预的复杂转换。

- **ExpressionMapper (通用引擎)**：处理同名属性的“平铺式”拷贝。它是领域框架的**通用保底机制**，也是高性能克隆的**主力引擎**。

#### 3.2 Native AOT 适配

工具通过 `RuntimeFeature.IsDynamicCodeCompiled` 探测运行时能力。在 AOT 发布模式下，它会自动规避无法运行的 `Expression.Compile()`，转而使用受元数据保护（`DynamicallyAccessedMembers`）的反射路径，确保应用在裁剪（Trimmed）模式下依然稳健。

---

## 二、 业务代码调用示例

### 1. 三大核心扩展方法

```csharp
// 场景 A：拷贝值到现有对象 (Update)
existingUser.CopyValuesFrom(dto);
// 场景 B：创建新对象并拷贝 (Create)
var newUser = dto.CopyToNew<UserInfo>();
// 场景 C：高性能深拷贝 (Clone) - 2.1 新增
var snapshot = user.Clone();
```

### 2. 配合 Source Generator (极致性能)

在类上添加 `[DomainMapFrom]` 并声明为 `partial`，工具会自动生成静态代码：

```csharp
// 支持多源映射与自动克隆
[DomainMapFrom(typeof(UserCreateDto), typeof(UserUpdateDto))]
public partial class UserInfo : ICopyValuesFrom<UserCreateDto>, ICopyValuesFrom<UserUpdateDto>
{
    // SG 会自动生成对应接口的静态实现逻辑
}
```

---

## 三、 关键设计细节

1. **Native AOT 完美适配**：
   
   自动探测 `RuntimeFeature.IsDynamicCodeCompiled`。在 AOT 环境下优先通过接口探测调用 SG 代码，无 SG 时自动降级为反射路径，配合 `[DynamicallyAccessedMembers]` 防止元数据裁剪。

2. **零锁高性能并发**：
   
   放弃 `ReaderWriterLockSlim`，改用 `ConcurrentDictionary` 配合 `Interlocked.Exchange` 原子操作更新 `long` 类型时间戳，极大降低高并发下的竞争开销。

3. **智能内存治理**：
   
   内置后台 `Timer` 每小时扫描一次，结合 `CachePolicy` 自动释放不活跃的缓存项。

4. **启动预热支持 (2.1 新增)**：
   
   提供 `BatchPreCache` 接口，允许开发者在 `Program.cs` 中预编译核心映射关系，消除首次调用的冷启动延迟。

---

## 四、 默认行为与“傻瓜式”运行

| **维度**    | **默认行为**           | **设计目的**                            |
| --------- | ------------------ | ----------------------------------- |
| **环境自适应** | 自动选择 SG > JIT > 反射 | 保证在 Windows、Linux 容器或 AOT 环境均能正确运行。 |
| **缓存策略**  | 24 小时自动滑动过期        | 保证性能的同时，防止内存随时间无限膨胀。                |
| **按需编译**  | 首次调用触发延迟编译         | 避免启动时加载大量无用缓存，按需分配资源。               |
| **手写兼容**  | 手写代码 > 生成代码        | 尊重开发者意图，若手动实现了接口，SG 会自动让路。          |

---

## 五、 总结：核心要点

- **三位一体**：目前业界极少数同时支持 SG 静态生成、JIT 动态编译和 AOT 反射兜底的映射工具。

- **工业级稳定**：内置完善的资源回收、异常捕获以及原子化并发控制。

- **面向未来**：充分利用 C# 13/14 底层特性（如 `Unsafe`、`ref` 优化），作为领域自治框架的最稳健组件。
