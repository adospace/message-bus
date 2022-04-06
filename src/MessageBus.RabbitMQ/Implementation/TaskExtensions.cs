using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBus.RabbitMQ.Implementation
{
    internal static class TaskExtensions
    {
        public static async Task<bool> CancelAfter(this Task task, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (await Task.WhenAny(task, Task.Delay(timeout, cancellationToken)) != task) return false;

            // Task completed within timeout.
            // Consider that the task may have faulted or been canceled.
            // We re-await the task so that any exceptions/cancellation is rethrown.
            await task;
            return true;

            // timeout/cancellation logic
        }
    }
}
