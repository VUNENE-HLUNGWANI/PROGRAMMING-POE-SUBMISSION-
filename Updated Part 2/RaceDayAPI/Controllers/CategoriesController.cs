using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDayAPI.Data;
using RaceDayAPI.Filters;
using RaceDayAPI.Models;

namespace RaceDayAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public CategoriesController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves all categories across all events, each with its parent event. Returns 200 with a list.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Include(c => c.Event)
                .ToListAsync();

            return Ok(categories);
        }

        /// <summary>
        /// Retrieves one category by ID with its parent event. Returns 200, or 404 if the category does not exist.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
            {
                return NotFound(new { message = "Category not found." });
            }

            return Ok(category);
        }

        /// <summary>
        /// Adds a category (name, distance, level, entry fee, max participants, eventID) to one of the Organiser's own events. Returns 201, 400 for invalid input, or 403 if the event belongs to another Organiser.
        /// </summary>
        [HttpPost]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> CreateCategory([FromBody] Category categoryDto)
        {
            try
            {
                // Verify if the parent event exists
                var raceEvent = await _context.Events
                    .FindAsync(categoryDto.EventID);

                if (raceEvent == null)
                {
                    return BadRequest(new { message = "Invalid EventID. Event does not exist." });
                }

                if (raceEvent.EventOrganiserID != HttpContext.Session.GetInt32("UserId"))
                {
                    return StatusCode(403, new { message = "You can only add categories to your own events." });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _context.Categories.Add(categoryDto);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCategory), new { id = categoryDto.CategoryID }, new { message = "Category created successfully", categoryId = categoryDto.CategoryID });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// Deletes a category from one of the Organiser's own events. Returns 200, 403 for another Organiser's event, 404 if not found, or 409 if the category has enrolments.
        /// </summary>
        [HttpDelete("{id}")]
        [SessionAuthorize("Organiser")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (category == null)
                return NotFound(new { message = "Category not found." });

            if (category.Event!.EventOrganiserID != HttpContext.Session.GetInt32("UserId"))
                return StatusCode(403, new { message = "You can only delete categories on your own events." });

            try
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { message = "This category has enrolments and cannot be deleted." });
            }

            return Ok(new { message = "Category deleted successfully." });
        }
    }
}