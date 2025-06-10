namespace TKWF.DMP.Core.Interfaces;

public interface IDataConverter<out T> where T : class
{
    T Convert(Dictionary<string, object> dataItem);
}