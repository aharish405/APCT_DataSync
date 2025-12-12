using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataSync.Core.Interfaces;
using DataSync.Core.Models;
using DataSync.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DataSync.Web.Controllers
{
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public class ConfigurationController : Controller
    {
        private readonly IConfigurationRepository _configRepo;
        private readonly DataSyncDbContext _context;
        private readonly IDataExportRepository _dataExportRepo;

        public ConfigurationController(IConfigurationRepository configRepo, DataSyncDbContext context, IDataExportRepository dataExportRepo)
        {
            _configRepo = configRepo;
            _context = context;
            _dataExportRepo = dataExportRepo;
        }

        public IActionResult Index()
        {
            return View(_context.ExportConfigurations.OrderBy(x => x.AppName));
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ExportConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(config.TableName))
            {
               ModelState.AddModelError("TableName", "Table Name is required.");
            }

            if (string.IsNullOrWhiteSpace(config.CustomQuery) && string.IsNullOrWhiteSpace(config.DateColumn))
            {
                ModelState.AddModelError("DateColumn", "Date Column is required when Custom Query is not provided.");
            }

            if (ModelState.IsValid)
            {
                // Strict Backend Validation
                var validationResult = await _dataExportRepo.ValidateQueryAsync(config);
                if (!validationResult.Success)
                {
                    ModelState.AddModelError("", $"Configuration Failed Validation: {validationResult.Message}");
                }
                else
                {
                    await _configRepo.AddConfigurationAsync(config);
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(config);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var config = await _configRepo.GetConfigurationByIdAsync(id);
            if (config == null)
            {
                return NotFound();
            }
            return View(config);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ExportConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(config.TableName))
            {
               ModelState.AddModelError("TableName", "Table Name is required.");
            }

            if (string.IsNullOrWhiteSpace(config.CustomQuery) && string.IsNullOrWhiteSpace(config.DateColumn))
            {
                 ModelState.AddModelError("DateColumn", "Date Column is required when Custom Query is not provided.");
            }

            if (ModelState.IsValid)
            {
                // Strict Backend Validation
                var validationResult = await _dataExportRepo.ValidateQueryAsync(config);
                if (!validationResult.Success)
                {
                    ModelState.AddModelError("", $"Configuration Failed Validation: {validationResult.Message}");
                }
                else
                {
                    await _configRepo.UpdateConfigurationAsync(config);
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(config);
        }

        public async Task<IActionResult> Delete(int id)
        {
            await _configRepo.DeleteConfigurationAsync(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMultiple(int[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                await _configRepo.DeleteConfigurationsAsync(ids);
            }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult BulkImport()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> BulkImport(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Please select a file.");
                return View();
            }

            var configs = new List<ExportConfiguration>();
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                // Skip header
                await reader.ReadLineAsync();
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = ParseCsvLine(line);
                    if (values.Count >= 8)
                    {
                        configs.Add(new ExportConfiguration
                        {
                            AppId = values[0].Trim(),
                            AppName = values[1].Trim(),
                            DbServerIP = values[2].Trim(),
                            DbName = values[3].Trim(),
                            TableName = values[4].Trim(),
                            DateColumn = values[5].Trim(),
                            CustomQuery = values[6].Trim(),
                            Enabled = bool.Parse(values[7].Trim())
                        });
                    }
                }
            }

            if (configs.Any())
            {
                await _configRepo.AddConfigurationsAsync(configs);
            }

            return RedirectToAction(nameof(Index));
        }
        public IActionResult DownloadSampleCsv()
        {
            var csv = "AppId,AppName,DbServerIP,DbName,TableName,DateColumn,CustomQuery,Enabled\n" +
                      "app-001,Sales App,192.168.1.10,SalesDb,Orders,OrderDate,,true\n" +
                      "app-002,Inventory App,192.168.1.11,InventoryDb,Stock,LastUpdated,,true\n" +
                      "app-custom,Complex Report,192.168.1.12,HrDb,Employees,,\"SELECT * FROM Employees WHERE JoinDate BETWEEN @FromDate AND @ToDate\",true";
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "sample_configurations.csv");
        }

        [HttpPost]
        public async Task<IActionResult> ValidateQuery([FromBody] ValidationRequest request)
        {
            if (request == null) return BadRequest("Invalid configuration.");

            var result = await _dataExportRepo.ValidateQueryAsync(request, request.TestFromDate, request.TestToDate);
            return Ok(new { success = result.Success, message = result.Message, count = result.Count });
        }

        [HttpGet("~/api/getexporttables")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetExportTables([FromQuery] string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(new { success = false, message = "AppId is required" });
            }

            var tables = await _configRepo.GetAllExportTablesByAppIdAsync(appId);
            return Ok(tables);
        }
        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = "";
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current += '"';
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }

            result.Add(current);
            return result;
        }
        public class ValidationRequest : ExportConfiguration
        {
            public DateTime? TestFromDate { get; set; }
            public DateTime? TestToDate { get; set; }
        }
    }
}
