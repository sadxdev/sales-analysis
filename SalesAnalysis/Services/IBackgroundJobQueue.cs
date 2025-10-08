using System;
using System.Threading;
using System.Threading.Tasks;

namespace SalesAnalysis.BackgroundJobs
{
    public interface IBackgroundJobQueue
    {
        void Enqueue(Func<CancellationToken, Task> workItem);
        Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }
}
