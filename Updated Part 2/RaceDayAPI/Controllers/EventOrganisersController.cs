using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using RaceDayAPI.Filters;

namespace RaceDayAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [SessionAuthorize("Organiser")]
    public class EventOrganisersController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EventOrganisersController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves all event organisers with their user details. Organiser only. Returns 200 with a list, 401 if not logged in, or 403 for a Participant.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetEventOrganisers()
        {
            var organisers = await _context.EventOrganisers
                .Include(eo => eo.User)
                .ToListAsync();
            return Ok(organisers);
        }

        /// <summary>
        /// Retrieves one event organiser by ID with their user details. Organiser only. Returns 200, or 404 if not found.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEventOrganiser(int id)
        {
            var eventOrganiser = await _context.EventOrganisers
                .Include(eo => eo.User)
                .FirstOrDefaultAsync(eo => eo.EventOrganiserID == id);

            if (eventOrganiser == null)
            {
                return NotFound(new { message = $"EventOrganiser with ID {id} not found." });
            }

            return Ok(eventOrganiser);
        }

        /// <summary>
        /// Updates an organiser's organisation name and contact. Organiser only. The ID in the route must match the body. Returns 204, 400 on ID mismatch, or 404 if not found.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEventOrganiser(int id, [FromBody] EventOrganiser eventOrganiser)
        {
            if (id != eventOrganiser.EventOrganiserID)
            {
                return BadRequest(new { message = "ID mismatch between route and payload." });
            }

            _context.Entry(eventOrganiser).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventOrganiserExists(id))
                {
                    return NotFound(new { message = $"EventOrganiser with ID {id} not found." });
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        /// <summary>
        /// Deletes an event organiser by ID. Organiser only. Returns 204, or 404 if not found.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEventOrganiser(int id)
        {
            var eventOrganiser = await _context.EventOrganisers.FindAsync(id);
            if (eventOrganiser == null)
            {
                return NotFound(new { message = $"EventOrganiser with ID {id} not found." });
            }

            _context.EventOrganisers.Remove(eventOrganiser);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool EventOrganiserExists(int id)
        {
            return _context.EventOrganisers.Any(e => e.EventOrganiserID == id);
        }
    }
}