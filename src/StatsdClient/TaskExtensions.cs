using System;
using System.Threading;
using System.Threading.Tasks;

namespace StatsdClient
{
    internal static class TaskExtensions
    {
        internal static void WaitAndUnwrapException(this Task task, int waitTimeout)
        {
            var awaited = ConfigureAwaitFalse(task);
            try
            {
                awaited.Wait(waitTimeout);
            }
            catch (AggregateException agg)
            {
                throw agg.InnerException;

            }
        }

        private async static Task ConfigureAwaitFalse(Task t)
        {
            await t.ConfigureAwait(false);
        }
    }
}
