using System.Diagnostics;

namespace StressTest
{
    /// <summary>
    /// Trace listener that breaks into the debugger when an assert happens
    /// </summary>
    internal sealed class DebugTraceListener : TraceListener
    {
        public override void Fail(string message, string detailMessage)
        {
            Debugger.Break();
        }

        public override void Fail(string message)
        {
            Debugger.Break();
        }

        public override void Write(string message)
        {
            // Ignore the message
        }

        public override void WriteLine(string message)
        {
            // Ignore the message
        }
    }
}
