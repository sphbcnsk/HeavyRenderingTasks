using Backend.Data;
using Backend.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Services
{
    public class RenderBackgroundService : BackgroundService
    {
        private readonly RenderTaskQueue _taskQueue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IWebHostEnvironment _env;
        private readonly TaskCancellationManager _cancellationManager;

        public RenderBackgroundService(RenderTaskQueue taskQueue, IServiceScopeFactory scopeFactory, IWebHostEnvironment env, TaskCancellationManager cancellationManager)
        {
            _taskQueue = taskQueue;
            _scopeFactory = scopeFactory;
            _env = env;
            _cancellationManager = cancellationManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RestoreStuckTasksAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var task = await _taskQueue.DequeueAsync(stoppingToken);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _cancellationManager.RegisterTask(task.Id, cts);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var renderer = scope.ServiceProvider.GetRequiredService<CurveRenderer>();

                    var dbTask = await dbContext.RenderTasks.FindAsync(new object[] { task.Id }, stoppingToken);
                    if (dbTask != null)
                    {
                        try
                        {
                            int lastSavedProgress = 0;

                            Action<int> onProgress = (percent) =>
                            {
                                dbTask.Status = $"Малювання: {percent}%";
                                dbTask.Progress = percent;

                                if (percent - lastSavedProgress >= 5 || percent == 100)
                                {
                                    lastSavedProgress = percent;
                                    dbContext.SaveChanges();
                                }
                            };

                            dbTask.Status = "Початок малювання...";
                            await dbContext.SaveChangesAsync(stoppingToken);

                            string imageUrl = renderer.RenderCurve(task, _env.WebRootPath, onProgress, cts.Token);

                            dbTask.Status = "Виконано";
                            dbTask.Progress = 100;
                            dbTask.CompletedAt = DateTime.UtcNow;
                            dbTask.ResultImageUrl = imageUrl;
                            await dbContext.SaveChangesAsync(stoppingToken);
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine($"Задачу {task.Id} скасовано користувачем.");
                        }
                        catch (Exception innerEx)
                        {
                            Console.WriteLine($"Помилка рендеру задачі {task.Id}: {innerEx.Message}");
                            dbTask.Status = "Помилка";
                            dbTask.Progress = 0;
                            await dbContext.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Критична помилка при обробці черги для задачі {task.Id}: {ex.Message}");
                }
                finally
                {
                    _cancellationManager.UnregisterTask(task.Id);
                }
            }
        }

        private async Task RestoreStuckTasksAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var stuckTasks = await dbContext.RenderTasks
                    .Where(t => t.Status == "В черзі" || t.Status.Contains("Малювання") || t.Status == "Початок малювання...")
                    .ToListAsync(stoppingToken);

                foreach (var task in stuckTasks)
                {
                    task.Status = "В черзі";
                    task.Progress = 0;

                    _taskQueue.TryQueueBackgroundWorkItem(task);
                }

                if (stuckTasks.Any())
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                    Console.WriteLine($"[Відновлення] Знайдено та повернуто в чергу {stuckTasks.Count} завислих завдань.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка при відновленні черги: {ex.Message}");
            }
        }
    }
}