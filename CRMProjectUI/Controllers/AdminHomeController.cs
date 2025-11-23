using Microsoft.AspNetCore.Mvc;

namespace CRMProjectUI.Controllers
{
    public class AdminHomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}