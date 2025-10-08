using Microsoft.AspNetCore.Mvc;

namespace SalesAnalysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RefreshController : ControllerBase
    {
        private readonly IBackgroundJobQueue _queue;
        private readonly ICsvLoaderService _loader;

        public RefreshController(IBackgroundJobQueue queue, ICsvLoaderService loader)
        {
            _queue = queue;
            _loader = loader;
        }

        // Trigger on-demand load of a CSV file on the server
        [HttpPost("trigger")]
        public IActionResult TriggerRefresh([FromBody] TriggerRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.FilePath))
                return BadRequest("filePath is required.");

            _queue.QueueJob(async ct => await _loader.LoadCsvFileAsync(req.FilePath, ct));
            return Accepted(new { message = "Refresh queued", file = req.FilePath });
        }

        public class TriggerRequest { public string FilePath { get; set; } }
    }
}
