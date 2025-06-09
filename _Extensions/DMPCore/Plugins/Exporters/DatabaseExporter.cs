using Microsoft.Data.SqlClient;
using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Plugins.Exporters;

public class DatabaseExporter : IResultExporter
{
    public string TargetType => "database";

    public void Export(IEnumerable<FrozenMetricResult> results)
    {
        if (!results.Any())
            return;

        var connectionString = "Server=.;Database=StatDB;Trusted_Connection=True";
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        foreach (var result in results)
        {
            var command = new SqlCommand(
                "INSERT INTO MetricResults " +
                "(MetricName, MetricDefinitionId, Value, Unit, " +
                "StartTime, EndTime, Frequency, " +
                "TotalRecords, CalculationTime) " +
                "VALUES " +
                "(@MetricName, @MetricDefinitionId, @Value, @Unit, " +
                "@StartTime, @EndTime, @Frequency, " +
                "@TotalRecords, @CalculationTime)", connection);

            command.Parameters.AddWithValue("@MetricName", result.MetricName);
            command.Parameters.AddWithValue("@MetricDefinitionId", result.MetricDefinitionId);
            command.Parameters.AddWithValue("@Value", result.Value);
            command.Parameters.AddWithValue("@Unit", result.Unit);
            command.Parameters.AddWithValue("@StartTime", result.TimeRange.Start);
            command.Parameters.AddWithValue("@EndTime", result.TimeRange.End);
            command.Parameters.AddWithValue("@Frequency", result.Frequency);
            command.Parameters.AddWithValue("@TotalRecords", result.TotalRecords);
            command.Parameters.AddWithValue("@CalculationTime", result.CalculationTime);

            command.ExecuteNonQuery();
        }
    }
}