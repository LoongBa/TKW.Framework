# TKWF.Cryptography 组件指南

## 1. 演进背景与架构定位 (Background & Positioning)

在旧版本的系统中，加密模块包含了大量历史遗留代码（如 MD5 盐值加密、手动实例化的 `HashAlgorithmFactory`、缺乏防篡改验证的对称加密等）。随着系统向 **.NET 10** 演进，现代密码学标准和底层 CLR 性能都发生了巨大变化。

**本次重构的核心痛点与目标：**

- **消除严重安全隐患**：彻底废弃已被证明不安全的 MD5 密码存储机制，引入工业标准的 PBKDF2 算法；修复旧版 AES 加密密文使用 `UTF8.GetString` 强转导致的数据丢失 Bug。

- **引入 AEAD 认证加密**：传统的对称加密（如 AES-CBC）容易受到“填充预言攻击”，新架构全面转向自带防篡改 MAC（消息认证码）的 **AES-GCM**。

- **极致的性能榨取**：全面拥抱静态 `HashData`、`Span<T>` 和无分配（Allocation-Free）的底层 API，大幅降低 GC 内存回收压力。

- **职责极度纯粹**：本包被定位为“最底层、无状态的纯粹密码学机制”，移除了所有与业务（如微信支付签名、纯随机字符串生成）相关的逻辑，确保极高的复用性。

---

## 2. 核心设计原理 (Design Principles)

1. **Security by Default（默认安全）**：开发者不需要懂密码学，只要调用该包的方法，底层就会默认使用最安全的迭代次数、Nonce 长度和 Tag 校验。

2. **Stateless Static API（无状态静态调用）**：加密解密本质上是数学运算。新版废弃了繁琐的 Factory 和面向对象实例化，全部采用高性能的静态方法。

3. **Time-Constant Comparison（时间恒定比较）**：在验证密码或哈希时，底层强制使用 `CryptographicOperations.FixedTimeEquals`，彻底杜绝黑客利用响应时间差进行的时序攻击（Timing Attacks）。

---

## 3. 核心模块与使用说明 (Core Modules & Usage)

本包提供五个绝对核心的工具类，分别应对**防篡改哈希**、**用户密码存储**、**数据机密性加密**、**哈希消息认证码**以及**非对称签名**场景。

### 3.1 `HashUtil` (常规哈希与防篡改)

用于计算文件哈希、API 签名摘要或普通数据的单向散列。**严禁用于用户密码存储。**

```csharp
using TKW.Framework.Cryptography;

// 1. 计算 SHA256 (推荐默认使用)
// 输出大写的 16 进制字符串，完全无额外内存分配
string hashHex = HashUtil.ComputeSha256("my_payload_data");

// 2. 计算 MD5 (⚠️ 仅限用于兼容老旧第三方接口或校验普通文件，绝不可用于安全领域)
string md5Hex = HashUtil.ComputeMd5("legacy_data");

// 3. 安全的哈希比对 (防止时序攻击)
bool isValid = HashUtil.VerifyHash("my_payload_data", expectedHashHex, HashUtil.ComputeSha256);
```

### 3.2 `PasswordHasher` (用户密码专有处理)

用于系统中所有涉及到用户登录密码、支付密码的加密存储。底层采用防彩虹表和暴力破解的 **PBKDF2** 算法（遵循 OWASP 推荐的 350,000 次迭代）。

```csharp
using TKW.Framework.Cryptography;

// 1. 注册/修改密码时：生成密码的安全哈希
// 内部会自动生成高强度盐值 (Salt) 并执行 35万次哈希迭代
// 返回格式为 "Iterations.Salt(Base64).Hash(Base64)"，直接存入数据库的 PasswordHash 字段
string dbHashToStore = PasswordHasher.HashPassword("User@123456");

// 2. 用户登录时：验证密码
// 读取数据库中的 hash，与用户输入的明文比对
bool isLoginSuccess = PasswordHasher.VerifyPassword("User@123456", dbHashToStore);
```

