using System;

namespace PerfTest
{
    public static class Program
    {
        static void Main(string[] args)
        {
            Engine perfEngine = new Engine(threadCount: 2);

            RunTest(perfEngine, SqlClientTests.OpenPooledConnectionTest, "CreateConnectionTest", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15));
            RunTest(perfEngine, SqlClientTests.SelectOneTest, "SelectOneTest", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15));
            RunTest(perfEngine, SqlClientTests.SelectParametersTest, "SelectParametersTest", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15));
            RunTest(perfEngine, SqlClientTests.LargeStreamTcpTest, "LargeStreamTcpTest", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15));
            RunTest(perfEngine, SqlClientTests.LargeStreamNpTest, "LargeStreamNpTest", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15));
            RunTest(perfEngine, SqlClientTests.LargeStreamTcpMarsTest, "LargeStreamTcpMarsTest", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15));
        }

        private static void RunTest(Engine perfEngine, Action test, string testName, TimeSpan warmupTime, TimeSpan runningTime)
        {
            GC.Collect(generation: 2, mode: GCCollectionMode.Forced, blocking: true);

            Console.WriteLine("------------------");
            Console.WriteLine($"{testName} running for {runningTime}");

            long iterations = perfEngine.RunTest(test, warmupTime, runningTime);
            Console.WriteLine(iterations.ToString());

            GC.Collect(generation: 2, mode: GCCollectionMode.Forced, blocking: true);
        }
    }
}
