using Microsoft.AspNetCore.Mvc;

namespace DocumentVerificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { status = "API is running", timestamp = DateTime.UtcNow });
        }
    }
}