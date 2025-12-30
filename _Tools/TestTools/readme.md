## 在 xUnit 测试类中，通过构造函数注入?`ITestOutputHelper`，再创建?`TestOutputLogger`?实例，最终注入到适配器中。

```csharp
using Xunit;
using Xunit.Abstractions;
using DMP_Lite.Domain.Models;
using DMP_Lite.Domain.Services.DataAdapters;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace DMP_Lite.UnitTests;

/// <summary>
/// 摩术师适配器单元测试
/// </summary>
public class MoshushiCashierDataAdapterTests
{
    // 持有 ITestOutputHelper 实例（xUnit 构造函数注入）
    private readonly ITestOutputHelper _testOutput;
    // 持有适配后的 ILogger 实例
    private readonly ILogger<MoshushiCashierDataAdapter> _logger;

    /// <summary>
    /// 测试类构造函数：注入 ITestOutputHelper，创建测试日志
    /// </summary>
    /// <param name="testOutput">xUnit 测试输出助手</param>
    public MoshushiCashierDataAdapterTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput ?? throw new ArgumentNullException(nameof(testOutput));
        // 使用工厂类快速创建 TestOutputLogger（适配 ILogger 接口）
        _logger = TestOutputLoggerFactory.Create<MoshushiCashierDataAdapter>(_testOutput);
    }

    /// <summary>
    /// 测试：适配器初始化与 ValidatePaymentLog 方法
    /// </summary>
    [Fact]
    public void ValidatePaymentLog_ValidData_ReturnsContinue()
    {
        // 1. 模拟委托（单元测试中无需真实数据库操作，直接返回模拟数据）
        var mockGetOrCreateStore = new Func<string, StoreInfo>((storeName) =>
        {
            return new StoreInfo { StoreName = storeName, Uid = Guid.NewGuid().ToString() };
        });

        var mockGetOrCreateItem = new Func<string, PortfolioItemInfo>((itemName) =>
        {
            return new PortfolioItemInfo { Name = itemName, Id = Guid.NewGuid().ToString() };
        });

        var mockGetOrCreateGroup = new Func<string, PortfolioGroupInfo>((groupName) =>
        {
            return new PortfolioGroupInfo { Name = groupName, Id = Guid.NewGuid().ToString() };
        });

        // 2. 初始化适配器（注入模拟委托 + 测试日志（_logger 已适配 ITestOutputHelper））
        var adapter = new MoshushiCashierDataAdapter(
            mockGetOrCreateStore,
            mockGetOrCreateItem,
            mockGetOrCreateGroup,
            _logger); // 直接注入适配后的 ILogger

        // 3. 构造测试数据
        int rowIndex = 2; // 模拟 Excel 第2行
        var paymentLog = new PaymentLog
        {
            BillNumber = "TEST20251230001",
            OrderTime = DateTime.Now,
            ProjectName = "测试项目"
        };
        var rowValues = new Dictionary<string, object?>();
        var otherDict = new Dictionary<string, object?>();
        var failure = new ExcelImportFailure(); // 失败信息

        // 4. 调用待测试方法
        var result = adapter.ValidatePaymentLog(rowIndex, paymentLog, rowValues, otherDict, ref failure);

        // 5. 断言结果
        Assert.Equal(ExcelRecordProcessStatus.Continue, result);
        Assert.Null(failure.ErrorMessage); // 验证通过，失败信息为空
    }

    /// <summary>
    /// 测试：账单号为空时，ValidatePaymentLog 返回 Skip
    /// </summary>
    [Fact]
    public void ValidatePaymentLog_EmptyBillNumber_ReturnsSkip()
    {
        // 1. 模拟委托
        var mockGetOrCreateStore = new Func<string, StoreInfo>((s) => new StoreInfo());
        var mockGetOrCreateItem = new Func<string, PortfolioItemInfo>((i) => new PortfolioItemInfo());
        var mockGetOrCreateGroup = new Func<string, PortfolioGroupInfo>((g) => new PortfolioGroupInfo());

        // 2. 初始化适配器（注入测试日志）
        var adapter = new MoshushiCashierDataAdapter(
            mockGetOrCreateStore,
            mockGetOrCreateItem,
            mockGetOrCreateGroup,
            _logger);

        // 3. 构造无效测试数据（账单号为空）
        int rowIndex = 3;
        var paymentLog = new PaymentLog
        {
            BillNumber = null,
            OrderTime = DateTime.MinValue // 订单时间也为空
        };
        var rowValues = new Dictionary<string, object?>();
        var otherDict = new Dictionary<string, object?>();
        var failure = new ExcelImportFailure();

        // 4. 调用方法
        var result = adapter.ValidatePaymentLog(rowIndex, paymentLog, rowValues, otherDict, ref failure);

        // 5. 断言
        Assert.Equal(ExcelRecordProcessStatus.Skip, result);
        Assert.NotNull(failure.ErrorMessage); // 存在失败信息
        _testOutput.WriteLine($"失败信息：{failure.ErrorMessage}"); // 直接输出，也可通过适配器日志间接输出
    }
}
```