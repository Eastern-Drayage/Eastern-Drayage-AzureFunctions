using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FunctionApp1;

public class UpdateAmsStatus
{
    private readonly ILogger<UpdateAmsStatus> _logger;
    private readonly string _connectionString;

    public UpdateAmsStatus(ILogger<UpdateAmsStatus> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration["DefaultConnection"] ?? string.Empty;
    }

    [Function("UpdateAmsStatus")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", "delete")] HttpRequest req)
    {
        _logger.LogInformation("UpdateAmsStatus function triggered.");

        AmsUpdatePayload? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<AmsUpdatePayload>(
                req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("Invalid JSON payload.");
        }

        if (req.Method.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            if (payload?.ID is null)
                return new BadRequestObjectResult("Payload must include: ID.");

            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string selectSql = "SELECT ID FROM tblAMS WHERE ID = @ID";
                await using var selectCmd = new SqlCommand(selectSql, connection);
                selectCmd.Parameters.AddWithValue("@ID", payload.ID);
                var existing = await selectCmd.ExecuteScalarAsync();

                if (existing is null)
                    return new NotFoundObjectResult($"No AMS record found with ID {payload.ID}. Nothing was deleted.");

                const string deleteSql = "DELETE FROM tblAMS WHERE ID = @ID";
                await using var deleteCmd = new SqlCommand(deleteSql, connection);
                deleteCmd.Parameters.AddWithValue("@ID", payload.ID);

                int rows = await deleteCmd.ExecuteNonQueryAsync();
                _logger.LogInformation("Delete: {Rows} row(s) for ID {ID}.", rows, payload.ID);

                return new OkObjectResult(new { message = "AMS record deleted successfully.", id = payload.ID, rowsAffected = rows });
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Database error while deleting AMS record.");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.DispatchedContainer)
            || string.IsNullOrWhiteSpace(payload.DriverFullName)
            || string.IsNullOrWhiteSpace(payload.Date)
            || string.IsNullOrWhiteSpace(payload.Time)
            || string.IsNullOrWhiteSpace(payload.Chassis))
        {
            return new BadRequestObjectResult(
                "Payload must include: DispatchedContainer, DriverFullName, Date, Time, Chassis. " +
                "ReturnedContainer is optional.");
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string selectDispatchSql = "SELECT ID FROM tblAMS WHERE CONTAINER = @DispatchedContainer";
            await using var selectDispatchCmd = new SqlCommand(selectDispatchSql, connection);
            selectDispatchCmd.Parameters.AddWithValue("@DispatchedContainer", payload.DispatchedContainer);
            var existingDispatch = await selectDispatchCmd.ExecuteScalarAsync();

            if (existingDispatch is null)
                return new NotFoundObjectResult($"No AMS record found for container '{payload.DispatchedContainer}'. Nothing was updated.");

            // Required: update delivery info for dispatched container
            const string deliverySql = @"
                UPDATE tblAMS
                SET [DEL DATE] = @Date,
                    [DEL BY]   = @DriverFullName,
                    [DEL TME]  = @Time,
                    CHASSIS    = @Chassis
                WHERE CONTAINER = @DispatchedContainer";

            await using var deliveryCmd = new SqlCommand(deliverySql, connection);
            deliveryCmd.Parameters.AddWithValue("@Date", payload.Date);
            deliveryCmd.Parameters.AddWithValue("@DriverFullName", payload.DriverFullName);
            deliveryCmd.Parameters.AddWithValue("@Time", payload.Time);
            deliveryCmd.Parameters.AddWithValue("@Chassis", payload.Chassis);
            deliveryCmd.Parameters.AddWithValue("@DispatchedContainer", payload.DispatchedContainer);

            int deliveryRows = await deliveryCmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Delivery update: {Rows} row(s) for container {Container}.", deliveryRows, payload.DispatchedContainer);

            // Optional: update return info if driver returned an empty container
            int returnRows = 0;
            if (!string.IsNullOrWhiteSpace(payload.ReturnedContainer))
            {
                const string returnSql = @"
                    UPDATE tblAMS
                    SET [RET BY]   = @DriverFullName,
                        [RET DATE] = @Date,
                        [RET TIME] = @Time,
                        CHASSIS    = @Chassis
                    WHERE CONTAINER = @ReturnedContainer";

                await using var returnCmd = new SqlCommand(returnSql, connection);
                returnCmd.Parameters.AddWithValue("@DriverFullName", payload.DriverFullName);
                returnCmd.Parameters.AddWithValue("@Date", payload.Date);
                returnCmd.Parameters.AddWithValue("@Time", payload.Time);
                returnCmd.Parameters.AddWithValue("@Chassis", payload.Chassis);
                returnCmd.Parameters.AddWithValue("@ReturnedContainer", payload.ReturnedContainer);

                returnRows = await returnCmd.ExecuteNonQueryAsync();
                _logger.LogInformation("Return update: {Rows} row(s) for container {Container}.", returnRows, payload.ReturnedContainer);
            }

            return new OkObjectResult(new
            {
                message = "AMS record(s) updated successfully.",
                dispatchedContainer = payload.DispatchedContainer,
                deliveryRowsAffected = deliveryRows,
                returnedContainer = payload.ReturnedContainer,
                returnRowsAffected = returnRows
            });
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Database error while updating AMS records.");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}

public record AmsUpdatePayload(
    int? ID,
    string? DispatchedContainer,
    string? DriverFullName,
    string? Date,
    string? Time,
    string? Chassis,
    string? ReturnedContainer
);
