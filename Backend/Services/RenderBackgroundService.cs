using Backend.Data;
using Backend.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        private readonly string _serverId; 

        public RenderBackgroundService(RenderTaskQueue taskQueue, IServiceScopeFactory scopeFactory,
            IWebHostEnvironment env, TaskCancellationManager cancellationManager, IConfiguration configuration)
        {
            _taskQueue = taskQueue;
            _scopeFactory = scopeFactory;
            _env = env;
            _cancellationManager = cancellationManager;
            _serverId = configuration["ServerId"] ?? "UnknownServer";
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

                    int claimed = await dbContext.RenderTasks
                        .Where(t => t.Id == task.Id && t.Status == "В черзі")
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "Початок малювання..."), stoppingToken);

                    if (claimed == 0)
                    {
                        continue;
                    }

                    var dbTask = await dbContext.RenderTasks.FindAsync(new object[] { task.Id }, stoppingToken);
                    if (dbTask != null)
                    {
                        try
                        {
                            int lastSavedProgress = 0;

                            Action<int> onProgress = (percent) =>
                            {
                                if (cts.Token.IsCancellationRequested) return;

                                if (percent - lastSavedProgress >= 5 || percent == 100)
                                {
                                    int rowsUpdated = dbContext.RenderTasks
                                        .Where(t => t.Id == task.Id && t.Status != "Скасовано")
                                        .ExecuteUpdate(s => s
                                            .SetProperty(t => t.Status, $"Малювання: {percent}%")
                                            .SetProperty(t => t.Progress, percent));

                                    if (rowsUpdated == 0)
                                    {
                                        cts.Cancel();
                                        return;
                                    }
                                    lastSavedProgress = percent;
                                }
                            };

                            string imageUrl = renderer.RenderCurve(task, _env.WebRootPath, onProgress, cts.Token);

                            if (!cts.Token.IsCancellationRequested)
                            {
                                dbContext.RenderTasks
                                    .Where(t => t.Id == task.Id && t.Status != "Скасовано")
                                    .ExecuteUpdate(s => s
                                        .SetProperty(t => t.Status, "Виконано")
                                        .SetProperty(t => t.Progress, 100)
                                        .SetProperty(t => t.CompletedAt, DateTime.UtcNow)
                                        .SetProperty(t => t.ResultImageUrl, imageUrl));
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine($"Задачу {task.Id} перервано (Скасовано).");
                        }
                        catch (Exception innerEx)
                        {
                            Console.WriteLine($"Помилка рендеру: {innerEx.Message}");
                            dbContext.RenderTasks.Where(t => t.Id == task.Id).ExecuteUpdate(s => s.SetProperty(t => t.Status, "Помилка"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Критична помилка черги: {ex.Message}");
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
                    .Where(t => t.ServerId == _serverId && 
                                (t.Status == "В черзі" || t.Status.Contains("Малювання") || t.Status == "Початок малювання..."))
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
                    Console.WriteLine($"[Сервер {_serverId}] Знайдено та повернуто в чергу {stuckTasks.Count} завислих завдань.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка при відновленні черги: {ex.Message}");
            }
        }
    }
}