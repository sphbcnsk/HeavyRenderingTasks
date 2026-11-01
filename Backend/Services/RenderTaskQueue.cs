using System.Threading.Channels;
using Backend.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Services
{
    public class RenderTaskQueue
    {
        private readonly Channel<RenderTask> _queue;

        public RenderTaskQueue()
        {
         
            var options = new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait };
            _queue = Channel.CreateBounded<RenderTask>(options);
        }

        public async ValueTask QueueBackgroundWorkItemAsync(RenderTask workItem)
        {
            if (workItem == null) throw new ArgumentNullException(nameof(workItem));
            await _queue.Writer.WriteAsync(workItem);
        }

        public async ValueTask<RenderTask> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}