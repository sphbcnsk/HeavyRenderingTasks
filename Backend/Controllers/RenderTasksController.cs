using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; 
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
        private readonly TaskCancellationManager _cancellationManager;
        private readonly IConfiguration _configuration; 

        public RenderTasksController(AppDbContext context, RenderTaskQueue taskQueue, TaskCancellationManager cancellationManager, IConfiguration configuration)
        {
            _context = context;
            _taskQueue = taskQueue;
            _cancellationManager = cancellationManager;
            _configuration = configuration;
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

            if (request.CanvasWidth < 100 || request.CanvasHeight < 100)
            {
                return BadRequest(new { message = "Мінімальний розмір полотна: 100x100 пікселів." });
            }

            if (request.CanvasWidth > 5000 || request.CanvasHeight > 5000 || (long)request.CanvasWidth * request.CanvasHeight > 10_000_000)
            {
                return BadRequest(new { message = "Перевищено ліміт пам'яті. Максимальний розмір полотна — 10 мільйонів пікселів (наприклад, 4000x2500)." });
            }

            if (request.Iterations < 100 || request.Iterations > 5_000_000)
            {
                return BadRequest(new { message = "Кількість ітерацій має бути в межах від 100 до 5,000,000." });
            }

            var activeTasksCount = await _context.RenderTasks
                .CountAsync(t => t.UserId == userId && (t.Status == "В черзі" || t.Status.Contains("Малювання")));

            if (activeTasksCount >= 3)
            {
                return BadRequest(new { message = "Ви досягли ліміту активних завдань (максимум 3). Дочекайтеся завершення попередніх або скасуйте їх." });
            }

            string currentServerId = _configuration["ServerId"] ?? "UnknownServer";

            var task = new RenderTask
            {
                FigureType = request.FigureType,
                CanvasWidth = request.CanvasWidth,
                CanvasHeight = request.CanvasHeight,
                Iterations = request.Iterations,
                Status = "В черзі",
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
                ServerId = currentServerId 
            };

            _context.RenderTasks.Add(task);
            await _context.SaveChangesAsync();

            bool isQueued = _taskQueue.TryQueueBackgroundWorkItem(task);
            if (!isQueued)
            {
                _context.RenderTasks.Remove(task);
                await _context.SaveChangesAsync();

                return StatusCode(503, new { message = "Сервер наразі перевантажений великою кількістю запитів. Спробуйте пізніше." });
            }

            return Ok(new { message = "Завдання додано в чергу", taskId = task.Id });
        }

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelTask(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var task = await _context.RenderTasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (task == null) return NotFound(new { message = "Задачу не знайдено." });

            if (task.Status == "В черзі" || task.Status.Contains("Малювання") || task.Status == "Початок малювання...")
            {
                _cancellationManager.CancelTask(id);
                task.Status = "Скасовано";
                task.Progress = 0;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Задачу скасовано" });
            }

            return BadRequest(new { message = "Цю задачу вже завершено або скасовано." });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(Guid id)
        {
            var userId = GetCurrentUserId();

            var task = await _context.RenderTasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (task == null) return NotFound(new { message = "Завдання не знайдено або у вас немає доступу." });

            _cancellationManager.CancelTask(id);

            _context.RenderTasks.Remove(task);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(task.ResultImageUrl))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", task.ResultImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            return Ok();
        }
    }
}