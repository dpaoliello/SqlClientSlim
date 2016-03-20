using System;
using System.Data.SqlClient;
using System.Threading;

namespace StressTest
{
    /// <summary>
    /// Centrally manages a SqlConnection - should be passed by ref.
    /// </summary>
    public sealed class ConnectionManager
    {
        private const int ConnectionAlive = 0;
        private const int ConnectionDead = 1;

        private readonly SqlConnection _connection;
        private int _connectionState;

        /// <summary>
        /// Creates a manager for the given connection.
        /// </summary>
        public ConnectionManager(SqlConnection connection, bool isMarsEnabled)
        {
            _connection = connection;
            _connectionState = ConnectionAlive;
            IsMarsEnabled = isMarsEnabled;
        }

        /// <summary>
        /// Gets a value indicating if the connection is known to the manager to be alive.
        /// </summary>
        public bool IsConnectionAlive => (Volatile.Read(ref _connectionState) == ConnectionAlive);

        /// <summary>
        /// Gets a value indicating if MARS is enabled on the connection.
        /// </summary>
        public bool IsMarsEnabled { get; }

        /// <summary>
        /// Kills the managed connection.
        /// </summary>
        public void KillConnection()
        {
            Volatile.Write(ref _connectionState, ConnectionDead);

            try
            {
                _connection.KillConnection();
            }
            catch (InvalidOperationException ex) when (ex.Message == "Invalid operation. The connection is closed.")
            { }
        }
    }
}
