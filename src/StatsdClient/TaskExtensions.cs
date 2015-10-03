using System;
using System.Threading.Tasks;

namespace StatsdClient
{
    internal static class TaskExtensions
    {
        internal static void WaitAndUnwrapException(this Task task)
        {
            var awaited = ConfigureAwaitFalse(task);
            try
            {
                awaited.Wait();
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
