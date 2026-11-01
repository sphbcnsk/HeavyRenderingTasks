using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RenderTasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly CurveRenderer _renderer;
        private readonly IWebHostEnvironment _env;

        public RenderTasksController(AppDbContext context, CurveRenderer renderer, IWebHostEnvironment env)
        {
            _context = context;
            _renderer = renderer;
            _env = env;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] RenderTask task)
        {
            long complexity = (long)task.CanvasWidth * task.CanvasHeight * task.Iterations;
            if (complexity > 10_000_000_000)
            {
                return BadRequest("Завдання занадто важке. Зменште роздільну здатність або кількість ітерацій.");
            }

            string imageUrl = _renderer.RenderCurve(task, _env.WebRootPath);

            task.ResultImageUrl = imageUrl;
            task.Status = "Виконано";

            _context.RenderTasks.Add(task);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Завдання успішно виконано", taskId = task.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTasks()
        {
            var tasks = await _context.RenderTasks.ToListAsync();
            return Ok(tasks);
        }
    }
}