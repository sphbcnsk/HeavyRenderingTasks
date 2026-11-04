using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RenderTasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RenderTaskQueue _taskQueue;

        public RenderTasksController(AppDbContext context, RenderTaskQueue taskQueue)
        {
            _context = context;
            _taskQueue = taskQueue;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] RenderTask task)
        {
            long complexity = (long)task.CanvasWidth * task.CanvasHeight * task.Iterations;
            long maxComplexity = 10_000_000_000_000;

            if (complexity > maxComplexity)
            {
                return BadRequest($"Відмова у виконанні: завдання занадто трудомістке. Максимально дозволена складність — {maxComplexity}, ваша — {complexity}.");
            }

            task.Status = "В черзі";

            _context.RenderTasks.Add(task);
            await _context.SaveChangesAsync();

            await _taskQueue.QueueBackgroundWorkItemAsync(task);

            return Ok(new { message = "Завдання додано в чергу", taskId = task.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTasks()
        {
            var tasks = await _context.RenderTasks.OrderByDescending(t => t.Id).ToListAsync();
            return Ok(tasks);
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(Guid id) 
        {
            var task = await _context.RenderTasks.FindAsync(id);
            if (task == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(task.ResultImageUrl))
            {
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", task.ResultImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.RenderTasks.Remove(task);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Завдання видалено" });
        }
    }
}