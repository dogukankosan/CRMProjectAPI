using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMProjectUI.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        [AllowAnonymous]
        public IActionResult Index(int statusCode)
        {
            return statusCode switch
            {
                404 => View("Error404"),
                _ => View("Error404")
            };
        }
    }
}
