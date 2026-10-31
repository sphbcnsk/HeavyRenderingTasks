using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RenderTasksController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RenderTasksController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] RenderTask task)
        {
            
            long complexity = (long)task.CanvasWidth * task.CanvasHeight * task.Iterations;
            if (complexity > 10_000_000_000)
            {
                return BadRequest("Завдання занадто важке. Зменште роздільну здатність або кількість ітерацій.");
            }

            task.Status = "В черзі";

            _context.RenderTasks.Add(task);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Завдання успішно створено", taskId = task.Id });
        }
    }
}