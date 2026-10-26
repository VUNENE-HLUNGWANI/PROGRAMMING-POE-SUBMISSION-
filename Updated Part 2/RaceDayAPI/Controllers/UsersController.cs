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
    public class UsersController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public UsersController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves all users. Organiser only. Password hashes are never returned. Returns 200 with a list, 401 if not logged in, or 403 for a Participant.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        /// <summary>
        /// Retrieves one user by ID. Organiser only. Returns 200, or 404 if not found.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound(new { message = $"User with ID {id} not found." });
            }

            return user;
        }

        /// <summary>
        /// Updates a user's details. Organiser only. The ID in the route must match the body, and the password is never changed here. Returns 204, 400 on ID mismatch, or 404 if not found.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            if (id != user.UserID)
            {
                return BadRequest(new { message = "ID mismatch between route and payload." });
            }

            _context.Entry(user).State = EntityState.Modified;

            // Prevent EF Core from overwriting the password hash with null/empty on updates
            _context.Entry(user).Property(u => u.Password).IsModified = false;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound(new { message = $"User with ID {id} not found." });
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        /// <summary>
        /// Deletes a user by ID. Organiser only. Returns 204, or 404 if not found.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = $"User with ID {id} not found." });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.UserID == id);
        }
    }
}