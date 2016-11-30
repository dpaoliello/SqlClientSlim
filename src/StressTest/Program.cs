using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace StressTest
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Trace.Listeners.Add(new DebugTraceListener());

            // TODO: Dynamic number of threads
            const int threadCount = 8;

            // Pressing enter will stop the engine
            Console.WriteLine("Press [Enter] to cancel");
            CancellationTokenSource stopEngineSource = new CancellationTokenSource();
            Task.Run(() => {
                Console.ReadLine();
                Console.WriteLine("Stopping");
                stopEngineSource.Cancel();
            });

            Stopwatch watch = new Stopwatch();
            Action<long> reportStatusAction = (count) => {
                watch.Stop();
                Console.WriteLine($"Completed {count}. Time {watch.ElapsedMilliseconds}ms");
                watch.Restart();
            };

            try
            {
                Engine.Run(threadCount, reportStatusAction, stopEngineSource.Token);
            }
            catch (OperationCanceledException)
            { }
        }
    }
}
