using System.Linq;
using DataSync.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataSync.Web.Controllers
{
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public class DashboardController : Controller
    {
        private readonly DataSyncDbContext _context;

        public DashboardController(DataSyncDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View(_context.ExportLogs.OrderByDescending(x => x.RequestTimestamp));
        }
    }
}
