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

        public DataCopyController(
            IDataCopyConfigRepository configRepo,
            IDataCopyJobRepository jobRepo,
            IDataCopyService copyService,
            DataSyncDbContext dbContext)
        {
            _configRepo = configRepo;
            _jobRepo = jobRepo;
            _copyService = copyService;
            _dbContext = dbContext;
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

            var result = await _configRepo.ValidateConfigurationAsync(config);
            return Ok(new { success = result.Success, message = result.Message });
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
    }
}