### 3.3 `AeadEncryptionUtil` (高级认证对称加密)

用于加密敏感数据（如身份证号、数据库连接字符串、跨服务通信 Token）。底层采用 **AES-GCM**。**它不仅能加密，还能在解密时验证数据是否被中间人篡改。**

```csharp
using TKW.Framework.Cryptography;

// 必须准备一个绝对安全的 32字节(256位) 密钥，并转换为 Base64。
string keyBase64 = "YOUR_32_BYTE_KEY_IN_BASE64_FORMAT=="; 

// 1. 加密敏感数据
// 返回的密文 Base64 内部已安全封入了随机 Nonce 和防篡改 Tag
string cipherText = AeadEncryptionUtil.Encrypt("130102199001011234", keyBase64);

// 2. 解密数据
// 如果 cipherText 在传输过程中被黑客修改了哪怕一个比特，Decrypt 会直接抛出 CryptographicException，绝对不会返回错误明文。
string plainText = AeadEncryptionUtil.Decrypt(cipherText, keyBase64);
```

### 3.4 `HmacUtil` (哈希消息认证码)

用于需要带密钥的哈希计算场景（如微信支付 V2 验签、JWT Token 签名、开放 API 接口防伪造）。防篡改能力强于普通哈希。

```csharp
using TKW.Framework.Cryptography;

// 1. 计算 HMAC-SHA256 (输出大写 16 进制字符串)
string hmacHex = HmacUtil.ComputeHmacSha256("api_payload_data", "your_secret_api_key");

// 2. 安全验证 HMAC (防止时序攻击)
bool isValid = HmacUtil.VerifyHmacSha256("api_payload_data", "your_secret_api_key", expectedHmacHex);
```

### 3.5 `RsaUtil` (非对称加密与数字签名)

用于极其重要的身份验签场景（如支付宝回调验签、微信支付 V3 请求签名）。全面拥抱标准 PEM 格式，彻底摒弃 .NET 独有且难以跨语言的 XML 密钥格式。

```csharp
using TKW.Framework.Cryptography;

string privateKeyPem = "-----BEGIN PRIVATE KEY-----\nMIIEvAIB...\n-----END PRIVATE KEY-----";
string publicKeyPem = "-----BEGIN PUBLIC KEY-----\nMIIBIjAN...\n-----END PUBLIC KEY-----";

// 1. 使用私钥生成 SHA256 数字签名 (返回 Base64)
string signature = RsaUtil.SignDataSha256("order_id=123&amount=100", privateKeyPem);

// 2. 使用公钥验证签名真实性
bool isValid = RsaUtil.VerifyDataSha256("order_id=123&amount=100", signature, publicKeyPem);

// 3. 公钥加密 (仅限加密极短的敏感信息，如传输 AES 密钥)
string cipherBase64 = RsaUtil.Encrypt("my_short_secret", publicKeyPem);

// 4. 私钥解密
string plainText = RsaUtil.Decrypt(cipherBase64, privateKeyPem);
```

---

## 4. 密钥配置与管理须知 (Key Management Guidelines)

在现代密码学架构中，**算法是公开的，系统的绝对安全性完全依赖于密钥的安全性**。为了配合本加密包的最高安全标准，必须遵循以下密钥配置规范：

### 4.1 严禁代码硬编码

任何环境下，**绝对禁止**在 `.cs` 代码文件中直接写入明文密钥或 PEM 字符串。这会导致密钥随代码一起提交到 Git 仓库，造成不可挽回的泄漏事故。

### 4.2 密钥的注入与存储方案

- **本地开发环境 (Local Dev)**：使用 .NET 的 `User Secrets` (机密管理器) 或者本地不提交的 `appsettings.Development.json`。

