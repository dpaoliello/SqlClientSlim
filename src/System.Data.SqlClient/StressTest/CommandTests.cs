using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace StressTest
{
    /// <summary>
    /// Set of tests to run for commands
    /// </summary>
    public static class CommandTests
    {
        private static readonly Func<SqlCommand, CancellationToken, Task<int>> s_executeNonQueryAsync = (command, cancelToken) => command.ExecuteNonQueryAsync(cancelToken);
        private static readonly Func<SqlCommand, CancellationToken, Task<SqlDataReader>> s_executeReaderAsync = (command, cancelToken) => command.ExecuteReaderAsync(cancelToken);

        /// <summary>
        /// Runs one or more tests using the given command
        /// </summary>
        public static async Task RunAsync(SqlCommand command, ConnectionManager connectionManager, CancellationToken cancellationToken)
        {
            command.CommandText = "SELECT 1";

            if (RandomHelper.NextBoolWithProbability(1))
            {
                // 1%: Kill the connection
                connectionManager.KillConnection();
            }

            if (RandomHelper.NextBoolWithProbability(10))
            {
                // 10%: Run prepare
                command.Prepare();
            }

            // TODO: Command cancellation
            // TODO: Transactions

            if (RandomHelper.NextBoolWithProbability(50))
            {
                // 50%: ExecuteNonQuery
                await TryExecuteNonQueryAsync(command, connectionManager, cancellationToken);
            }
            else
            {
                // 50%: ExecuteReader
                var dataReaderResult = await TryExecuteReaderAsync(command, connectionManager, cancellationToken);

                // TODO: data reader
                if (dataReaderResult.WasSuccessful)
                {
                    var dataReader = dataReaderResult.Value;
                    if (RandomHelper.NextBoolWithProbability(95))
                    {
                        // 95%: Close the reader
                        dataReader.Close();
                    }
                }
            }

            if (RandomHelper.NextBoolWithProbability(95))
            {
                // 95%: Dispose the command
                command.Dispose();
            }
        }

        /// <summary>
        /// Attempts to run ExecuteNonQuery on a command, handling expected exceptions
        /// </summary>
        /// <returns>The number of rows affects if successful, otherwise null</returns>
        private static Task<ExecutionResult<int>> TryExecuteNonQueryAsync(SqlCommand command, ConnectionManager connectionManager, CancellationToken cancellationToken)
        {
            return TryExecuteAsync(command, connectionManager, cancellationToken, s_executeNonQueryAsync);
        }

        /// <summary>
        /// Attempts to run ExecuteNonQuery on a command, handling expected exceptions
        /// </summary>
        /// <returns>A SqlDataReader if successful, otherwise null</returns>
        private static Task<ExecutionResult<SqlDataReader>> TryExecuteReaderAsync(SqlCommand command, ConnectionManager connectionManager, CancellationToken cancellationToken)
        {
            return TryExecuteAsync(command, connectionManager, cancellationToken, s_executeReaderAsync);
        }

        /// <summary>
        /// Attempts to execute a command using the given execution function, catching known exceptions
        /// </summary>
        private static async Task<ExecutionResult<T>> TryExecuteAsync<T>(SqlCommand command, ConnectionManager connectionManager, CancellationToken cancellationToken, Func<SqlCommand, CancellationToken, Task<T>> executeFunc)
        {
            try
            {
                return new ExecutionResult<T>(await executeFunc(command, cancellationToken));
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            { }
            catch (SqlException ex) when (cancellationToken.IsCancellationRequested && (ex.Class == 11) && (ex.Number == 0) &&
                ((ex.Message == "Operation cancelled by user.") ||
                (ex.Errors[0].Message == "A severe error occurred on the current command.  The results, if any, should be discarded.")))
            {
                // Operation cancelled by user.
            }
            catch (SqlException ex) when (cancellationToken.IsCancellationRequested && (ex.Number == 3980))
            {
                // The request failed to run because the batch is aborted, this can be caused by abort signal sent from client, or another request is running in the same session, which makes the session busy.
            }
            catch (InvalidOperationException ex) when (cancellationToken.IsCancellationRequested && (ex.Message == "Operation cancelled by user."))
            {
                // Operation cancelled by user.
            }
            catch (SqlException ex) when (!connectionManager.IsConnectionAlive &&
                (ex.Message.EndsWith("(provider: SNI_PN7, error: 0 - Unable to write data to the transport connection: A request to send or receive data was disallowed because the socket had already been shut down in that direction with a previous shutdown call.)") ||
                ex.Message.EndsWith("(provider: SNI_PN7, error: 0 - Unable to write data to the transport connection: An established connection was aborted by the software in your host machine.)") ||
                ex.Message.EndsWith("(provider: SNI_PN7, error: 0 - Unable to read data from the transport connection: A request to send or receive data was disallowed because the socket had already been shut down in that direction with a previous shutdown call.)") ||
                ex.Message.EndsWith("(provider: SNI_PN7, error: 0 - Unable to read data from the transport connection: An established connection was aborted by the software in your host machine.)") ||
                ex.Message.EndsWith("(provider: SNI_PN7, error: 0 - Connection was terminated)") ||
                (ex.Message == "The connection is broken and recovery is not possible.  The connection is marked by the client driver as unrecoverable.  No attempt was made to restore the connection") ||
                ((connectionManager.IsMarsEnabled) && (ex.Message == "The connection is broken and recovery is not possible.  The connection is marked by the client driver as unrecoverable.  No attempt was made to restore the connection."))))
            {
                // Connection was killed
            }
            catch (InvalidOperationException ex) when (!connectionManager.IsConnectionAlive && connectionManager.IsMarsEnabled &&
                (ex.Message.Contains("requires an open and available Connection. The connection's current state") ||
                (ex.Message == "Invalid operation. The connection is closed.") ||
                (ex.Message == "The requested operation cannot be completed because the connection has been broken.")))
            {
                // MARS: Connection was killed on another thread
            }
            catch (SqlException ex) when (ex.Message == "Timeout expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.")
            {
                // Timeout
            }
            catch (Exception ex)
            {
                // Unknown error
                Debug.Assert(false, $"Unknown error: {ex.Message}");
                throw;
            }

            // Fallthrough: known exception happened
            return new ExecutionResult<T>();
        }

        /// <summary>
        /// Represents the result of executing a command
        /// </summary>
        private struct ExecutionResult<T>
        {
            /// <summary>
            /// Constructs a successful result with the given value
            /// </summary>
            public ExecutionResult(T value)
            {
                Value = value;
                WasSuccessful = true;
            }

            /// <summary>
            /// Gets the value of the result if successful, otherwise default(T)
            /// </summary>
            public T Value { get; }

            /// <summary>
            /// Gets a value indicating if the result was successful
            /// </summary>
            public bool WasSuccessful { get; }
        }
    }
}
