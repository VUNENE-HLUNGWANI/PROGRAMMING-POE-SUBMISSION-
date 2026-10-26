using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using RaceDayAPI.Filters;

namespace RaceDayAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PerformanceHistoriesController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public PerformanceHistoriesController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves all participant performance histories (best time, total wins, best position, last race, average position). Public. Returns 200 with a list.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPerformanceHistories()
        {
            var histories = await _context.PerformanceHistories
                .Include(ph => ph.Participant)
                .ToListAsync();
            return Ok(histories);
        }

        /// <summary>
        /// Retrieves one performance history by ID. Public. Returns 200, or 404 if not found.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPerformanceHistory(int id)
        {
            var history = await _context.PerformanceHistories
                .Include(ph => ph.Participant)
                .FirstOrDefaultAsync(ph => ph.PerformanceHistoryID == id);

            if (history == null)
            {
                return NotFound(new { message = "Performance history not found." });
            }

            return Ok(history);
        }

        /// <summary>
        /// Creates a performance history record for a participant. Organiser only. Returns 201, 400 for invalid input, 401 if not logged in, 403 for a Participant, or 500 on a server error.
        /// </summary>
        [HttpPost]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> PostPerformanceHistory([FromBody] PerformanceHistory performanceHistory)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _context.PerformanceHistories.Add(performanceHistory);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetPerformanceHistory), new { id = performanceHistory.PerformanceHistoryID }, performanceHistory);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Internal server error." });
            }
        }

        /// <summary>
        /// Updates a performance history record. Organiser only. The ID in the route must match the body. Returns 204, 400 on ID mismatch, 401, 403, or 404 if not found.
        /// </summary>
        [HttpPut("{id}")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> PutPerformanceHistory(int id, [FromBody] PerformanceHistory performanceHistory)
        {
            if (id != performanceHistory.PerformanceHistoryID)
            {
                return BadRequest(new { message = "ID mismatch." });
            }

            _context.Entry(performanceHistory).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.PerformanceHistories.Any(e => e.PerformanceHistoryID == id))
                {
                    return NotFound(new { message = "Performance history not found." });
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        /// <summary>
        /// Deletes a performance history record. Organiser only. Returns 204, 401, 403, or 404 if not found.
        /// </summary>
        [HttpDelete("{id}")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> DeletePerformanceHistory(int id)
        {
            var history = await _context.PerformanceHistories.FindAsync(id);
            if (history == null)
            {
                return NotFound(new { message = "Performance history not found." });
            }

            _context.PerformanceHistories.Remove(history);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}