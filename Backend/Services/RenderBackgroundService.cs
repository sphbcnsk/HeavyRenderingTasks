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
                                if (cts.Token.IsCancellationRequested) return;

                                if (percent - lastSavedProgress >= 5 || percent == 100)
                                {
                                    // АТОМАРНЕ ОНОВЛЕННЯ: оновлюємо відсотки ТІЛЬКИ якщо статус ще не "Скасовано"
                                    int rowsUpdated = dbContext.RenderTasks
                                        .Where(t => t.Id == task.Id && t.Status != "Скасовано")
                                        .ExecuteUpdate(s => s
                                            .SetProperty(t => t.Status, $"Малювання: {percent}%")
                                            .SetProperty(t => t.Progress, percent));

                                    if (rowsUpdated == 0)
                                    {
                                        // Якщо оновлено 0 рядків — інший сервер (через Load Balancer) вже скасував задачу!
                                        cts.Cancel();
                                        return;
                                    }

                                    lastSavedProgress = percent;
                                }
                            };

                            dbTask.Status = "Початок малювання...";
                            await dbContext.SaveChangesAsync(stoppingToken);

                            // Запуск малювання
                            string imageUrl = renderer.RenderCurve(task, _env.WebRootPath, onProgress, cts.Token);

                            if (!cts.Token.IsCancellationRequested)
                            {
                                // Оновлюємо фінальний статус атомарно
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
                            Console.WriteLine($"Задачу {task.Id} успішно перервано (Скасовано).");
                        }
                        catch (Exception innerEx)
                        {
                            Console.WriteLine($"Помилка рендеру задачі {task.Id}: {innerEx.Message}");
                            dbContext.RenderTasks
                                .Where(t => t.Id == task.Id)
                                .ExecuteUpdate(s => s
                                    .SetProperty(t => t.Status, "Помилка")
                                    .SetProperty(t => t.Progress, 0));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Критична помилка при обробці черги: {ex.Message}");
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