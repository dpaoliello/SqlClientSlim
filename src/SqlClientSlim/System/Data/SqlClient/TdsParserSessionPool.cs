// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.



//------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;

namespace System.Data.SqlClient
{
    internal class TdsParserSessionPool
    {
        // NOTE: This is a very simplistic, lightweight pooler.  It wasn't
        //       intended to handle huge number of items, just to keep track
        //       of the session objects to ensure that they're cleaned up in
        //       a timely manner, to avoid holding on to an unacceptable 
        //       amount of server-side resources in the event that consumers
        //       let their data readers be GC'd, instead of explicitly 
        //       closing or disposing of them

        private const int MaxInactiveCount = 10; // pick something, preferably small...

        private readonly TdsParser _parser;       // parser that owns us
        private readonly List<TdsParserStateObject> _cache;        // collection of all known sessions 
        private TdsParserStateObject[] _freeStateObjects; // collection of all sessions available for reuse
        private int _freeStateObjectCount; // Number of available free sessions

        internal TdsParserSessionPool(TdsParser parser)
        {
            _parser = parser;
            _cache = new List<TdsParserStateObject>();
            _freeStateObjects = new TdsParserStateObject[MaxInactiveCount];
            _freeStateObjectCount = 0;
        }

        private bool IsDisposed => (null == _freeStateObjects);

        internal void Deactivate()
        {
            // When being deactivated, we check all the sessions in the
            // cache to make sure they're cleaned up and then we dispose of
            // sessions that are past what we want to keep around.

            List<TdsParserStateObject> orphanedSessions = null;
            lock (_cache)
            {
                foreach (TdsParserStateObject session in _cache)
                {
                    if (session.IsOrphaned)
                    {
                        if (orphanedSessions == null)
                        {
                            orphanedSessions = new List<TdsParserStateObject>(_cache.Count);
                        }
                        orphanedSessions.Add(session);
                    }
                }
            }

            if (orphanedSessions != null)
            {
                foreach (TdsParserStateObject orphanedSession in orphanedSessions)
                {
                    PutSession(orphanedSession);
                }
            }
        }

        internal void Dispose()
        {
            TdsParserStateObject[] freeStateObjects;
            int freeStateObjectCount;
            List<TdsParserStateObject> orphanedSessions = null;
            lock (_cache)
            {
                freeStateObjects = _freeStateObjects;
                freeStateObjectCount = _freeStateObjectCount;
                _freeStateObjects = null;
                _freeStateObjectCount = 0;

                for (int i = 0; i < _cache.Count; i++)
                {
                    if (_cache[i].IsOrphaned)
                    {
                        if (orphanedSessions == null)
                        {
                            orphanedSessions = new List<TdsParserStateObject>();
                        }
                        orphanedSessions.Add(_cache[i]);
                    }
                    else
                    {
                        // Remove the "initial" callback
                        _cache[i].DecrementPendingCallbacks();
                    }
                }
                _cache.Clear();
                // Any active sessions will take care of themselves
                // (It's too dangerous to dispose them, as this can cause AVs)
            }

            // Dispose free sessions
            _parser.Connection._parserLock.Wait(canReleaseFromAnyThread: false);
            try
            {
                for (int i = 0; i < freeStateObjectCount; i++)
                {
                    if (freeStateObjects[i] != null)
                    {
                        freeStateObjects[i].Dispose();
                    }
                }
                if (orphanedSessions != null)
                {
                    foreach (TdsParserStateObject orphanedSession in orphanedSessions)
                    {
                        orphanedSession.Dispose();
                    }
                }
            }
            finally
            {
                _parser.Connection._parserLock.Release();
            }
        }

        internal TdsParserStateObject GetSession(object owner)
        {
            TdsParserStateObject session;
            bool createSession = false;
            lock (_cache)
            {
                if (IsDisposed)
                {
                    throw ADP.ClosedConnectionError();
                }
                else if (_freeStateObjectCount > 0)
                {
                    // Free state object - grab it
                    _freeStateObjectCount--;
                    session = _freeStateObjects[_freeStateObjectCount];
                    _freeStateObjects[_freeStateObjectCount] = null;
                    Debug.Assert(session != null, "There was a null session in the free session list?");
                }
                else
                {
                    // No free objects, create a new on
                    session = null;
                    createSession = true;
                }
            }

            if (createSession)
            {
                _parser.Connection._parserLock.Wait(canReleaseFromAnyThread: false);
                try
                {
                    session = _parser.CreateSession();
                }
                finally
                {
                    _parser.Connection._parserLock.Release();
                }

                lock (_cache)
                {
                    _cache.Add(session);
                }
            }

            session.Activate(owner);

            return session;
        }

        internal void PutSession(TdsParserStateObject session)
        {
            Debug.Assert(null != session, "null session?");

            bool okToReuse;
            _parser.Connection._parserLock.Wait(canReleaseFromAnyThread: false);
            try
            {
                okToReuse = session.Deactivate();
            }
            finally
            {
                _parser.Connection._parserLock.Release();
            }

            bool disposeSession = false;
            lock (_cache)
            {
                if (IsDisposed)
                {
                    // We're diposed - just clean out the session
                    Debug.Assert(_cache.Count == 0, "SessionPool is disposed, but there are still sessions in the cache?");
                    disposeSession = true;
                }
                else if ((okToReuse) && (_freeStateObjectCount < MaxInactiveCount))
                {
                    // Session is good to re-use and our cache has space
                    Debug.Assert(!session._pendingData, "pending data on a pooled session?");

                    _freeStateObjects[_freeStateObjectCount] = session;
                    _freeStateObjectCount++;
                }
                else
                {
                    // Either the session is bad, or we have no cache space - so dispose the session and remove it

                    bool removed = _cache.Remove(session);
                    Debug.Assert(removed, "session not in pool?");
                    disposeSession = true;
                }

                session.RemoveOwner();
            }

            if (disposeSession)
            {
                _parser.Connection._parserLock.Wait(canReleaseFromAnyThread: false);
                try
                {
                    session.Dispose();
                }
                finally
                {
                    _parser.Connection._parserLock.Release();
                }
            }
        }

        internal int ActiveSessionsCount => _cache.Count - _freeStateObjectCount;
    }
}


