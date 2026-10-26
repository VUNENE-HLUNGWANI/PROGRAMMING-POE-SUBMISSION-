using Microsoft.AspNetCore.Mvc;
using RaceDayAPI.Data;
using RaceDayAPI.Filters;
using System.ComponentModel.DataAnnotations;

namespace RaceDayAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [SessionAuthorize]
    public class ProfileController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ProfileController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns the logged-in user's profile (participant or organiser details included).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found." });

            var participant = await _context.Participants.FindAsync(userId);
            var organiser = await _context.EventOrganisers.FindAsync(userId);

            return Ok(new ProfileModel
            {
                UserID = user.UserID,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                DateOfBirth = participant?.DateOfBirth,
                Gender = participant?.Gender,
                EmergencyContactName = participant?.EmergencyContactName,
                EmergencyContactNumber = participant?.EmergencyContactNumber,
                OrganisationName = organiser?.OrganisationName,
                OrganisationContact = organiser?.OrganisationContact
            });
        }

        /// <summary>
        /// Updates the logged-in user's personal information. Email cannot be changed.
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] ProfileModel model)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            string? role = HttpContext.Session.GetString("Role");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found." });

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;

            if (role == "Participant")
            {
                var participant = await _context.Participants.FindAsync(userId);
                if (participant == null)
                    return NotFound(new { message = "Participant profile not found." });

                if (model.DateOfBirth == null
                    || string.IsNullOrWhiteSpace(model.Gender)
                    || string.IsNullOrWhiteSpace(model.EmergencyContactName)
                    || string.IsNullOrWhiteSpace(model.EmergencyContactNumber))
                {
                    return BadRequest(new { message = "Date of birth, gender and emergency contact details are required." });
                }

                if (model.DateOfBirth.Value.Date > DateTime.Today)
                    return BadRequest(new { message = "Date of birth cannot be in the future." });

                participant.DateOfBirth = model.DateOfBirth.Value;
                participant.Gender = model.Gender;
                participant.EmergencyContactName = model.EmergencyContactName;
                participant.EmergencyContactNumber = model.EmergencyContactNumber;
            }
            else if (role == "Organiser")
            {
                var organiser = await _context.EventOrganisers.FindAsync(userId);
                if (organiser == null)
                    return NotFound(new { message = "Organiser profile not found." });

                if (string.IsNullOrWhiteSpace(model.OrganisationName) || string.IsNullOrWhiteSpace(model.OrganisationContact))
                    return BadRequest(new { message = "Organisation name and contact are required." });

                organiser.OrganisationName = model.OrganisationName;
                organiser.OrganisationContact = model.OrganisationContact;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Profile updated successfully." });
        }
    }

    public class ProfileModel
    {
        public int UserID { get; set; }

        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        public string? Email { get; set; }

        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Role { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [MaxLength(20)]
        public string? Gender { get; set; }

        [MaxLength(100)]
        public string? EmergencyContactName { get; set; }

        [MaxLength(20)]
        public string? EmergencyContactNumber { get; set; }

        [MaxLength(100)]
        public string? OrganisationName { get; set; }

        [MaxLength(100)]
        public string? OrganisationContact { get; set; }
    }
}