- **生产环境 (Production)**：
  
  - **基础方案**：通过服务器的环境变量 (Environment Variables) 注入。
  
  - **推荐进阶方案**：集成云厂商的密钥管理服务（如 **Azure Key Vault**, **AWS KMS**, 或 **Aliyun KMS**），在应用启动时动态加载到内存中。

### 4.3 格式与转义避坑指南

- **AES/HMAC 密钥**：建议统一在外部生成高强度的随机字节流，并使用 Base64 字符串格式保存在配置中。不要使用有明显语义的普通字符串作为密钥（如 `my_secret_password`）。

- **RSA PEM 格式的换行符处理**：
  
  标准的 PEM 密钥包含大量换行符（`\n`）。如果在 JSON 配置、Kubernetes Secrets 或环境变量中直接配置，换行符可能会丢失或被转义为字面量 `\\n`，导致 `.ImportFromPem()` 抛出异常。
  
  **正确做法**：
  
  1. 在配置文件中将密钥存为单行字符串，用明确的 `\n` 分隔。
  
  2. 在程序读取配置后，务必进行一次替换清洗：
     
     ```csharp
     var rawPem = configuration["Alipay:PrivateKey"];
     var safePem = rawPem.Replace("\\n", "\n"); // 修复环境变量导致的转义问题
     ```

---

## 5. 遗留系统迁移指南 (Migration Guide)

如果你的老项目正在升级，请按照以下对照表进行无情重构：

| **过去的老写法 (TKW.Framework.Cryptography V1)**                      | **现在的现代写法 (.NET 10 Ready)**             | **迁移风险/备注**                                                                       |
| --------------------------------------------------------------- | --------------------------------------- | --------------------------------------------------------------------------------- |
| `Md5Helper.Md5Encoding(pass, salt)`                             | `PasswordHasher.HashPassword(pass)`     | **高风险**：老用户密码无法直接转为新哈希。建议在用户下次登录时，验证老 MD5 成功后，后台静默使用 `PasswordHasher` 升级密码并覆写数据库。 |
| `CryptographyHelper.Hash(HashAlgorithmType.Sha256, text)`       | `HashUtil.ComputeSha256(text)`          | **无缝迁移**。                                                                         |
| `SymmetricAlgorithmHelper.SymmetricEncrypt(Aes, text, key, iv)` | `AeadEncryptionUtil.Encrypt(text, key)` | **不兼容**：新版 AES-GCM 不需要手动传 IV（内部自动生成安全 Nonce）。旧数据需写工具脚本，用老方法解密，再用新方法加密清洗一次。        |
| `StringUtilities.GetRandomString(length)`                       | 移至 `TKW.Framework.Common.Text`          | 基础随机数已剥离出密码包，移至通用工具包调用。                                                           |
| 各种微信支付 `MakeSignature`                                          | 移至 `TKW.Framework.Common.Signatures`    | 业务逻辑彻底从本底层包中剥离。                                                                   |

---

## 6. 安全最佳实践与红线 (Security Red Lines)

为保障系统处于绝对安全的水位，所有团队成员必须遵守以下规定：

1. 🚫 **绝对禁止**使用 `Encoding.UTF8.GetString(byte[])` 来转换任何密文或哈希的字节流。请永远使用 `Convert.ToBase64String` 或 `Convert.ToHexString`。

2. 🚫 **绝对禁止**将 `HashUtil.ComputeSha256` 或 `ComputeMd5` 用于密码存储。这无法防住现代 GPU 的彩虹表攻击，密码必须经过 `PasswordHasher`。

3. 🚫 **绝对禁止**在代码库中硬编码对称加密的密钥（Key）或非对称私钥。

4. ⚠️ 当验证两个敏感字符串（如 Token、签名或哈希）是否相等时，严禁使用 `==` 或 `string.Equals()`，必须使用 `HashUtil.VerifyHash` 或 `HmacUtil.VerifyHmacSha256`，以防止被黑客通过响应时间推算出真实 Token。
