// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Data.Common;
using System.Data.ProviderBase;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.SqlClient
{
    public sealed partial class SqlConnection : DbConnection
    {
        private static readonly Task s_connectionClosedTask = Task.FromException(ADP.ClosedConnectionError());

        private bool _AsyncCommandInProgress;

        // SQLStatistics support
        internal SqlStatistics _statistics;
        private bool _collectstats;

        private bool _fireInfoMessageEventOnUserErrors; // False by default

        // root task associated with current async invocation
        private Tuple<TaskCompletionSource<DbConnectionInternal>, Task> _currentCompletion;

        private string _connectionString;
        private int _connectRetryCount;

        // connection resiliency
        internal Task _currentReconnectionTask;
        private Task _asyncWaitingForReconnection; // current async task waiting for reconnection in non-MARS connections
        private Guid _originalConnectionId = Guid.Empty;
        private CancellationTokenSource _reconnectionCancellationSource;
        internal SessionData _recoverySessionData;
        internal bool _supressStateChangeForReconnection;
        private int _reconnectCount;

        // diagnostics listener
        private readonly static DiagnosticListener s_diagnosticListener = new DiagnosticListener(SqlClientDiagnosticListenerExtensions.DiagnosticListenerName);

        // Transient Fault handling flag. This is needed to convey to the downstream mechanism of connection establishment, if Transient Fault handling should be used or not
        // The downstream handling of Connection open is the same for idle connection resiliency. Currently we want to apply transient fault handling only to the connections opened
        // using SqlConnection.Open() method. 
        internal bool _applyTransientFaultHandling = false;

        public SqlConnection(string connectionString) : this()
        {
            ConnectionString = connectionString;    // setting connection string first so that ConnectionOption is available
            CacheConnectionStringProperties();
        }

        // This method will be called once connection string is set or changed. 
        private void CacheConnectionStringProperties()
        {
            SqlConnectionString connString = ConnectionOptions as SqlConnectionString;
            if (connString != null)
            {
                _connectRetryCount = connString.ConnectRetryCount;
            }
        }

        //
        // PUBLIC PROPERTIES
        //

        // used to start/stop collection of statistics data and do verify the current state
        //
        // devnote: start/stop should not performed using a property since it requires execution of code
        //
        // start statistics
        //  set the internal flag (_statisticsEnabled) to true.
        //  Create a new SqlStatistics object if not already there.
        //  connect the parser to the object.
        //  if there is no parser at this time we need to connect it after creation.
        //

        public bool StatisticsEnabled
        {
            get
            {
                return (_collectstats);
            }
            set
            {
                {
                    if (value)
                    {
                        // start
                        if (ConnectionState.Open == State)
                        {
                            if (null == _statistics)
                            {
                                _statistics = new SqlStatistics();
                                ADP.TimerCurrent(out _statistics._openTimestamp);
                            }
                            // set statistics on the parser
                            // update timestamp;
                            Debug.Assert(Parser != null, "Where's the parser?");
                            Parser.Statistics = _statistics;
                        }
                    }
                    else
                    {
                        // stop
                        if (null != _statistics)
                        {
                            if (ConnectionState.Open == State)
                            {
                                // remove statistics from parser
                                // update timestamp;
                                TdsParser parser = Parser;
                                Debug.Assert(parser != null, "Where's the parser?");
                                parser.Statistics = null;
                                ADP.TimerCurrent(out _statistics._closeTimestamp);
                            }
                        }
                    }
                    _collectstats = value;
                }
            }
        }

        internal bool AsyncCommandInProgress
        {
            get
            {
                return (_AsyncCommandInProgress);
            }
            set
            {
                _AsyncCommandInProgress = value;
            }
        }

        internal SqlConnectionString.TypeSystem TypeSystem
        {
            get
            {
                return ((SqlConnectionString)ConnectionOptions).TypeSystemVersion;
            }
        }


        internal int ConnectRetryInterval
        {
            get
            {
                return ((SqlConnectionString)ConnectionOptions).ConnectRetryInterval;
            }
        }

        override public string ConnectionString
        {
            get
            {
                return ConnectionString_Get();
            }
            set
            {
                ConnectionString_Set(new DbConnectionPoolKey(value));
                _connectionString = value;  // Change _connectionString value only after value is validated
                CacheConnectionStringProperties();
            }
        }

        override public int ConnectionTimeout
        {
            get
            {
                SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                return ((null != constr) ? constr.ConnectTimeout : SqlConnectionString.DEFAULT.Connect_Timeout);
            }
        }

        override public string Database
        {
            // if the connection is open, we need to ask the inner connection what it's
            // current catalog is because it may have gotten changed, otherwise we can
            // just return what the connection string had.
            get
            {
                SqlInternalConnection innerConnection = (InnerConnection as SqlInternalConnection);
                string result;

                if (null != innerConnection)
                {
                    result = innerConnection.CurrentDatabase;
                }
                else
                {
                    SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                    result = ((null != constr) ? constr.InitialCatalog : SqlConnectionString.DEFAULT.Initial_Catalog);
                }
                return result;
            }
        }

        override public string DataSource
        {
            get
            {
                SqlInternalConnection innerConnection = (InnerConnection as SqlInternalConnection);
                string result;

                if (null != innerConnection)
                {
                    result = innerConnection.CurrentDataSource;
                }
                else
                {
                    SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                    result = ((null != constr) ? constr.DataSource : SqlConnectionString.DEFAULT.Data_Source);
                }
                return result;
            }
        }

        public int PacketSize
        {
            // if the connection is open, we need to ask the inner connection what it's
            // current packet size is because it may have gotten changed, otherwise we
            // can just return what the connection string had.
            get
            {
                SqlInternalConnectionTds innerConnection = (InnerConnection as SqlInternalConnectionTds);
                int result;

                if (null != innerConnection)
                {
                    result = innerConnection.PacketSize;
                }
                else
                {
                    SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                    result = ((null != constr) ? constr.PacketSize : SqlConnectionString.DEFAULT.Packet_Size);
                }
                return result;
            }
        }

        public Guid ClientConnectionId
        {
            get
            {
                SqlInternalConnectionTds innerConnection = (InnerConnection as SqlInternalConnectionTds);

                if (null != innerConnection)
                {
                    return innerConnection.ClientConnectionId;
                }
                else
                {
                    Task reconnectTask = _currentReconnectionTask;
                    if (reconnectTask != null && !reconnectTask.IsCompleted)
                    {
                        return _originalConnectionId;
                    }
                    return Guid.Empty;
                }
            }
        }

        override public string ServerVersion
        {
            get
            {
                return GetOpenTdsConnection().ServerVersion;
            }
        }

        override public ConnectionState State
        {
            get
            {
                Task reconnectTask = _currentReconnectionTask;
                if (reconnectTask != null && !reconnectTask.IsCompleted)
                {
                    return ConnectionState.Open;
                }
                return InnerConnection.State;
            }
        }


        internal SqlStatistics Statistics
        {
            get
            {
                return _statistics;
            }
        }

        public string WorkstationId
        {
            get
            {
                // If not supplied by the user, the default value is the MachineName
                // Note: In Longhorn you'll be able to rename a machine without
                // rebooting.  Therefore, don't cache this machine name.
                SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                string result = ((null != constr) ? constr.WorkstationId : string.Empty);
                return result;
            }
        }

        // SqlCredential: Pair User Id and password in SecureString which are to be used for SQL authentication

        //
        // PUBLIC EVENTS
        //

        public event SqlInfoMessageEventHandler InfoMessage;

        public bool FireInfoMessageEventOnUserErrors
        {
            get
            {
                return _fireInfoMessageEventOnUserErrors;
            }
            set
            {
                _fireInfoMessageEventOnUserErrors = value;
            }
        }

        // Approx. number of times that the internal connection has been reconnected
        internal int ReconnectCount
        {
            get
            {
                return _reconnectCount;
            }
        }

        internal bool ForceNewConnection { get; set; }

        protected override void OnStateChange(StateChangeEventArgs stateChange)
        {
            if (!_supressStateChangeForReconnection)
            {
                base.OnStateChange(stateChange);
            }
        }

        //
        // PUBLIC METHODS
        //

        new public SqlTransaction BeginTransaction()
        {
            // this is just a delegate. The actual method tracks executiontime
            return BeginTransaction(IsolationLevel.Unspecified, null);
        }

        new public SqlTransaction BeginTransaction(IsolationLevel iso)
        {
            // this is just a delegate. The actual method tracks executiontime
            return BeginTransaction(iso, null);
        }

        public SqlTransaction BeginTransaction(string transactionName)
        {
            // Use transaction names only on the outermost pair of nested
            // BEGIN...COMMIT or BEGIN...ROLLBACK statements.  Transaction names
            // are ignored for nested BEGIN's.  The only way to rollback a nested
            // transaction is to have a save point from a SAVE TRANSACTION call.
            return BeginTransaction(IsolationLevel.Unspecified, transactionName);
        }

        override protected DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            DbTransaction transaction = BeginTransaction(isolationLevel);

            //   InnerConnection doesn't maintain a ref on the outer connection (this) and 
            //   subsequently leaves open the possibility that the outer connection could be GC'ed before the SqlTransaction
            //   is fully hooked up (leaving a DbTransaction with a null connection property). Ensure that this is reachable
            //   until the completion of BeginTransaction with KeepAlive
            GC.KeepAlive(this);

            return transaction;
        }

        public SqlTransaction BeginTransaction(IsolationLevel iso, string transactionName)
        {
            WaitForPendingReconnection();
            SqlStatistics statistics = null;

            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                SqlTransaction transaction;
                bool isFirstAttempt = true;
                do
                {
                    transaction = GetOpenTdsConnection().BeginSqlTransaction(iso, transactionName, isFirstAttempt); // do not reconnect twice
                    Debug.Assert(isFirstAttempt || !transaction.InternalTransaction.ConnectionHasBeenRestored, "Restored connection on non-first attempt");
                    isFirstAttempt = false;
                } while (transaction.InternalTransaction.ConnectionHasBeenRestored);


                //  The GetOpenConnection line above doesn't keep a ref on the outer connection (this),
                //  and it could be collected before the inner connection can hook it to the transaction, resulting in
                //  a transaction with a null connection property.  Use GC.KeepAlive to ensure this doesn't happen.
                GC.KeepAlive(this);

                return transaction;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
        }

        override public void ChangeDatabase(string database)
        {
            SqlStatistics statistics = null;
            RepairInnerConnection();
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);
                InnerConnection.ChangeDatabase(database);
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
        }

        static public void ClearAllPools()
        {
            SqlConnectionFactory.SingletonInstance.ClearAllPools();
        }

        static public void ClearPool(SqlConnection connection)
        {
            ADP.CheckArgumentNull(connection, nameof(connection));

            DbConnectionOptions connectionOptions = connection.UserConnectionOptions;
            if (null != connectionOptions)
            {
                SqlConnectionFactory.SingletonInstance.ClearPool(connection);
            }
        }


        private void CloseInnerConnection()
        {
            // CloseConnection() now handles the lock

            // The SqlInternalConnectionTds is set to OpenBusy during close, once this happens the cast below will fail and 
            // the command will no longer be cancelable.  It might be desirable to be able to cancel the close operation, but this is
            // outside of the scope of Whidbey RTM.  See (SqlCommand::Cancel) for other lock.
            InnerConnection.CloseConnection(this, ConnectionFactory);
        }

        override public void Close()
        {
            ConnectionState previousState = State;
            Guid operationId;
            Guid clientConnectionId;

            // during the call to Dispose() there is a redundant call to 
            // Close(). because of this, the second time Close() is invoked the 
            // connection is already in a closed state. this doesn't seem to be a 
            // problem except for logging, as we'll get duplicate Before/After/Error
            // log entries
            if (previousState != ConnectionState.Closed)
            { 
                operationId = s_diagnosticListener.WriteConnectionCloseBefore(this);
                // we want to cache the ClientConnectionId for After/Error logging, as when the connection 
                // is closed then we will lose this identifier
                //
                // note: caching this is only for diagnostics logging purposes
                clientConnectionId = ClientConnectionId;
            }

            SqlStatistics statistics = null;

            Exception e = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                Task reconnectTask = Interlocked.Exchange(ref _currentReconnectionTask, s_connectionClosedTask);
                if (reconnectTask != null && !reconnectTask.IsCompleted)
                {
                    CancellationTokenSource cts = _reconnectionCancellationSource;
                    if (cts != null)
                    {
                        cts.Cancel();
                    }
                    AsyncHelper.WaitForCompletion(reconnectTask, 0, null, rethrowExceptions: false); // we do not need to deal with possible exceptions in reconnection
                    if (State != ConnectionState.Open)
                    {// if we cancelled before the connection was opened 
                        OnStateChange(DbConnectionInternal.StateChangeClosed);
                    }
                }
                CancelOpenAndWait();
                CloseInnerConnection();
                GC.SuppressFinalize(this);

                if (null != Statistics)
                {
                    ADP.TimerCurrent(out _statistics._closeTimestamp);
                }
            }
            catch (Exception ex)
            {
                e = ex;
                throw;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);

                // we only want to log this if the previous state of the 
                // connection is open, as that's the valid use-case
                if (previousState != ConnectionState.Closed)
                { 
                    if (e != null)
                    {
                        s_diagnosticListener.WriteConnectionCloseError(operationId, clientConnectionId, this, e);
                    }
                    else
                    {
                        s_diagnosticListener.WriteConnectionCloseAfter(operationId, clientConnectionId, this);
                    }
                }
            }
        }

        new public SqlCommand CreateCommand()
        {
            return new SqlCommand(null, this);
        }

        private void DisposeMe(bool disposing)
        {
            if (!disposing)
            {
                // For non-pooled connections we need to make sure that if the SqlConnection was not closed, 
                // then we release the GCHandle on the stateObject to allow it to be GCed
                // For pooled connections, we will rely on the pool reclaiming the connection
                var innerConnection = (InnerConnection as SqlInternalConnectionTds);
                if ((innerConnection != null) && (!innerConnection.ConnectionOptions.Pooling))
                {
                    var parser = innerConnection.Parser;
                    if ((parser != null) && (parser._physicalStateObj != null))
                    {
                        parser._physicalStateObj.DecrementPendingCallbacks();
                    }
                }
            }
        }


        override public void Open()
        {
            Guid operationId = s_diagnosticListener.WriteConnectionOpenBefore(this);

            PrepareStatisticsForNewConnection();

            SqlStatistics statistics = null;

            Exception e = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                TaskCompletionSource<DbConnectionInternal> completionSource = null;
                if (!TryOpen(isAsync: false, completionSource: ref completionSource))
                {
                    throw ADP.InternalError(ADP.InternalErrorCode.SynchronousConnectReturnedPending);
                }
            }
            catch (Exception ex)
            {
                e = ex;
                throw;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);

                if (e != null)
                {
                    s_diagnosticListener.WriteConnectionOpenError(operationId, this, e);
                }
                else
                { 
                    s_diagnosticListener.WriteConnectionOpenAfter(operationId, this);
                }
            }
        }

        internal void RegisterWaitingForReconnect(Task waitingTask)
        {
            if (((SqlConnectionString)ConnectionOptions).MARS)
            {
                return;
            }
            Interlocked.CompareExchange(ref _asyncWaitingForReconnection, waitingTask, null);
            if (_asyncWaitingForReconnection != waitingTask)
            { // somebody else managed to register 
                throw SQL.MARSUnspportedOnConnection();
            }
        }

        private async Task ReconnectAsync(int timeout)
        {
            try
            {
                long commandTimeoutExpiration = 0;
                if (timeout > 0)
                {
                    commandTimeoutExpiration = ADP.TimerCurrent() + ADP.TimerFromSeconds(timeout);
                }
                CancellationTokenSource cts = new CancellationTokenSource();
                _reconnectionCancellationSource = cts;
                CancellationToken ctoken = cts.Token;
                int retryCount = _connectRetryCount; // take a snapshot: could be changed by modifying the connection string
                for (int attempt = 0; attempt < retryCount; attempt++)
                {
                    if (ctoken.IsCancellationRequested)
                    {
                        return;
                    }
                    try
                    {
                        try
                        {
                            ForceNewConnection = true;
                            await OpenAsync(ctoken).ConfigureAwait(false);
                            // On success, increment the reconnect count - we don't really care if it rolls over since it is approx.
                            _reconnectCount = unchecked(_reconnectCount + 1);
#if DEBUG
                            Debug.Assert(_recoverySessionData._debugReconnectDataApplied, "Reconnect data was not applied !");
#endif
                        }
                        finally
                        {
                            ForceNewConnection = false;
                        }
                        return;
                    }
                    catch (SqlException e)
                    {
                        if (attempt == retryCount - 1)
                        {
                            throw SQL.CR_AllAttemptsFailed(e, _originalConnectionId);
                        }
                        if (timeout > 0 && ADP.TimerRemaining(commandTimeoutExpiration) < ADP.TimerFromSeconds(ConnectRetryInterval))
                        {
                            throw SQL.CR_NextAttemptWillExceedQueryTimeout(e, _originalConnectionId);
                        }
                    }
                    await Task.Delay(1000 * ConnectRetryInterval, ctoken).ConfigureAwait(false);
                }
            }
            finally
            {
                _recoverySessionData = null;
                _supressStateChangeForReconnection = false;
            }
            Debug.Assert(false, "Should not reach this point");
        }

        internal Task ValidateAndReconnect(Action beforeDisconnect, int timeout)
        {
            Task runningReconnect = _currentReconnectionTask;
            // This loop in the end will return not completed reconnect task or null
            while (runningReconnect != null && runningReconnect.IsCompleted)
            {
                // clean current reconnect task (if it is the same one we checked
                Interlocked.CompareExchange<Task>(ref _currentReconnectionTask, null, runningReconnect);
                // make sure nobody started new task (if which case we did not clean it)
                runningReconnect = _currentReconnectionTask;
            }
            if (runningReconnect == null)
            {
                if (_connectRetryCount > 0)
                {
                    SqlInternalConnectionTds tdsConn = GetOpenTdsConnection();
                    if (tdsConn._sessionRecoveryAcknowledged)
                    {
                        TdsParser tdsParser = tdsConn.Parser;
                        if (!tdsParser._physicalStateObj.ValidateSNIConnection())
                        {
                            if ((tdsParser._sessionPool != null) && (tdsParser._sessionPool.ActiveSessionsCount > 0))
                            {
                                // >1 MARS session
                                beforeDisconnect?.Invoke();
                                OnError(SQL.CR_UnrecoverableClient(ClientConnectionId), true, null);
                            }

                            SessionData cData = tdsConn.CurrentSessionData;
                            cData.AssertUnrecoverableStateCountIsCorrect();
                            if (cData._unrecoverableStatesCount == 0)
                            {
                                TaskCompletionSource<object> reconnectCompletionSource = new TaskCompletionSource<object>();
                                runningReconnect = Interlocked.CompareExchange(ref _currentReconnectionTask, reconnectCompletionSource.Task, null);
                                if (runningReconnect == null)
                                {
                                    if (cData._unrecoverableStatesCount == 0)
                                    { // could change since the first check, but now is stable since connection is know to be broken
                                        _originalConnectionId = ClientConnectionId;
                                        _recoverySessionData = cData;
                                        beforeDisconnect?.Invoke();
                                        try
                                        {
                                            _supressStateChangeForReconnection = true;
                                            tdsConn.DoomThisConnection();
                                        }
                                        catch (SqlException)
                                        {
                                        }
                                        Task.Run(() => ReconnectAsync(timeout).ContinueWith(t => {
                                            if (t.IsFaulted)
                                            {
                                                reconnectCompletionSource.SetException(t.Exception);
                                            }
                                            else if (t.IsCanceled)
                                            {
                                                reconnectCompletionSource.SetCanceled();
                                            }
                                            else
                                            {
                                                reconnectCompletionSource.SetResult(null);
                                            }
                                        }));
                                        runningReconnect = reconnectCompletionSource.Task;
                                    }
                                }
                                else
                                {
                                    beforeDisconnect?.Invoke();
                                }
                            }
                            else
                            {
                                beforeDisconnect?.Invoke();
                                OnError(SQL.CR_UnrecoverableServer(ClientConnectionId), true, null);
                            }
                        } // ValidateSNIConnection
                    } // sessionRecoverySupported
                } // connectRetryCount>0
            }
            else
            { // runningReconnect = null
                beforeDisconnect?.Invoke();
            }
            return runningReconnect;
        }

        // this is straightforward, but expensive method to do connection resiliency - it take locks and all preparations as for TDS request
        partial void RepairInnerConnection()
        {
            WaitForPendingReconnection();
            if (_connectRetryCount == 0)
            {
                return;
            }
            SqlInternalConnectionTds tdsConn = InnerConnection as SqlInternalConnectionTds;
            if (tdsConn != null)
            {
                tdsConn.ValidateConnectionForExecute(null);
                tdsConn.GetSessionAndReconnectIfNeeded((SqlConnection)this);
            }
        }

        private void WaitForPendingReconnection()
        {
            Task reconnectTask = _currentReconnectionTask;
            if (reconnectTask != null && !reconnectTask.IsCompleted)
            {
                AsyncHelper.WaitForCompletion(reconnectTask, 0, null, rethrowExceptions: false);
            }
        }

        private void CancelOpenAndWait()
        {
            // copy from member to avoid changes by background thread
            var completion = _currentCompletion;
            if (completion != null)
            {
                completion.Item1.TrySetCanceled();
                ((IAsyncResult)completion.Item2).AsyncWaitHandle.WaitOne();
            }
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            Guid operationId = s_diagnosticListener.WriteConnectionOpenBefore(this);

            PrepareStatisticsForNewConnection();
            
            SqlStatistics statistics = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                if (cancellationToken.IsCancellationRequested)
                {
                    s_diagnosticListener.WriteConnectionOpenAfter(operationId, this);
                    return Task.FromCanceled(cancellationToken);
                }

                bool completed;
                TaskCompletionSource<DbConnectionInternal> completion = null;
                try
                {
                    completed = TryOpen(isAsync: true, completionSource: ref completion);
                }
                catch (Exception e)
                {
                    s_diagnosticListener.WriteConnectionOpenError(operationId, this, e);
                    return Task.FromException(e);
                }

                if (!completed)
                {
                    CancellationTokenRegistration registration = new CancellationTokenRegistration();
                    if (cancellationToken.CanBeCanceled)
                    {
                        registration = cancellationToken.Register(s => ((TaskCompletionSource<DbConnectionInternal>)s).TrySetCanceled(), completion);
                    }
                    OpenAsyncRetry retry = new OpenAsyncRetry(this, registration);
                    var openTask = completion.Task.ContinueWith(retry.Retry, TaskScheduler.Default).Unwrap();
                    _currentCompletion = Tuple.Create(completion, openTask);

                    if (s_diagnosticListener.IsEnabled(SqlClientDiagnosticListenerExtensions.SqlAfterOpenConnection) ||
                        s_diagnosticListener.IsEnabled(SqlClientDiagnosticListenerExtensions.SqlErrorOpenConnection))
                    {
                        return openTask.ContinueWith((t) =>
                        {
                            if (t.Exception != null)
                            {
                                s_diagnosticListener.WriteConnectionOpenError(operationId, this, t.Exception);
                            }
                            else
                            {
                                s_diagnosticListener.WriteConnectionOpenAfter(operationId, this);
                            }
                        }, TaskScheduler.Default);
                    }

                    return openTask;
                }
                else
                {
                    s_diagnosticListener.WriteConnectionOpenAfter(operationId, this);
                    return Task.CompletedTask;
                }
            }
            catch (Exception ex)
            {
                s_diagnosticListener.WriteConnectionOpenError(operationId, this, ex);
                throw;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
        }

        private class OpenAsyncRetry
        {
            private SqlConnection _parent;
            private CancellationTokenRegistration _registration;

            public OpenAsyncRetry(SqlConnection parent, CancellationTokenRegistration registration)
            {
                _parent = parent;
                _registration = registration;
            }

            internal Task Retry(Task<DbConnectionInternal> retryTask)
            {
                _registration.Dispose();

                if (retryTask.IsFaulted || retryTask.IsCanceled)
                {
                    _parent.CloseInnerConnection();
                }
                else
                {
                    s_connectionFactory.SetInnerConnectionEvent(_parent, retryTask.Result);
                    _parent.FinishOpen();
                }

                return retryTask;
            }
        }

        private void PrepareStatisticsForNewConnection()
        {
            if (StatisticsEnabled ||
                s_diagnosticListener.IsEnabled(SqlClientDiagnosticListenerExtensions.SqlAfterExecuteCommand) ||
                s_diagnosticListener.IsEnabled(SqlClientDiagnosticListenerExtensions.SqlAfterOpenConnection))
            {
                if (null == _statistics)
                {
                    _statistics = new SqlStatistics();
                }
                else
                {
                    _statistics.ContinueOnNewConnection();
                }
            }
        }

        private bool TryOpen(bool isAsync, ref TaskCompletionSource<DbConnectionInternal> completionSource)
        {
            if (_currentReconnectionTask == s_connectionClosedTask)
            {
                _currentReconnectionTask = null;
            }

            if (ForceNewConnection)
            {
                if (!InnerConnection.TryReplaceConnection(this, isAsync, ConnectionFactory, UserConnectionOptions, ref completionSource))
                {
                    Debug.Assert(completionSource != null, "If didn't completed sync, then should have a completionSource");
                    return false;
                }
            }
            else
            {
                if (!InnerConnection.TryOpenConnection(this, ConnectionFactory, isAsync, UserConnectionOptions, ref completionSource))
                {
                    Debug.Assert(completionSource != null, "If didn't completed sync, then should have a completionSource");
                    return false;
                }
            }
            // does not require GC.KeepAlive(this) because of OnStateChange

            Debug.Assert(completionSource == null, "If completed sync, then shouldn't have a completionSource");
            FinishOpen();
            return true;
        }

        private void FinishOpen()
        {
            var tdsInnerConnection = (InnerConnection as SqlInternalConnectionTds);
            Debug.Assert(tdsInnerConnection.Parser != null, "Where's the parser?");

            if (!tdsInnerConnection.ConnectionOptions.Pooling)
            {
                // For non-pooled connections, we need to make sure that the finalizer does actually run to avoid leaking SNI handles
                GC.ReRegisterForFinalize(this);
            }

            if (StatisticsEnabled ||
                s_diagnosticListener.IsEnabled(SqlClientDiagnosticListenerExtensions.SqlAfterExecuteCommand))
            {
                ADP.TimerCurrent(out _statistics._openTimestamp);
                tdsInnerConnection.Parser.Statistics = _statistics;
            }
            else
            {
                tdsInnerConnection.Parser.Statistics = null;
                _statistics = null; // in case of previous Open/Close/reset_CollectStats sequence
            }
        }


        //
        // INTERNAL PROPERTIES
        //

        internal bool HasLocalTransaction
        {
            get
            {
                return GetOpenTdsConnection().HasLocalTransaction;
            }
        }

        internal bool HasLocalTransactionFromAPI
        {
            get
            {
                Task reconnectTask = _currentReconnectionTask;
                if (reconnectTask != null && !reconnectTask.IsCompleted)
                {
                    return false; //we will not go into reconnection if we are inside the transaction
                }
                return GetOpenTdsConnection().HasLocalTransactionFromAPI;
            }
        }


        internal bool IsKatmaiOrNewer
        {
            get
            {
                if (_currentReconnectionTask != null)
                { // holds true even if task is completed
                    return true; // if CR is enabled, connection, if established, will be Katmai+
                }
                return GetOpenTdsConnection().IsKatmaiOrNewer;
            }
        }

        internal TdsParser Parser
        {
            get
            {
                SqlInternalConnectionTds tdsConnection = GetOpenTdsConnection();
                return tdsConnection.Parser;
            }
        }

        /// <summary>
        /// Gets the TdsParser associated with this connection if there is one, otherwise returns null.
        /// </summary>
        internal TdsParser TryGetParser() => (InnerConnection as SqlInternalConnectionTds)?.Parser;

        //
        // INTERNAL METHODS
        //

        internal void ValidateConnectionForExecute(string method, SqlCommand command)
        {
            Task asyncWaitingForReconnection = _asyncWaitingForReconnection;
            if (asyncWaitingForReconnection != null)
            {
                if (!asyncWaitingForReconnection.IsCompleted)
                {
                    throw SQL.MARSUnspportedOnConnection();
                }
                else
                {
                    Interlocked.CompareExchange(ref _asyncWaitingForReconnection, null, asyncWaitingForReconnection);
                }
            }
            if (_currentReconnectionTask != null)
            {
                Task currentReconnectionTask = _currentReconnectionTask;
                if (currentReconnectionTask != null && !currentReconnectionTask.IsCompleted)
                {
                    return; // execution will wait for this task later
                }
            }
            SqlInternalConnectionTds innerConnection = GetOpenTdsConnection(method);
            innerConnection.ValidateConnectionForExecute(command);
        }

        // Surround name in brackets and then escape any end bracket to protect against SQL Injection.
        // NOTE: if the user escapes it themselves it will not work, but this was the case in V1 as well
        // as native OleDb and Odbc.
        static internal string FixupDatabaseTransactionName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                return SqlServerEscapeHelper.EscapeIdentifier(name);
            }
            else
            {
                return name;
            }
        }

        // If wrapCloseInAction is defined, then the action it defines will be run with the connection close action passed in as a parameter
        // The close action also supports being run asynchronously
        internal void OnError(SqlException exception, bool breakConnection, Action<Action> wrapCloseInAction)
        {
            Debug.Assert(exception != null && exception.Errors.Count != 0, "SqlConnection: OnError called with null or empty exception!");


            if (breakConnection && (ConnectionState.Open == State))
            {
                if (wrapCloseInAction != null)
                {
                    int capturedCloseCount = _closeCount;

                    Action closeAction = () =>
                    {
                        if (capturedCloseCount == _closeCount)
                        {
                            Close();
                        }
                    };

                    wrapCloseInAction(closeAction);
                }
                else
                {
                    Close();
                }
            }

            if (exception.Class >= TdsEnums.MIN_ERROR_CLASS)
            {
                // It is an error, and should be thrown.  Class of TdsEnums.MIN_ERROR_CLASS or above is an error,
                // below TdsEnums.MIN_ERROR_CLASS denotes an info message.
                throw exception;
            }
            else
            {
                // If it is a class < TdsEnums.MIN_ERROR_CLASS, it is a warning collection - so pass to handler
                this.OnInfoMessage(new SqlInfoMessageEventArgs(exception));
            }
        }

        //
        // PRIVATE METHODS
        //


        internal SqlInternalConnectionTds GetOpenTdsConnection()
        {
            SqlInternalConnectionTds innerConnection = (InnerConnection as SqlInternalConnectionTds);
            if (null == innerConnection)
            {
                throw ADP.ClosedConnectionError();
            }
            return innerConnection;
        }

        internal SqlInternalConnectionTds GetOpenTdsConnection(string method)
        {
            DbConnectionInternal innerDbConnection = InnerConnection;
            SqlInternalConnectionTds innerSqlConnection = (innerDbConnection as SqlInternalConnectionTds);
            if (null == innerSqlConnection)
            {
                throw ADP.OpenConnectionRequired(method, innerDbConnection.State);
            }
            return innerSqlConnection;
        }

        internal void OnInfoMessage(SqlInfoMessageEventArgs imevent)
        {
            bool notified;
            OnInfoMessage(imevent, out notified);
        }

        internal void OnInfoMessage(SqlInfoMessageEventArgs imevent, out bool notified)
        {
            SqlInfoMessageEventHandler handler = InfoMessage;
            if (null != handler)
            {
                notified = true;
                try
                {
                    handler(this, imevent);
                }
                catch (Exception e)
                {
                    if (!ADP.IsCatchableOrSecurityExceptionType(e))
                    {
                        throw;
                    }
                }
            }
            else
            {
                notified = false;
            }
        }

        // this only happens once per connection
        // SxS: using named file mapping APIs

        internal void RegisterForConnectionCloseNotification<T>(ref Task<T> outerTask, object value, int tag)
        {
            // Connection exists,  schedule removal, will be added to ref collection after calling ValidateAndReconnect
            outerTask = outerTask.ContinueWith(task =>
            {
                RemoveWeakReference(value);
                return task;
            }, TaskScheduler.Default).Unwrap();
        }

        public void ResetStatistics()
        {
            if (null != Statistics)
            {
                Statistics.Reset();
                if (ConnectionState.Open == State)
                {
                    // update timestamp;
                    ADP.TimerCurrent(out _statistics._openTimestamp);
                }
            }
        }

        public IDictionary RetrieveStatistics()
        {
            if (null != Statistics)
            {
                UpdateStatistics();
                return Statistics.GetDictionary();
            }
            else
            {
                return new SqlStatistics().GetDictionary();
            }
        }

        private void UpdateStatistics()
        {
            if (ConnectionState.Open == State)
            {
                // update timestamp
                ADP.TimerCurrent(out _statistics._closeTimestamp);
            }
            // delegate the rest of the work to the SqlStatistics class
            Statistics.UpdateStatistics();
        }

        /// <summary>
        /// TEST ONLY: Kills the underlying connection (without any checks)
        /// </summary>
        internal void KillConnection()
        {
            GetOpenTdsConnection().Parser._physicalStateObj.Handle.KillConnection();
        }
    } // SqlConnection
} // System.Data.SqlClient namespace


