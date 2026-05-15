# TKWF.IdGenerator 组件指南

## 1. 需求分析 (Requirement Analysis)

在现代化的业务系统中，生成唯一标识符（ID）是极其高频的基础基建需求（如订单号、流水号、批次号等）。随着系统向高并发、分布式（如 Kubernetes 集群）以及 .NET 10 架构的演进，传统的 ID 生成策略面临以下痛点，也是本组件旨在解决的核心需求：

- **绝对唯一性要求**：在多台服务器/多个容器实例同时高并发生成 ID 时，必须保证业务层面 100% 不出现主键冲突。

- **高吞吐与低延迟**：ID 生成往往处于业务链路的最前端，必须具备极高的性能，不能成为系统的性能瓶颈。

- **时序可读性**：单纯的 UUID/GUID 过于冗长且无序，不仅降低数据库索引效率，排查问题时也无法直观看出生成时间。业务更倾向于 `时间戳 + 流水号` 格式。

- **时钟回拨容忍度**：基于时间戳的算法（如 Snowflake 思想）极易受服务器时间同步（NTP）导致的时钟回拨影响，必须有健壮的异常处理机制。

- **差异化部署场景**：单体应用需要“零配置开箱即用”；而大规模 K8s 集群需要严格的“工作节点（WorkerId）隔离”。

---

## 2. 设计原理 (Design Principles)

本组件遵循“机制与策略分离”**以及**“按需付费”的设计哲学，底层依赖 .NET 10 原生高性能密码学 API，上层提供两种差异化的生成策略。

### 2.1 核心生成规则

无论是哪种生成器，核心结构均采用以下模式保证唯一性与可读性： `[业务前缀] + [时间戳 yyyyMMddHHmmss] + [WorkerId(可选)] + [毫秒内自增序列(3位)] + [安全随机字符串补齐]` *(示例：`ORD2023102710304505001aX9b`)*

### 2.2 双引擎架构

- **`DefaultIdGenerator` (轻量级引擎)**
  
  - **原理**：去除 WorkerId 概念，依赖“时间戳 + 本地 CAS 序列锁 + .NET 10 密码学强随机数”保证唯一性。
  
  - **优势**：零内存分配、极致性能、无需任何环境变量配置。

- **`DistributedIdGenerator` (重型分布式引擎)**
  
  - **原理**：在生成字符串中强制混入 `WorkerId`（节点标识）。
  
  - **优势**：从数学与物理层面彻底阻断多实例间的毫秒级并发碰撞。

### 2.3 关键技术点

1. **无锁并发 (Lock-Free)**：放弃传统的 `lock` 关键字，使用 `Interlocked.CompareExchange` 和 `SpinWait`（CAS 机制）处理毫秒内的序列号递增，大幅提升高并发下的线程吞吐量。

2. **防御时钟回拨**：在自旋锁内部记录 `_lastTimestamp`，一旦发现当前时间小于上次时间，立即抛出异常或自旋等待，杜绝由于时钟回拨产生的重复 ID。

3. **.NET 10 性能榨取**：随机字符串的生成抛弃了手写的 `for` 循环拼接，直接调用底层 `RandomNumberGenerator.GetString(Alphabet, length)`。该 API 由底层 C/C++ 实现，真正做到零 GC 内存分配。

---

## 3. 使用说明 (User Guide)

### 3.1 注册依赖注入 (DI)

在你的 `Program.cs` 或 `Startup.cs` 中，根据当前部署环境的规模选择注入哪种生成器：

C#

```
// 方案 A：适用于单体应用或小规模微服务（开箱即用）
builder.Services.AddSingleton<IIdGenerator, DefaultIdGenerator>();

// 方案 B：适用于 K8s 大规模集群部署
// 从环境变量读取 Pod 专属的 WorkerId (0-99)
int workerId = int.TryParse(Environment.GetEnvironmentVariable("WORKER_ID"), out int id) ? id : 0;
builder.Services.AddSingleton<IIdGenerator>(new DistributedIdGenerator(workerId));
```

**⚠️ 重要提醒**：ID 生成器**必须**注册为 `Singleton`（单例），否则内存中的毫秒序列号 `_sequence` 将失去自增意义。

### 3.2 基础注入与使用

在业务 Service 中通过构造函数注入 `IIdGenerator` 即可使用：

C#

```
public class OrderService
{
    private readonly IIdGenerator _idGenerator;

    public OrderService(IIdGenerator idGenerator)
    {
        _idGenerator = idGenerator;
    }

    public void CreateOrder()
    {
        // 生成长度为 24，带 ORD 前缀的订单号
        // 输出示例: ORD20231027103045001xyz8
        string orderNo = _idGenerator.NewId(length: 24, prefix: "ORD"); 
    }
}
```

### 3.3 集合的批量生成 (依托扩展方法)

利用 `IdGeneratorExtensions`，可以在处理列表或异步流时，优雅地为实体批量赋码：

C#

```
// 1. 同步列表批量赋码 (直接返回新 List)
var newOrders = unassignedOrders.ApplyIds(
    generator: _idGenerator,
    idSetter: (order, id) => order.OrderNo = id,
    length: 24,
    prefix: "ORD"
);

// 2. 异步流批量处理 (适用于从 EF Core 或 EF/Dapper 获取数据流时)
await foreach (var item in dbStream.WithIdsAsync(_idGenerator, (x, id) => x.TraceId = id))
{
    // 处理已被赋予 TraceId 的 item
}
```

---

## 4. 后续维护和扩展的使用说明 (Maintenance & Extension)

本组件设计严格遵循开闭原则（OCP），对扩展开放，对修改封闭。

### 4.1 如何扩展新的生成规则

如果未来业务要求一种完全不同格式的 ID（例如纯数字的分布式短码），**不要修改**现有的 `DefaultIdGenerator`。
正确的做法是实现一个新的类：

C#

```
public class NumericShortIdGenerator : IIdGenerator
{
    public string NewId(int length = 32, string? prefix = null)
    {
        // 实现你的纯数字生成逻辑，如调用底层 MathUtil 等
        return "新规则生成的ID";
    }
}
```

随后在 DI 容器中按需替换注入逻辑即可。

### 4.2 运维指南：Kubernetes 环境下的 WorkerId 分配

使用 `DistributedIdGenerator` 时，`WorkerId` 的分配是运维的关键。以下是推荐的实践方案：

1. **StatefulSet 序号提取**：如果应用部署为 StatefulSet，Pod 名称会带有序号（如 `my-app-0`, `my-app-1`），可在启动脚本中提取最后的数字作为 `WORKER_ID` 环境变量传入。

2. **IP 取模计算**：在程序启动时，获取当前容器的内网 IP 地址，取最后一段（如 `192.168.1.105` 取 `105`），执行 `105 % 100` 得到 WorkerId（5）。

### 4.3 故障排查 (Troubleshooting)

- **异常**：`System.InvalidOperationException: 时钟回拨异常`
  
  - **原因**：服务器操作系统发生了向后调整时间的行为。
  
  - **处理建议**：组件内置了小范围回拨的自旋等待（如果回拨在毫秒级会等待恢复）。如果抛出此异常，说明回拨跨度极大。建议检查服务器的 NTP 同步守护进程（如 `chronyd`）配置，务必将时间同步模式设置为 **Slew（平滑微调）**，禁止采用 Step（跳跃重置）模式。

- **性能瓶颈排查**：如果在高并发压测下发现 CPU 利用率异常升高，请检查 DI 注册是否误写成了 `AddTransient`。每次请求 new 一个生成器会导致 CAS 锁失效并疯狂触发随机字符串生成逻辑。
