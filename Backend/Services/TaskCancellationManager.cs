using System.Collections.Concurrent;
using System.Threading;
using System;

namespace Backend.Services
{
    public class TaskCancellationManager
    {
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _tokens = new();

        public void RegisterTask(Guid taskId, CancellationTokenSource cts)
        {
            _tokens.TryAdd(taskId, cts);
        }

        public void UnregisterTask(Guid taskId)
        {
            _tokens.TryRemove(taskId, out _);
        }

        public void CancelTask(Guid taskId)
        {
            if (_tokens.TryGetValue(taskId, out var cts))
            {
                cts.Cancel();
            }
        }
    }
}