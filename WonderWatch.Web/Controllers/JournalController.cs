using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WonderWatch.Application.Interfaces;

namespace WonderWatch.Web.Controllers
{
    [Route("api/journal")]
    [ApiController]
    public class JournalController : ControllerBase
    {
        private readonly IJournalService _journalService;

        public JournalController(IJournalService journalService)
        {
            _journalService = journalService;
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] JournalSubscribeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Email))
                return BadRequest(new { success = false, message = "Email is required." });

            var result = await _journalService.SubscribeAsync(request.Email);

            if (result)
                return Ok(new { success = true, message = "Welcome to The Wonder Watch Journal." });

            return Ok(new { success = false, message = "This email is already subscribed." });
        }
    }

    public class JournalSubscribeRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}
