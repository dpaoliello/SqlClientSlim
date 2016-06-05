using System;
using System.Threading;
using System.Threading.Tasks;

namespace StressTest
{
    /// <summary>
    /// Main stress test engine
    /// </summary>
    public static class Engine
    {
        private const long ReportStatusEvery = 100;

        private static readonly CancellationTokenSource cancellationSourceWithoutPendingCancel = new CancellationTokenSource();
        private static readonly Func<CancellationToken, Task>[] s_workerActions = {
            EndToEndTests.SqlAuthConnectionTest,
            EndToEndTests.MarsConnectionTest
        };

        /// <summary>
        /// Runs the stress test engine
        /// </summary>
        public static long Run(int threadCount, Action<long> reportStatus, CancellationToken stopEngineToken)
        {
            long tasksCompleted = 0;

            // Start the tasks
            Task[] workerTasks = new Task[threadCount];
            CancellationTokenSource[] workerCancellationSources = new CancellationTokenSource[threadCount];
            for (int i = 0; i < workerTasks.Length; i++)
            {
                workerTasks[i] = StartWorker(out workerCancellationSources[i]);
            }

            // Replace each task as it completes
            while (!stopEngineToken.IsCancellationRequested)
            {
                int completedIndex = Task.WaitAny(workerTasks, stopEngineToken);
                if (completedIndex >= 0)
                {
                    tasksCompleted++;

                    try
                    {
                        workerTasks[completedIndex].Wait();
                    }
                    catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
                    {
                        // Eat task cancelled exceptions
                    }

                    if (!stopEngineToken.IsCancellationRequested)
                    {
                        workerTasks[completedIndex] = StartWorker(out workerCancellationSources[completedIndex]);
                    }

                    if (tasksCompleted % ReportStatusEvery == 0)
                    {
                        reportStatus(tasksCompleted);
                    }
                }
            }

            // Stop was requested - stop all of the workers
            for (int i = 0; i < workerTasks.Length; i++)
            {
                workerCancellationSources[i].Cancel();
            }

            Task.WaitAll(workerTasks);
            return tasksCompleted;
        }

        /// <summary>
        /// Starts a worker task
        /// </summary>
        private static Task StartWorker(out CancellationTokenSource cancellationSource)
        {
            if (RandomHelper.NextBoolWithProbability(25))
            {
                // 25%: Provide a token that will be cancelled
                cancellationSource = new CancellationTokenSource();
                cancellationSource.CancelAfter(RandomHelper.Next(50, 2000));
            }
            else
            {
                // else: Token will not be cancelled
                cancellationSource = cancellationSourceWithoutPendingCancel;
            }

            var token = cancellationSource.Token;
            var action = RandomHelper.SelectFromArray(s_workerActions);

            return Task.Run(() => action(token));
        }
    }
}
