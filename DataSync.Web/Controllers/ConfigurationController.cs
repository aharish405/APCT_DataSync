using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataSync.Core.Interfaces;
using DataSync.Core.Models;
using DataSync.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DataSync.Web.Controllers
{
    public class ConfigurationController : Controller
    {
        private readonly IConfigurationRepository _configRepo;
        private readonly DataSyncDbContext _context;

        public ConfigurationController(IConfigurationRepository configRepo, DataSyncDbContext context)
        {
            _configRepo = configRepo;
            _context = context;
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
            if (ModelState.IsValid)
            {
                await _configRepo.AddConfigurationAsync(config);
                return RedirectToAction(nameof(Index));
            }
            return View(config);
        }

        public async Task<IActionResult> Edit(string appId)
        {
            var config = await _configRepo.GetConfigurationAsync(appId);
            if (config == null)
            {
                return NotFound();
            }
            return View(config);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ExportConfiguration config)
        {
            if (ModelState.IsValid)
            {
                await _configRepo.UpdateConfigurationAsync(config);
                return RedirectToAction(nameof(Index));
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
                    var values = line.Split(',');
                    if (values.Length >= 7)
                    {
                        configs.Add(new ExportConfiguration
                        {
                            AppId = values[0].Trim(),
                            AppName = values[1].Trim(),
                            DbServerIP = values[2].Trim(),
                            DbName = values[3].Trim(),
                            TableName = values[4].Trim(),
                            DateColumn = values[5].Trim(),
                            Enabled = bool.Parse(values[6].Trim())
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
            var csv = "AppId,AppName,DbServerIP,DbName,TableName,DateColumn,Enabled\n" +
                      "app-001,Sales App,192.168.1.10,SalesDb,Orders,OrderDate,true\n" +
                      "app-002,Inventory App,192.168.1.11,InventoryDb,Stock,LastUpdated,true";
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "sample_configurations.csv");
        }

        [HttpGet("~/api/getexporttables")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetExportTables()
        {
            var tables = await _configRepo.GetAllExportTablesAsync();
            return Ok(tables);
        }
    }
}
