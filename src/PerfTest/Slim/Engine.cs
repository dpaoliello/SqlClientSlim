using System;
using System.Threading;
using System.Threading.Tasks;

namespace PerfTest
{
    /// <summary>
    /// Perf engine - has a set of threads that can run a given task for a given amount of time
    /// </summary>
    internal sealed class Engine
    {
        private Thread[] _threads;
        private Action _currentOperation;
        private CancellationToken _cancellationToken;
        private Barrier _workCompletedBarrier;
        private ManualResetEventSlim _workReadyEvent;
        private long _iterationsCompleted;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="threadCount">Number of work threads in the engine</param>
        public Engine(int threadCount)
        {
            _workCompletedBarrier = new Barrier(threadCount + 1);
            _workReadyEvent = new ManualResetEventSlim();
            Interlocked.MemoryBarrier();

            _threads = new Thread[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                _threads[i] = new Thread(WorkLoop);
                _threads[i].IsBackground = true;
                _threads[i].Start();
            }
        }

        /// <summary>
        /// Runs a perf test
        /// </summary>
        /// <param name="workToDo">The item that needs to be run</param>
        /// <param name="warmupTime">How long to run the warmup for</param>
        /// <param name="runningTime">How long to run the actual test for</param>
        /// <returns>Number of completed iterations</returns>
        public long RunTest(Action workToDo, TimeSpan warmupTime, TimeSpan runningTime)
        {
            _currentOperation = workToDo;

            StartWork(warmupTime);
            StartWork(runningTime);

            return _iterationsCompleted;
        }

        /// <summary>
        /// Runs the work threads for the given amount of time
        /// </summary>
        /// <param name="timeToRun">Amount of time to run the tests for</param>
        private void StartWork(TimeSpan timeToRun)
        {
            // Reset state
            CancellationTokenSource cancellationSource = new CancellationTokenSource();
            _cancellationToken = cancellationSource.Token;
            _iterationsCompleted = 0;

            // Kick off threads
            _workReadyEvent.Set();

            // Wait for completion
            Thread.Sleep(timeToRun);

            // Stop the threads
            cancellationSource.Cancel();
            _workReadyEvent.Reset();
            _workCompletedBarrier.SignalAndWait();
        }

        /// <summary>
        /// Main work loop for all of the threads
        /// </summary>
        private void WorkLoop()
        {
            do
            {
                // Wait for new work to be available
                _workReadyEvent.Wait();

                // Keep looping until requested
                long iterationsCompleted = 0;
                while (!_cancellationToken.IsCancellationRequested)
                {
                    _currentOperation();
                    iterationsCompleted++;
                }

                // Update the total count and indicate that we're done
                Interlocked.Add(ref _iterationsCompleted, iterationsCompleted);
                _workCompletedBarrier.SignalAndWait();
            } while (true);
        }
    }
}
