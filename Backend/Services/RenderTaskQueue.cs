using Backend.Models;
using System.Threading.Channels;

namespace Backend.Services
{
    public class RenderTaskQueue
    {
        private readonly Channel<RenderTask> _queue;

        public RenderTaskQueue()
        {
            var options = new BoundedChannelOptions(100) 
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _queue = Channel.CreateBounded<RenderTask>(options);
        }

        public bool TryQueueBackgroundWorkItem(RenderTask workItem)
        {
            if (workItem == null) throw new ArgumentNullException(nameof(workItem));
            return _queue.Writer.TryWrite(workItem);
        }

        public async ValueTask<RenderTask> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}