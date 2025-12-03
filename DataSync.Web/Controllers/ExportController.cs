using System;
using System.Text.Json;
using System.Threading.Tasks;
using DataSync.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataSync.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ExportController : ControllerBase
    {
        private readonly IExportService _exportService;

        public ExportController(IExportService exportService)
        {
            _exportService = exportService;
        }

        [HttpGet]
        public async Task ExportData([FromQuery] string appName, [FromQuery] string dbName, [FromQuery] string tableName, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            if (string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(tableName))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("appName, dbName and tableName are required");
                return;
            }

            long logId = 0;
            int count = 0;

            try
            {
                var result = await _exportService.ExportDataAsync(appName, dbName, tableName, fromDate, toDate);
                logId = result.LogId;

                Response.ContentType = "application/json";
                
                await using var writer = new Utf8JsonWriter(Response.BodyWriter);
                writer.WriteStartObject();
                writer.WriteString("dbName", result.DbName);
                writer.WriteString("tableName", result.TableName);
                writer.WriteStartArray("data");

                foreach (var item in result.Data)
                {
                    JsonSerializer.Serialize(writer, item);
                    count++;
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                await writer.FlushAsync();

                await _exportService.CompleteLogAsync(logId, count, "Success");
            }
            catch (Exception ex)
            {
                if (logId > 0)
                {
                    await _exportService.CompleteLogAsync(logId, count, "Failed", ex.Message);
                }
                
                // If we haven't started writing the response, we can return 500
                if (!Response.HasStarted)
                {
                    Response.StatusCode = 500;
                    await Response.WriteAsync(ex.Message);
                }
            }
        }
    }
}
