using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace studentsData.Controllers
{
    [ApiController]
    [Route("api/debug")]
    public class DebugController : Controller
    {
        [HttpGet("me")]
        public IActionResult Me()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            var isAuth = User?.Identity?.IsAuthenticated ?? false;
            var isInRoleAdmin = User.IsInRole("Admin"); // checks the runtime role mapping
            return Ok(new
            {
                isAuthenticated = isAuth,
                isInRoleAdmin,
                claims
            });
        }
    }
}
