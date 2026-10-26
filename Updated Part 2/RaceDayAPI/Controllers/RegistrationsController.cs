using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Filters;
using RaceDayAPI.Models;
using System.ComponentModel.DataAnnotations;

namespace RaceDayAPI.Controllers
{
    [Route("api/enrolments")]
    [ApiController]
    public class RegistrationsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public RegistrationsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves all enrolments for events owned by the logged-in Organiser. Organiser only. Returns 200 with a list, 401 if not logged in, or 403 for a Participant.
        /// </summary>
        [HttpGet]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> GetRegistrations()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            var registrations = await _context.ParticipantEvents
                .Include(pe => pe.Participant)
                .Include(pe => pe.Event)
                .Include(pe => pe.Category)
                .Where(pe => pe.Event!.EventOrganiserID == userId)
                .ToListAsync();

            return Ok(registrations);
        }

        /// <summary>
        /// Retrieves the logged-in Participant's own enrolments with event, category, bib and status. Participant only. Returns 200 with a list, 401, or 403 for an Organiser.
        /// </summary>
        [HttpGet("mine")]
        [SessionAuthorize("Participant")]
        public async Task<IActionResult> GetMyRegistrations()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            var registrations = await _context.ParticipantEvents
                .Include(pe => pe.Event)
                .Include(pe => pe.Category)
                .Where(pe => pe.ParticipantID == userId)
                .OrderBy(pe => pe.Event!.Date)
                .ToListAsync();

            return Ok(registrations);
        }

        /// <summary>
        /// Retrieves one enrolment by ID with participant, event and category. Any logged-in user. Returns 200, 401 if not logged in, or 404 if not found.
        /// </summary>
        [HttpGet("{id}")]
        [SessionAuthorize]
        public async Task<IActionResult> GetRegistration(int id)
        {
            var registration = await _context.ParticipantEvents
                .Include(pe => pe.Participant)
                .Include(pe => pe.Event)
                .Include(pe => pe.Category)
                .FirstOrDefaultAsync(pe => pe.ParticipantEventID == id);

            if (registration == null)
            {
                return NotFound(new { message = "Registration not found." });
            }

            return Ok(registration);
        }

        /// <summary>
        /// Retrieves all enrolments for one event, including each participant's name and selected category. Organiser only, own events. Returns 200, 403 for another Organiser's event, or 404 if the event does not exist.
        /// </summary>
        [HttpGet("/api/events/{eventId}/enrolments")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> GetRegistrationsByEvent(int eventId)
        {
            var raceEvent = await _context.Events.FindAsync(eventId);
            if (raceEvent == null)
                return NotFound(new { message = "Event not found." });

            if (raceEvent.EventOrganiserID != HttpContext.Session.GetInt32("UserId"))
                return StatusCode(403, new { message = "You can only view enrolments for your own events." });

            var registrations = await _context.ParticipantEvents
                .Include(pe => pe.Participant)
                    .ThenInclude(p => p!.User)
                .Include(pe => pe.Category)
                .Where(pe => pe.EventID == eventId)
                .ToListAsync();

            return Ok(registrations);
        }

        /// <summary>
        /// Enrols the logged-in Participant in an event. Expects eventID and categoryID. The enrolment is saved as Pending with an auto-generated bib number. Participant only. Returns 201, 400 (deadline passed, or invalid event or category), 401, 403, or 409 (already enrolled, or category full).
        /// </summary>
        [HttpPost]
        [SessionAuthorize("Participant")]
        public async Task<IActionResult> CreateRegistration([FromBody] EnrolmentRequest request)
        {
            try
            {
                int participantId = HttpContext.Session.GetInt32("UserId") ?? 0;

                var participant = await _context.Participants.FindAsync(participantId);
                if (participant == null)
                {
                    return BadRequest(new { message = "Participant profile does not exist." });
                }

                var raceEvent = await _context.Events.FindAsync(request.EventID);
                if (raceEvent == null)
                {
                    return BadRequest(new { message = "Invalid EventID. Event does not exist." });
                }

                // Validate registration deadline
                if (DateTime.Now > raceEvent.RegistrationDeadline)
                {
                    return BadRequest(new { message = "Registration deadline for this event has passed." });
                }

                var category = await _context.Categories.FindAsync(request.CategoryID);
                if (category == null)
                {
                    return BadRequest(new { message = "Invalid CategoryID. Category does not exist." });
                }

                if (category.EventID != request.EventID)
                {
                    return BadRequest(new { message = "Category does not belong to this event." });
                }

                var alreadyRegistered = await _context.ParticipantEvents.AnyAsync(pe =>
                    pe.ParticipantID == participantId &&
                    pe.EventID == request.EventID);

                if (alreadyRegistered)
                {
                    return Conflict(new { message = "Participant is already registered for this event." });
                }

                if (category.MaxParticipants > 0)
                {
                    int categoryCount = await _context.ParticipantEvents.CountAsync(pe => pe.CategoryID == request.CategoryID);
                    if (categoryCount >= category.MaxParticipants)
                    {
                        return Conflict(new { message = "This category is full." });
                    }
                }

                int eventCount = await _context.ParticipantEvents.CountAsync(pe => pe.EventID == request.EventID);

                var registration = new ParticipantEvent
                {
                    EventID = request.EventID,
                    CategoryID = request.CategoryID,
                    ParticipantID = participantId,
                    Bib = (eventCount + 1).ToString("D4"),
                    RegistrationDate = DateTime.Now,
                    RegistrationStatus = "Pending" // Default status set to Pending
                };

                _context.ParticipantEvents.Add(registration);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetRegistration), new { id = registration.ParticipantEventID }, new { message = "Registration created successfully", participantEventId = registration.ParticipantEventID });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Internal server error." });
            }
        }

        /// <summary>
        /// Sets an enrolment status to Pending, Confirmed or Cancelled. Organiser only, own events. Returns 200, 400 for an invalid status, 403 for another Organiser's event, or 404 if not found.
        /// </summary>
        [HttpPut("{id}/status")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] EnrolmentStatusRequest request)
        {
            string[] allowed = { "Pending", "Confirmed", "Cancelled" };
            string? status = allowed.FirstOrDefault(s => string.Equals(s, request.Status, StringComparison.OrdinalIgnoreCase));

            if (status == null)
                return BadRequest(new { message = "Status must be Pending, Confirmed or Cancelled." });

            var registration = await _context.ParticipantEvents
                .Include(pe => pe.Event)
                .FirstOrDefaultAsync(pe => pe.ParticipantEventID == id);

            if (registration == null)
                return NotFound(new { message = "Registration not found." });

            if (registration.Event!.EventOrganiserID != HttpContext.Session.GetInt32("UserId"))
                return StatusCode(403, new { message = "You can only manage enrolments for your own events." });

            registration.RegistrationStatus = status;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Status updated.", status });
        }
    }

    /// <summary>
    /// Request body for enrolling in an event.
    /// </summary>
    public class EnrolmentRequest
    {
        /// <summary>The event to enrol in.</summary>
        public int EventID { get; set; }

        /// <summary>The category chosen for that event.</summary>
        public int CategoryID { get; set; }
    }

    /// <summary>
    /// Request body for changing an enrolment status.
    /// </summary>
    public class EnrolmentStatusRequest
    {
        /// <summary>New status: Pending, Confirmed or Cancelled.</summary>
        [Required]
        public string Status { get; set; } = string.Empty;
    }
}