using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Models;
using RaceDayAPI.Services;
using System.ComponentModel.DataAnnotations;

namespace RaceDayAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(RaceDayDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        /// <summary>
        /// Registers a new Participant or Organiser. Organiser registration requires the organiser registration code.
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (model.Role != "Organiser" && model.Role != "Participant")
                return BadRequest(new { message = "Role must be Organiser or Participant." });

            if (model.Role == "Organiser")
            {
                string? requiredCode = _configuration["Registration:OrganiserCode"];
                if (string.IsNullOrWhiteSpace(requiredCode) || model.OrganiserCode != requiredCode)
                    return StatusCode(403, new { message = "A valid organiser registration code is required." });

                if (string.IsNullOrWhiteSpace(model.OrganisationName) || string.IsNullOrWhiteSpace(model.OrganisationContact))
                    return BadRequest(new { message = "Organisation name and contact are required for Organisers." });
            }
            else
            {
                if (model.DateOfBirth == null
                    || string.IsNullOrWhiteSpace(model.Gender)
                    || string.IsNullOrWhiteSpace(model.EmergencyContactName)
                    || string.IsNullOrWhiteSpace(model.EmergencyContactNumber))
                {
                    return BadRequest(new { message = "Date of birth, gender and emergency contact details are required for Participants." });
                }

                if (model.DateOfBirth.Value.Date > DateTime.Today)
                    return BadRequest(new { message = "Date of birth cannot be in the future." });
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (existingUser != null)
                return BadRequest(new { message = "Email already exists." });

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var user = new User
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                Password = PasswordHasher.Hash(model.Password),
                PhoneNumber = model.PhoneNumber,
                Role = model.Role,
                RegistrationDate = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            if (model.Role == "Organiser")
            {
                _context.EventOrganisers.Add(new EventOrganiser
                {
                    EventOrganiserID = user.UserID,
                    OrganisationName = model.OrganisationName!,
                    OrganisationContact = model.OrganisationContact!
                });
            }
            else
            {
                _context.Participants.Add(new Participant
                {
                    ParticipantID = user.UserID,
                    DateOfBirth = model.DateOfBirth!.Value,
                    Gender = model.Gender!,
                    EmergencyContactName = model.EmergencyContactName!,
                    EmergencyContactNumber = model.EmergencyContactNumber!
                });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return StatusCode(201, new { message = "User registered successfully.", userId = user.UserID });
        }

        /// <summary>
        /// Authenticates a user using Email and Password, then establishes a session.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null)
                return Unauthorized(new { message = "Invalid email or password." });

            bool valid;
            if (PasswordHasher.IsHashed(user.Password))
            {
                valid = PasswordHasher.Verify(model.Password, user.Password);
            }
            else
            {
                // Legacy plain-text password (seed data): accept once, then store it hashed.
                valid = user.Password == model.Password;
                if (valid)
                {
                    user.Password = PasswordHasher.Hash(model.Password);
                    await _context.SaveChangesAsync();
                }
            }

            if (!valid)
                return Unauthorized(new { message = "Invalid email or password." });

            // Set session variables using exact User model properties
            HttpContext.Session.SetInt32("UserId", user.UserID);
            HttpContext.Session.SetString("Email", user.Email);
            HttpContext.Session.SetString("Role", user.Role);

            return Ok(new
            {
                message = "Login successful.",
                role = user.Role,
                userId = user.UserID,
                firstName = user.FirstName,
                lastName = user.LastName
            });
        }

        /// <summary>
        /// Logs out the current user by clearing the session.
        /// </summary>
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Ok(new { message = "Logged out successfully." });
        }
    }

    public class LoginModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterModel
    {
        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(100)]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Role { get; set; } = string.Empty;

        // Participant fields
        public DateTime? DateOfBirth { get; set; }

        [MaxLength(20)]
        public string? Gender { get; set; }

        [MaxLength(100)]
        public string? EmergencyContactName { get; set; }

        [MaxLength(20)]
        public string? EmergencyContactNumber { get; set; }

        // Organiser fields
        [MaxLength(100)]
        public string? OrganisationName { get; set; }

        [MaxLength(100)]
        public string? OrganisationContact { get; set; }

        public string? OrganiserCode { get; set; }
    }
}