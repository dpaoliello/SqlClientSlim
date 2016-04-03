using System;

namespace PerfTest
{
    public static class Program
    {
        static void Main(string[] args)
        {
            Engine perfEngine = new Engine(threadCount: 4);

            RunSlimAndFull(perfEngine, SqlClientSlimTests.OpenPooledConnectionTest, SqlClientFullTests.OpenPooledConnectionTest, "CreateConnectionTest", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15));
            RunSlimAndFull(perfEngine, SqlClientSlimTests.SelectOneTest, SqlClientFullTests.SelectOneTest, "SelectOneTest", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15));
            RunSlimAndFull(perfEngine, SqlClientSlimTests.SelectParametersTest, SqlClientFullTests.SelectParametersTest, "SelectParametersTest", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15));
            RunSlimAndFull(perfEngine, SqlClientSlimTests.LargeStreamTest, SqlClientFullTests.LargeStreamTest, "LargeStreamTest", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15));
        }

        private static void RunSlimAndFull(Engine perfEngine, Action slimTest, Action fullTest, string testName, TimeSpan warmupTime, TimeSpan runningTime)
        {
            Console.WriteLine("------------------");
            Console.WriteLine($"{testName} running for {runningTime}");

            long iterations = perfEngine.RunTest(slimTest, warmupTime, runningTime);
            Console.WriteLine($"Slim: {iterations}");

            iterations = perfEngine.RunTest(fullTest, warmupTime, runningTime);
            Console.WriteLine($"Full: {iterations}");
        }
    }
}
