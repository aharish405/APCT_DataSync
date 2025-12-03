using System.Linq;
using System.Threading.Tasks;
using DataSync.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DataSync.Web.Controllers
{
    public class SecurityController : Controller
    {
        private readonly IAuthService _authService;

        public SecurityController(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<IActionResult> Index()
        {
            var clients = await _authService.GetAllClientsAsync();
            return View(clients.AsQueryable());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(string clientName)
        {
            if (string.IsNullOrEmpty(clientName))
            {
                ModelState.AddModelError("", "Client Name is required");
                return View();
            }

            var result = await _authService.CreateClientAsync(clientName);
            ViewBag.PlainSecret = result.plainSecret;
            return View("ShowSecret", result.client);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _authService.DeleteClientAsync(id);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Tokens()
        {
            var tokens = await _authService.GetAllRefreshTokensAsync();
            return View(tokens.AsQueryable());
        }

        [HttpPost]
        public async Task<IActionResult> RevokeToken(int id)
        {
            await _authService.RevokeRefreshTokenByIdAsync(id);
            return RedirectToAction(nameof(Tokens));
        }
    }
}
