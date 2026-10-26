using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Filters;
using RaceDayAPI.Models;

namespace RaceDayAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ParticipantsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ParticipantsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves all participants with their user details. Organiser only. Returns 200 with a list, 401 if not logged in, or 403 for a Participant.
        /// </summary>
        [HttpGet]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> GetParticipants()
        {
            var participants = await _context.Participants
                .Include(p => p.User)
                .ToListAsync();
            return Ok(participants);
        }

        /// <summary>
        /// Creates a participant profile (date of birth, gender, emergency contact) for an existing user. Organiser only. Returns 201, 400 for invalid input, 401, 403, or 500 on a server error.
        /// </summary>
        [HttpPost]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> CreateParticipant([FromBody] Participant participantDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // If the incoming DTO has a User object nested, check/save it or attach it
                if (participantDto.User != null)
                {
                    var existingUser = await _context.Users.FindAsync(participantDto.User.UserID);
                    if (existingUser != null)
                    {
                        participantDto.User = existingUser;
                        participantDto.ParticipantID = existingUser.UserID;
                    }
                    else
                    {
                        // Add the new user first if it doesn't exist
                        _context.Users.Add(participantDto.User);
                        await _context.SaveChangesAsync();
                        participantDto.ParticipantID = participantDto.User.UserID;
                    }
                }

                _context.Participants.Add(participantDto);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetParticipants), new { id = participantDto.ParticipantID }, new { message = "Participant created successfully", participantID = participantDto.ParticipantID });
            }
            catch (Exception)
            {
                // Removed exception and inner exception exposure
                return StatusCode(500, new { message = "Internal server error." });
            }
        }
    }
}