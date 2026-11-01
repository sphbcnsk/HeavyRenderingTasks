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
           // long complexity = (long)task.CanvasWidth * task.CanvasHeight * task.Iterations;
           // if (complexity > 100_000_000_000)
            //{
            //    return BadRequest("Завдання занадто важке.");
           // }

            task.Status = "В черзі";

            _context.RenderTasks.Add(task);
            await _context.SaveChangesAsync(); 

        
            await _taskQueue.QueueBackgroundWorkItemAsync(task);

            return Ok(new { message = "Завдання додано в чергу", taskId = task.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTasks()
        {
            var tasks = await _context.RenderTasks.ToListAsync();
            return Ok(tasks);
        }
    }
}