namespace TKWF.Tools.ExcelTools;

/// <summary>
/// 通用导入数据适配器接口
/// 统一各类数据源（不局限于Excel）的数据验证与转换逻辑，适配任意目标实体类型
/// </summary>
/// <typeparam name="TTargetEntity">数据转换后的目标实体类型</typeparam>
public interface IImportDataAdapter<in TTargetEntity>
{
    /// <summary>
    /// 获取当前数据源名称（如：摩术师、美团券、抖音券）
    /// </summary>
    string DataSourceName { get; }

    /// <summary>
    /// 版本号（如：V1.0、V2.0）
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 获取当前数据源备注（如：适配摩术师V2.0版本、美团官方对账模板）
    /// </summary>
    string Remark { get; }

    /// <summary>
    /// 源数据列名 → 目标实体字段名 映射关系（只读）
    /// 用于定义数据源字段与目标实体字段的对应关系
    /// </summary>
    Dictionary<string, string> ColumnMapping { get; }

    /// <summary>
    /// 数据业务验证逻辑：校验数据是否符合导入要求，不符合则返回对应处理状态
    /// </summary>
    /// <param name="rowIndex">数据行索引（用于定位错误数据位置）</param>
    /// <param name="targetEntity">待验证的目标实体对象</param>
    /// <param name="rowValues">原始数据行键值对字典</param>
    /// <param name="otherDict">未参与映射的原始数据键值对字典</param>
    /// <param name="failure">校验失败信息（引用传递，用于输出错误详情）</param>
    /// <returns>导入数据处理状态指令（继续/跳过/终止）</returns>
    ImportDataProcessStatusEnum ValidateData(int rowIndex, TTargetEntity targetEntity, Dictionary<string, object?> rowValues, Dictionary<string, object?> otherDict, ref ImportFailure failure);

    /// <summary>
    /// 数据清洗、转换与加载逻辑：将原始数据处理后赋值到目标实体
    /// </summary>
    /// <param name="rowIndex">数据行索引（用于定位错误数据位置）</param>
    /// <param name="targetEntity">待赋值的目标实体对象</param>
    /// <param name="otherDict">未参与映射的原始数据键值对字典</param>
    void ConvertData(int rowIndex, TTargetEntity targetEntity, Dictionary<string, object?> otherDict);
}