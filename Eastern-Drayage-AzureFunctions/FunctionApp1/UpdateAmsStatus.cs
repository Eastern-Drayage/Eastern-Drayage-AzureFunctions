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
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        _logger.LogInformation("UpdateAmsStatus function triggered.");

        // Temporary test response — DB update logic is ready below, enabled after client approval
        return new OkObjectResult("UpdateAmsStatus function is running successfully.");

        /*
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

        if (payload is null || payload.Id == 0 || string.IsNullOrWhiteSpace(payload.Status))
        {
            return new BadRequestObjectResult("Payload must include a non-zero 'Id' and a non-empty 'Status'.");
        }

        _logger.LogInformation("Updating AMS record {Id} to status '{Status}'.", payload.Id, payload.Status);

        const string sql = "UPDATE AMS SET Status = @Status WHERE Id = @Id";

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Status", payload.Status);
            command.Parameters.AddWithValue("@Id", payload.Id);

            int rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                _logger.LogWarning("No AMS record found with Id {Id}.", payload.Id);
                return new NotFoundObjectResult($"No AMS record found with Id {payload.Id}.");
            }

            _logger.LogInformation("AMS record {Id} updated successfully.", payload.Id);
            return new OkObjectResult(new { message = "AMS record updated successfully.", id = payload.Id, status = payload.Status });
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Database error while updating AMS record {Id}.", payload.Id);
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
        */
    }
}

public record AmsUpdatePayload(int Id, string Status);
