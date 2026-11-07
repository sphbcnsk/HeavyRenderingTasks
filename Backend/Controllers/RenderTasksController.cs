using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RenderTasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RenderTaskQueue _taskQueue;

        public RenderTasksController(AppDbContext context, RenderTaskQueue taskQueue)
        {
            _context = context;
            _taskQueue = taskQueue;
        }

        private int GetCurrentUserId()
        {
            var userIdStr = User.FindFirst("userId")?.Value
                         ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("id")?.Value;

            if (int.TryParse(userIdStr, out int userId))
            {
                return userId;
            }
            return 0; 
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks()
        {
            var userId = GetCurrentUserId();

            var tasks = await _context.RenderTasks
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    id = t.Id,
                    figureType = t.FigureType,
                    canvasWidth = t.CanvasWidth,
                    canvasHeight = t.CanvasHeight,
                    iterations = t.Iterations,
                    status = t.Status,
                    progress = t.Progress,
                    resultImageUrl = t.ResultImageUrl
                })
                .ToListAsync();

            return Ok(tasks);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] RenderTask request)
        {
            var userId = GetCurrentUserId();

            if (userId == 0)
            {
                return Unauthorized(new { message = "Помилка токена: неможливо визначити ваш ID. Спробуйте вийти з акаунта і зайти знову." });
            }

            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                return Unauthorized(new { message = "Ваш обліковий запис не знайдено. Будь ласка, перелогіньтеся." });
            }

            var task = new RenderTask
            {
                FigureType = request.FigureType,
                CanvasWidth = request.CanvasWidth,
                CanvasHeight = request.CanvasHeight,
                Iterations = request.Iterations,
                Status = "В черзі",
                CreatedAt = DateTime.UtcNow,
                UserId = userId 
            };

            _context.RenderTasks.Add(task);
            await _context.SaveChangesAsync();

            await _taskQueue.QueueBackgroundWorkItemAsync(task);

            return Ok(new { message = "Завдання додано в чергу", taskId = task.Id });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(Guid id)
        {
            var userId = GetCurrentUserId();

            var task = await _context.RenderTasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (task == null) return NotFound("Завдання не знайдено або у вас немає доступу.");

            _context.RenderTasks.Remove(task);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(task.ResultImageUrl))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", task.ResultImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Exists(filePath); 
                    System.IO.File.Delete(filePath);
                }
            }

            return Ok();
        }
    }
}