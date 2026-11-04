using Backend.Data;
using Backend.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Services
{
    public class RenderBackgroundService : BackgroundService
    {
        private readonly RenderTaskQueue _taskQueue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IWebHostEnvironment _env;

        public RenderBackgroundService(RenderTaskQueue taskQueue, IServiceScopeFactory scopeFactory, IWebHostEnvironment env)
        {
            _taskQueue = taskQueue;
            _scopeFactory = scopeFactory;
            _env = env;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var task = await _taskQueue.DequeueAsync(stoppingToken);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var renderer = scope.ServiceProvider.GetRequiredService<CurveRenderer>();

                    var dbTask = await dbContext.RenderTasks.FindAsync(new object[] { task.Id }, stoppingToken);
                    if (dbTask != null)
                    {
                        Action<int> onProgress = (percent) =>
                        {
                            dbTask.Status = $"Малювання: {percent}%";
                            dbContext.SaveChanges(); 
                        };

                        string imageUrl = renderer.RenderCurve(task, _env.WebRootPath, onProgress);

                        dbTask.Status = "Виконано";
                        dbTask.ResultImageUrl = imageUrl;
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Помилка рендеру задачі {task.Id}: {ex.Message}");
                }
            }
        }
    }
}