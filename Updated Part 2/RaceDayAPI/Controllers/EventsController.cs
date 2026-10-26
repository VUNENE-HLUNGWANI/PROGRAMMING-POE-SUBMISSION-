using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Filters;
using RaceDayAPI.Models;

namespace RaceDayAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EventsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves all scheduled racing events with their categories.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Event>>> GetEvents()
        {
            return await _context.Events.Include(e => e.Categories).ToListAsync();
        }

        /// <summary>
        /// Retrieves all events created by the logged-in Organiser.
        /// </summary>
        [HttpGet("mine")]
        [SessionAuthorize("Organiser")]
        public async Task<ActionResult<IEnumerable<Event>>> GetMyEvents()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            return await _context.Events
                .Include(e => e.Categories)
                .Where(e => e.EventOrganiserID == userId)
                .OrderBy(e => e.Date)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves a specific event by ID, including its categories.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Event>> GetEvent(int id)
        {
            var raceEvent = await _context.Events
                .Include(e => e.Categories)
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (raceEvent == null)
                return NotFound(new { message = "Event not found." });

            return raceEvent;
        }

        /// <summary>
        /// Creates a new event. Restricted to Organiser roles via Session verification.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] Event raceEvent)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Organiser")
            {
                return StatusCode(403, new { message = "Access denied. Organiser role required." });
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            raceEvent.EventOrganiserID = HttpContext.Session.GetInt32("UserId") ?? 0;

            _context.Events.Add(raceEvent);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetEvent), new { id = raceEvent.EventID }, raceEvent);

        }

        /// <summary>
        /// Updates an event owned by the logged-in Organiser.
        /// </summary>
        [HttpPut("{id}")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] Event updated)
        {
            var raceEvent = await _context.Events.FindAsync(id);
            if (raceEvent == null)
                return NotFound(new { message = "Event not found." });

            if (raceEvent.EventOrganiserID != HttpContext.Session.GetInt32("UserId"))
                return StatusCode(403, new { message = "You can only edit your own events." });

            raceEvent.Name = updated.Name;
            raceEvent.Description = updated.Description;
            raceEvent.Date = updated.Date;
            raceEvent.Location = updated.Location;
            raceEvent.StartTime = updated.StartTime;
            raceEvent.Routemap = updated.Routemap;
            raceEvent.EventType = updated.EventType;
            raceEvent.RegistrationDeadline = updated.RegistrationDeadline;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Event updated successfully." });
        }

        /// <summary>
        /// Deletes an event. Restricted to Organiser roles.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Organiser")
            {
                return StatusCode(403, new { message = "Access denied. Organiser role required." });
            }

            var raceEvent = await _context.Events.FindAsync(id);
            if (raceEvent == null)
                return NotFound(new { message = "Event not found." });

            if (raceEvent.EventOrganiserID != HttpContext.Session.GetInt32("UserId"))
                return StatusCode(403, new { message = "You can only delete your own events." });

            try
            {
                _context.Events.Remove(raceEvent);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { message = "This event has enrolments and cannot be deleted." });
            }

            return Ok(new { message = "Event deleted successfully." });
        }
    }
}