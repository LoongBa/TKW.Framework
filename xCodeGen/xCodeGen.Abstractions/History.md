## [V1.27] - 2026-03-08

### 🛡️ 架构与安全升级 (Architecture & Security)

* **持久化信任闭环**：
  * **入口自动挂载**：Service 层查询入口（Get/Select）自动为实体注入 `IsFromPersistentSource = true`。
  * **出口强制终验**：Service 层写入入口（Create/Update）通过 `ForceValidate` 强行击穿信任标记，确保最终入库数据合法。
  * **防伪造注入**：基类属性 `IsFromPersistentSource` 强制要求手动配置 `[JsonIgnore]`，切断前端提权路径。
* **属性行为隔离**：
  * **DtoFieldIgnore 支持**：验证引擎与回填引擎同步支持 `[DtoFieldIgnore]`，彻底解决“DTO不存在该属性但验证逻辑仍尝试访问”导致的编译错误。
  * **导航属性拦截**：自动拦截 `[Navigate]` 属性，杜绝 ORM 级联更新引发的数据污染。

### ⚡ 现代化 .NET 适配

* **NRT 完全支持**：所有生成文件强制开启 `#nullable enable`。
* **Required 语义对齐**：DTO 必填字段自动挂载 C# 11 `required` 关键字，消除 `CS8618` 警告。

### 🐛 缺陷修复

* **MapType 校验修正**：当字段使用 `MapType` 进行物理映射偏移时，自动撤销无效的 `MaxLength` 验证。
* **Summary 注释安全**：引入 `CleanSummary` 净化多行注释，防止非对称 XML 标签破坏代码结构。
