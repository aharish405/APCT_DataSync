using DataSync.Core.Interfaces;
using DataSync.Core.Models.DataCopy;
using DataSync.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DataSync.Web.Controllers
{
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public class DataCopyController : Controller
    {
        private readonly IDataCopyConfigRepository _configRepo;
        private readonly IDataCopyJobRepository _jobRepo;
        private readonly IDataCopyService _copyService;
        private readonly DataSyncDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public DataCopyController(
            IDataCopyConfigRepository configRepo,
            IDataCopyJobRepository jobRepo,
            IDataCopyService copyService,
            DataSyncDbContext dbContext,
            IConfiguration configuration)
        {
            _configRepo = configRepo;
            _jobRepo = jobRepo;
            _copyService = copyService;
            _dbContext = dbContext;
            _configuration = configuration;
        }

        // GET: DataCopy
        public IActionResult Index()
        {
            var configs = _dbContext.DataCopyConfigurations.AsQueryable();
            return View(configs);
        }

        // GET: DataCopy/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: DataCopy/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DataCopyConfiguration config)
        {
            if (ModelState.IsValid)
            {
                var validation = await _configRepo.ValidateConfigurationAsync(config);
                if (!validation.Success)
                {
                    ModelState.AddModelError("", validation.Message);
                }
                else
                {
                    await _configRepo.AddConfigurationAsync(config);
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(config);
        }

        // GET: DataCopy/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var config = await _configRepo.GetConfigurationByIdAsync(id);
            if (config == null)
            {
                return NotFound();
            }
            return View(config);
        }

        // POST: DataCopy/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(DataCopyConfiguration config)
        {
            if (ModelState.IsValid)
            {
                var validation = await _configRepo.ValidateConfigurationAsync(config);
                if (!validation.Success)
                {
                    ModelState.AddModelError("", validation.Message);
                }
                else
                {
                    await _configRepo.UpdateConfigurationAsync(config);
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(config);
        }

        // GET: DataCopy/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            await _configRepo.DeleteConfigurationAsync(id);
            return RedirectToAction(nameof(Index));
        }

        // POST: DataCopy/TriggerCopy/5
        [HttpPost]
        public async Task<IActionResult> TriggerCopy(int id)
        {
            var result = await _copyService.ExecuteCopyJobAsync(id, DataCopyTriggerType.Manual);
            if (result.Success)
            {
                TempData["Success"] = $"Copy job started successfully. Job ID: {result.JobId}";
            }
            else
            {
                TempData["Error"] = result.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: DataCopy/ValidateConfig
        [HttpPost]
        public async Task<IActionResult> ValidateConfig([FromBody] DataCopyConfiguration config)
        {
            if (config == null) return BadRequest("Invalid configuration.");

            try
            {
                // Basic field validation first
                var basicValidation = await _configRepo.ValidateConfigurationAsync(config);
                if (!basicValidation.Success)
                    return Ok(new { success = false, message = basicValidation.Message });

                // Build connection strings using helper
                var connHelper = new DataSync.Data.Helpers.ConnectionStringHelper(_configuration);
                var sourceConnString = connHelper.BuildConnectionString(config.SourceDbServerIP, config.SourceDbName);
                var destConnString = connHelper.BuildConnectionString(config.DestDbServerIP, config.DestDbName);

                // Create execution repository for testing
                var execRepo = new DataSync.Data.Repositories.DataCopyExecutionRepository();

                // Test source connection
                var sourceTest = await execRepo.TestConnectionAsync(sourceConnString);
                if (!sourceTest.Success)
                    return Ok(new { success = false, message = $"Cannot connect to source database: {sourceTest.ErrorMessage}. Check server IP, database name, and credentials in appsettings.json." });

                // Test destination connection
                var destTest = await execRepo.TestConnectionAsync(destConnString);
                if (!destTest.Success)
                    return Ok(new { success = false, message = $"Cannot connect to destination database: {destTest.ErrorMessage}. Check server IP, database name, and credentials in appsettings.json." });

                // Verify source table exists
                if (!await execRepo.TableExistsAsync(sourceConnString, config.SourceTableName))
                    return Ok(new { success = false, message = $"Source table '{config.SourceTableName}' does not exist." });

                // Verify destination table exists
                if (!await execRepo.TableExistsAsync(destConnString, config.DestTableName))
                    return Ok(new { success = false, message = $"Destination table '{config.DestTableName}' does not exist." });

                return Ok(new { success = true, message = "Configuration is valid and ready to use." });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Validation error: {ex.Message}" });
            }
        }

        // GET: DataCopy/Dashboard
        public IActionResult Dashboard()
        {
            var recentJobs = _dbContext.DataCopyJobs
                .Include(j => j.Configuration)
                .OrderByDescending(j => j.CreatedDate)
                .Take(50)
                .AsQueryable();
            
            return View(recentJobs);
        }

        // GET: DataCopy/JobStatus/5
        [HttpGet]
        public async Task<IActionResult> JobStatus(int id)
        {
            var job = await _jobRepo.GetJobByIdAsync(id);
            if (job == null)
                return NotFound();

            return Ok(new
            {
                id = job.Id,
                status = job.Status.ToString(),
                totalRecords = job.TotalRecords,
                processedRecords = job.ProcessedRecords,
                failedRecords = job.FailedRecords,
                progressPercentage = job.ProgressPercentage,
                startTime = job.StartTime,
                endTime = job.EndTime,
                duration = job.Duration,
                errorMessage = job.ErrorMessage
            });
        }

        // GET: DataCopy/PreviewCron
        [HttpGet]
        public IActionResult PreviewCron(string expression)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(expression))
                    return Ok(new { success = false, message = "Cron expression is required" });

                var cronExpression = Cronos.CronExpression.Parse(expression);
                var nextExecutions = new List<DateTime>();
                var baseTime = DateTime.UtcNow;

                for (int i = 0; i < 5; i++)
                {
                    var next = cronExpression.GetNextOccurrence(baseTime, TimeZoneInfo.Local);
                    if (next.HasValue)
                    {
                        nextExecutions.Add(next.Value);
                        baseTime = next.Value;
                    }
                }

                return Ok(new { success = true, nextExecutions });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        // GET: DataCopy/JobLogs/5
        [HttpGet]
        public async Task<IActionResult> JobLogs(int id)
        {
            var logs = await _jobRepo.GetJobLogsAsync(id);
            return Json(logs);
        }

        // POST: DataCopy/ResumeJob
        [HttpPost]
        public async Task<IActionResult> ResumeJob(int id)
        {
            var result = await _copyService.ResumeJobAsync(id);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }
            return RedirectToAction("Dashboard");
        }
    }
}
