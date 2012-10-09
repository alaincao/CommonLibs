using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Web;

namespace CommonLibs.Web.LongPolling
{
	// Warning: MessageHandler and ConnectionList are heavily dependents.
	// To any avoid dead-locks, all these interactions must be performed outside their respective locks() (or Enter{Read|Write}Lock())

	public class ConnectionList
	{
		private class ConnectionEntry : IDisposable
		{
// TODO: Alain: Is it used? if yes, don't forget to check the warning above
			internal ConnectionList					ConnectionList					{ get; private set; }
			internal string							SessionID						{ get; private set; }
			internal string							ConnectionID					{ get; private set; }
			internal IConnection					Connection						= null;
			internal bool							Available						{ get { return (!Disposed) && (Connection != null); } }
			internal bool							Disposed						{ get; private set; }
			internal Utils.TaskEntry				DisconnectionTimeout			= null;
			internal Utils.TaskEntry				StaleTimeout					= null;

			internal ConnectionEntry(ConnectionList connectionList, string sessionID, string connectionID)
			{
				System.Diagnostics.Debug.Assert( connectionList != null, "Missing parameter 'connectionList'" );
				System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(connectionID), "Missing parameter 'connectionID'" );

				ConnectionList = connectionList;
				SessionID = sessionID;
				ConnectionID = connectionID;
				Disposed = false;
			}

			public void Dispose()
			{
				if( Disposed )
					// Already disposed
					return;
				Disposed = true;
				if( Connection != null )
				{
					try { Connection.SendReset(); }
					catch( System.Exception ex ) { ConnectionList.FatalExceptionHandler( "Call to Connection.SendReset() threw an exception", ex ); }
				}
				if( DisconnectionTimeout != null )
				{
					try { DisconnectionTimeout.Dispose(); }
					catch( System.Exception ex ) { ConnectionList.FatalExceptionHandler( "Call to DisconnectionTimeout.Dispose() threw an exception", ex ); }
					DisconnectionTimeout = null;
				}
				if( StaleTimeout != null )
				{
					try { StaleTimeout.Dispose(); }
					catch( System.Exception ex ) { ConnectionList.FatalExceptionHandler( "Call to StaleTimeout.Dispose() threw an exception", ex ); }
					StaleTimeout = null;
				}
			}
		}

		public static MessageHandler				MessageHandler					= null;

		/// <summary>If a connection stays disconnected more than this amount of seconds, it is considered definately disconnected</summary>
		public const int							DisconnectionSeconds			= 60*60;
		/// <summary>
		/// If a connections is 
		/// </summary>
		public const int							StaleConnectionSeconds			= 60*60;


		public Utils.TasksQueue						TasksQueue						{ get; private set; }

		/// <summary>
		/// Key=ConnectionID ; Value=The ConnectionEntry
		/// </summary>
		private Dictionary<string,ConnectionEntry>	AllConnections					= new Dictionary<string,ConnectionEntry>();

		/// <summary>
		/// Key=SessionID ; Value: List of ConnectionIDs
		/// </summary>
		private Dictionary<string,List<string>>		AllSessions						= new Dictionary<string,List<string>>();

		/// <summary>Triggered when a new connection has been created</summary>
		public event Action<string>					ConnectionAllocated				{ add { ConnectionAllocatedCallbacks.Add(value); } remove { var rc=ConnectionAllocatedCallbacks.Remove(value); System.Diagnostics.Debug.Assert( rc, "Failed to remove event's callback " + value ); } }
		private List<Action<string>>				ConnectionAllocatedCallbacks	= new List<Action<string>>();

		/// <summary>Triggered when a connection has been registered</summary>
		/// <remarks>Be aware that due to multithreading, the connection is not guaranteed to be available at the time of the event triggering</remarks>
		public event Action<string>					ConnectionRegistered			{ add { ConnectionRegisteredCallbacks.Add(value); } remove { var rc=ConnectionRegisteredCallbacks.Remove(value); System.Diagnostics.Debug.Assert( rc, "Failed to remove event's callback " + value ); } }
		private List<Action<string>>				ConnectionRegisteredCallbacks	= new List<Action<string>>();

		private Action<string,Exception>			FatalExceptionHandler;

		public ConnectionList(Utils.TasksQueue tasksQueue)
		{
			LOG( "Constructor" );

			FatalExceptionHandler = DefaultFatalExceptionHandler;

			TasksQueue = tasksQueue;
		}

		[System.Diagnostics.Conditional("DEBUG")]
		private void LOG(string message)
		{
			LOG( GetType().FullName, message );
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void LOG(string type, string message)
		{
			System.Diagnostics.Debug.WriteLine( "" + Thread.CurrentThread.ManagedThreadId + ": " + DateTime.UtcNow + " " + type + ": " + (message ?? "<NULL>") );
		}

		private void DefaultFatalExceptionHandler(string description, Exception exception)
		{
			System.Diagnostics.Debug.Assert( exception != null, "Missing parameter 'exception'" );
			string message = "" + description + ":" + exception;
			System.Diagnostics.Debug.Fail( message );
			LOG( message );
		}

		public void SessionStarted(string sessionID)
		{
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(sessionID), "Missing parameter 'sessionID'" );
			LOG( "SessionStarted(" + sessionID + ") - Start" );

			lock( this )
			{
				LOG( "SessionStarted(" + sessionID + ") - Lock aquired" );

				CheckValidity();

				if( AllSessions.ContainsKey(sessionID) )
				{
					System.Diagnostics.Debug.Fail( "Session '" + sessionID + "' is already registered" );
					return;
				}
				else
				{
					AllSessions.Add( sessionID, new List<string>() );
				}

				CheckValidity();
			}

			LOG( "SessionStarted(" + sessionID + ") - Exit" );
		}

		public void SessionEnded(string sessionID)
		{
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(sessionID), "Missing parameter 'sessionID'" );
			LOG( "SessionEnded(" + sessionID + ") - Start" );

			lock( this )
			{
				LOG( "SessionEnded(" + sessionID + ") - Lock aquired" );

				CheckValidity();

				List<string> connectionList;
				if(! AllSessions.TryGetValue(sessionID, out connectionList) )
				{
					System.Diagnostics.Debug.Fail( "Session '" + sessionID + "' is supposed to be removed but is not registered" );
					return;
				}
				if( connectionList == null )
				{
					System.Diagnostics.Debug.Fail( "The 'connectionList' is not supposed to be null here" );
					return;
				}

				// Close all connections in this session
				foreach( var connectionID in connectionList )
				{
					try
					{
						ConnectionEntry connectionEntry;
						if(! AllConnections.TryGetValue(connectionID, out connectionEntry) )
						{
							System.Diagnostics.Debug.Fail( "Connection '" + connectionID + "' is not in 'AllConnections'" );
							continue;
						}
						AllConnections.Remove( connectionID );
						connectionEntry.Connection.SendLogout();
						connectionEntry.Connection = null;
						connectionEntry.Dispose();
					}
					catch( System.Exception ex )
					{
						FatalExceptionHandler( "Could not close connection '" + connectionID + "'", ex );
					}
				}
				AllSessions.Remove( sessionID );

				CheckValidity();
			}
			LOG( "SessionEnded(" + sessionID + ") - Exit" );
		}

		/// <param name="sessionID">The session this new ConnectionID will belong</param>
		public string AllocateNewConnectionID(string sessionID)
		{
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(sessionID), "Missing parameter 'sessionID'" );
			LOG( "AllocateNewConnectionID(" + sessionID + ") - Start" );

			string connectionID;
			lock( this )
			{
				LOG( "AllocateNewConnectionID(" + sessionID + ") - Lock aquired" );
				CheckValidity();

				List<string> sessionConnections;
				if(! AllSessions.TryGetValue(sessionID, out sessionConnections) )
				{
					System.Diagnostics.Debug.Fail( "Allocating a new ConnectionID for a session that hasn't been registered" );
					sessionConnections = new List<string>();
					AllSessions.Add( sessionID, sessionConnections );
				}

				connectionID = Guid.NewGuid().ToString();
				var connectionEntry = new ConnectionEntry( this, sessionID, connectionID );
				AllConnections.Add( connectionID, connectionEntry );
				sessionConnections.Add( connectionID );

				LOG( "AllocateNewConnectionID(" + sessionID + ") - Starting DisconnectionTimeout" );
				connectionEntry.DisconnectionTimeout = TasksQueue.CreateTask( 0, 0, 0, DisconnectionSeconds, 0, (taskEntry)=>{ConnectionEntry_DisconnectionTimeout(taskEntry, connectionEntry);} );
				LOG( "AllocateNewConnectionID(" + sessionID + ") - Started DisconnectionTimeout " + connectionEntry.DisconnectionTimeout );

				CheckValidity();
				LOG( "AllocateNewConnectionID(" + sessionID + ") - Exit: " + connectionID );
			}

			foreach( var callback in ConnectionAllocatedCallbacks )
			{
				try { callback( connectionID ); }
				catch( System.Exception ex ) { FatalExceptionHandler( "ConnectionAllocated callback threw an exception", ex ); }
			}
			return connectionID;
		}

		/// <returns>'false' if CheckValidity() returns false</returns>
		internal bool RegisterConnection(IConnection connection)
		{
			System.Diagnostics.Debug.Assert( connection != null, "Missing parameter 'connection'" );
			var connectionID = connection.ConnectionID;
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(connectionID), "The connection has no 'ConnectionID'" );
			var sessionID = connection.SessionID;
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(sessionID), "The connection has no 'SessionID'" );
			LOG( "RegisterConnection(" + connectionID + ") - Start" );

			lock( this )
			{
				LOG( "RegisterConnection(" + connectionID + ") - Lock aquired" );
				CheckValidity();

				if(! CheckConnectionIsValid(sessionID, connectionID) )
				{
					System.Diagnostics.Debug.Fail( "Trying to register an invalid connection" );
					return false;
				}
				System.Diagnostics.Debug.Assert( AllConnections.ContainsKey(connectionID), "If the ConnectionIsValid then 'AllConnections' should contain the ConnectionID" );

				// Get the ConnectionEntry
				var connectionEntry = AllConnections[ connectionID ];

				if( connectionEntry.DisconnectionTimeout == null )
				{
					System.Diagnostics.Debug.Fail( "We are supposed to be registering a long-polling HTTP connection to a currently disconnected IConnection. A DisconnectionTimeout is supposed to be running here" );
				}
				else
				{
					// Stop the DisconnectionTimeout
					LOG( "RegisterConnection(" + connectionID + ") - Unregistering DisconnectionTimeout " + connectionEntry.DisconnectionTimeout );
					connectionEntry.DisconnectionTimeout.GetStatus( (status)=>{ if( status != Utils.TaskEntry.Statuses.Running )  connectionEntry.DisconnectionTimeout.Dispose(); } );  // Don't deactivate the timeout if it is already running (it may be this very thread)
					connectionEntry.DisconnectionTimeout = null;
				}

				if( connectionEntry.StaleTimeout != null )
				{
					System.Diagnostics.Debug.Fail( "We are supposed to be registering a long-polling HTTP connection to a currently disconnected IConnection. No StaleTimeout is supposed to be running here" );
					// Remove the (mistakenly) running StaleTimeout
					LOG( "RegisterConnection(" + connectionID + ") - Unregistering StaleTimeout '" + connectionEntry.StaleTimeout + "'" );
					connectionEntry.StaleTimeout.GetStatus( (status)=>{ if( status != Utils.TaskEntry.Statuses.Running )  connectionEntry.StaleTimeout.Dispose(); } );  // Don't deactivate the timeout if it is already running (it may be this very thread)
					connectionEntry.StaleTimeout = null;
				}

				if( connectionEntry.Connection != null )
				{
					System.Diagnostics.Debug.Fail( "We are supposed to be registering a long-polling HTTP connection to a currently disconnected IConnection. No Connection is supposed to be present here" );
					// Reset the (mistakenly) open connection
					connectionEntry.Connection.SendReset();
					connectionEntry.Connection = null;
				}

				// Associate the IConnection to the ConnectionEntry
				connectionEntry.Connection = connection;

				// Start the StaleTimeout
				connectionEntry.StaleTimeout = TasksQueue.CreateTask( 0, 0, 0, StaleConnectionSeconds, 0, (taskEntry)=>{ConnectionEntry_StaleTimeout(taskEntry, connectionEntry);} );
				LOG( "RegisterConnection(" + connectionID + ") - Started StaleTimeout '" + connectionEntry.StaleTimeout + "'" );

				CheckValidity();
				LOG( "RegisterConnection(" + connectionID + ") - Exit" );
			}

			foreach( var callback in ConnectionRegisteredCallbacks )
			{
				try { callback( connectionID ); }
				catch( System.Exception ex ) { FatalExceptionHandler( "ConnectionRegistered callback threw an exception", ex ); }
			}
			return true;
		}

		public bool CheckConnectionIsValid( string sessionID, string connectionID )
		{
			LOG( "CheckConnectionIsValid(" + sessionID + ", " + connectionID + ") - Start" );
			lock( this )
			{
				LOG( "CheckConnectionIsValid(" + sessionID + ", " + connectionID + ") - Lock aquired" );
				CheckValidity();

				List<string> connectionIDs;
				if(! AllSessions.TryGetValue(sessionID, out connectionIDs) )
					return false;
				LOG( "CheckConnectionIsValid(" + sessionID + ", " + connectionID + ") - Exit" );
				return connectionIDs.Contains( connectionID );
			}
		}

		private void ConnectionEntry_DisconnectionTimeout(Utils.TaskEntry taskEntry, ConnectionEntry connectionEntry)
		{
			LOG( "ConnectionEntry_DisconnectionTimeout() - Start" );
			lock( this )
			{
				System.Diagnostics.Debug.Assert( taskEntry != null, "Missing parameter 'taskEntry'" );
				System.Diagnostics.Debug.Assert( connectionEntry != null, "Missing parameter 'connectionEntry'" );
				if( (connectionEntry.DisconnectionTimeout == null)
					|| (connectionEntry.DisconnectionTimeout.ID != taskEntry.ID) )
				{
					System.Diagnostics.Debug.Fail( "The state of the ConnectionEntry changed between the trigger of the timeout and the Locker.EnterWriteLock() in this method" );
					// ^^ Chances this happens are very low. This is more a sign of something wrong is going on.

					// This timeout trigger is not valid anymore => discard it
					return;
				}
				LOG( "ConnectionEntry_DisconnectionTimeout('" + taskEntry + "', " + connectionEntry.ConnectionID + ") - Lock aquired" );

				// Discard the TaskEntry that just triggered
				//connectionEntry.DisconnectionTimeout.Dispose(); <= Don't dispose it, it is still running, it's this one!!!
				connectionEntry.DisconnectionTimeout = null;

				var connectionID = connectionEntry.ConnectionID;
				var sessionID = connectionEntry.SessionID;
				System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(connectionID), "'connectionEntry.ConnectionID' is missing" );
				System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(sessionID), "'connectionEntry.SessionID' is missing" );
				CheckValidity();
				System.Diagnostics.Debug.Assert( AllConnections.ContainsKey(connectionID), "This ConnectionEntry is not handled by this ConnectionList" );
				System.Diagnostics.Debug.Assert( connectionEntry.Connection == null, "We are supposed to declare a long-polling HTTP connection definately disconnected. An active Connection is not supposed to be present here" );

				// This connection is considered definately lost => Remove it from 'AllSessions' ...
				var connectionList = new List<string>();
				if(! AllSessions.TryGetValue(sessionID, out connectionList) )
				{
					System.Diagnostics.Debug.Fail( "The connection '" + connectionID + "' is reqested to be removed but SessionID '" + sessionID + "' is not in 'AllSessions'" );
				}
				else
				{
					if(! connectionList.Remove(connectionID) )
						System.Diagnostics.Debug.Fail( "The connection '" + connectionID + "' is reqested to be removed but is not in 'AllSessions'" );
				}

				// ... and from 'AllConnections' ...
				if(! AllConnections.Remove(connectionID) )
				{
					System.Diagnostics.Debug.Fail( "The connection '" + connectionID + "' is reqested to be removed but is not in 'AllConnections'" );
				}

				// ... and Dispose() it.
				connectionEntry.Dispose();

				CheckValidity();
				LOG( "ConnectionEntry_DisconnectionTimeout('" + taskEntry + "', " + connectionEntry.ConnectionID + ") - Exit" );
			}
		}

		private void ConnectionEntry_StaleTimeout(Utils.TaskEntry taskEntry, ConnectionEntry connectionEntry)
		{
			LOG( "ConnectionEntry_StaleTimeout() - Start" );
			lock( this )
			{
				System.Diagnostics.Debug.Assert( connectionEntry != null, "Missing parameter 'connectionEntry'" );

				if( (connectionEntry.StaleTimeout == null)
				 || (connectionEntry.StaleTimeout.ID != taskEntry.ID) )
				{
					System.Diagnostics.Debug.Fail( "The state of the ConnectionEntry changed between the trigger of the timeout and the Locker.EnterWriteLock() in this method" );
					// ^^ Chances this happens are very low. This is more a sign of something wrong is going on.

					// This timeout trigger is not valid anymore => discard it
					return;
				}
				LOG( "ConnectionEntry_StaleTimeout('" + taskEntry + "', " + connectionEntry.ConnectionID + ") - Lock aquired" );

				// Discard the TaskEntry that just triggered
				//connectionEntry.StaleTimeout.Dispose(); <= Don't dispose it, it is still running, it's this one!!!
				connectionEntry.StaleTimeout = null;

				var connectionID = connectionEntry.ConnectionID;
				var sessionID = connectionEntry.SessionID;
				System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(connectionID), "'connectionEntry.ConnectionID' is missing" );
				System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(sessionID), "'connectionEntry.SessionID' is missing" );
				CheckValidity();
				System.Diagnostics.Debug.Assert( AllConnections.ContainsKey(connectionID), "This ConnectionEntry is not handled by this ConnectionList" );
				System.Diagnostics.Debug.Assert( connectionEntry.Connection != null, "We are supposed to declare a long-polling HTTP connection stale. An active Connection is supposed to be present here" );

				// Ask the peer to reconnect
				connectionEntry.Connection.SendReset();
				connectionEntry.Connection = null;

				// Start the DisconnectionTimeout
				LOG( "ConnectionEntry_StaleTimeout('" + taskEntry + "', " + connectionEntry.ConnectionID + ") - Creating DisconnectionTimeout" );
				connectionEntry.DisconnectionTimeout = TasksQueue.CreateTask( 0, 0, 0, DisconnectionSeconds, 0, (taskEntryParm)=>{ConnectionEntry_DisconnectionTimeout(taskEntryParm, connectionEntry);} );
				LOG( "ConnectionEntry_StaleTimeout('" + taskEntry + "', " + connectionEntry.ConnectionID + ") - Created DisconnectionTimeout " + connectionEntry.DisconnectionTimeout );

				CheckValidity();
				LOG( "ConnectionEntry_StaleTimeout('" + taskEntry + "', " + connectionEntry.ConnectionID + ") - Exit" );
			}
		}

		/// <summary>
		/// Check the validity of the lists and properties managed by this object.<br/>
		/// Call this method only inside a Locker.Enter{Read|Write}Lock() statement.
		/// </summary>
		/// <remarks>This method is compiled only in release mode</remarks>
		[System.Diagnostics.Conditional("DEBUG")]
		private void CheckValidity()
		{
			if( AllSessions == null )
			{
				System.Diagnostics.Debug.Fail( "Property 'AllSessions' is null" );
				return;
			}
			if( AllConnections == null )
			{
				System.Diagnostics.Debug.Fail( "Property 'AllConnections' is null" );
				return;
			}

			// Check 'AllConnections'
			foreach( var connItem in AllConnections )
			{
				System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(connItem.Key), "'AllConnections' contains an empty ConnectionID" );
				if( connItem.Value == null )
				{
					System.Diagnostics.Debug.Fail( "'AllConnections contain an empty entry'" );
					continue;
				}
				System.Diagnostics.Debug.Assert( string.Equals(connItem.Key, connItem.Value.ConnectionID), "An entry in 'AllConnections' does not match its dictionary key" );
				System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(connItem.Value.SessionID), "An entry in 'AllConnections' misses its SessionID" );
				System.Diagnostics.Debug.Assert( AllSessions.ContainsKey(connItem.Value.SessionID), "'AllConnections' contains an entry with its SessionID that is not in 'AllSessions'" );
			}

			// Check 'AllSessions'
			int count = 0;
			foreach( var sessItem in AllSessions )
			{
				System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(sessItem.Key), "'AllSessions' contains an empty SessionID" );
				if( sessItem.Value == null )
				{
					System.Diagnostics.Debug.Fail( "'AllConnections contain an empty entry'" );
					continue;
				}
				foreach( var connectionID in sessItem.Value )
				{
					System.Diagnostics.Debug.Assert( AllConnections.ContainsKey(connectionID), "'AllSessions' contains a ConnectionID that is not in 'AllConnections'" );
					++ count;
				}
			}
			System.Diagnostics.Debug.Assert( count == AllConnections.Count, "Count in 'AllSessions' does not match count in 'AllConnections'" );
		}



/*
SendMessageToConnection(receiverConnectionID, object message)
{
	!!! reset StaleTimeout !!!
}
SendMessageToSession(receiverSessionID, object message)
{
	!!! reset StaleTimeout !!!
}
*/






//        internal void UnregisterConnection(IConnection connection)
//        {
//            Locker.EnterWriteLock();
//            try
//            {
//                System.Diagnostics.Debug.Assert( connection != null, "Missing parameter 'connection'" );
//                System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(connection.ConnectionID), "The connection has no 'ConnectionID'" );
//                System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(connection.SessionID), "The connection has no 'SessionID'" );

//                List<IConnection> sessionConnections;
//                if(! Sessions.TryGetValue(connection.SessionID, out sessionConnections) )
//                {
//                    System.Diagnostics.Debug.Fail( "Could not find the session '" + connection.SessionID + "' associated with the connection '" + connection.ConnectionID + "'" );
//                }
//                else
//                {
//                    if(! sessionConnections.Remove(connection) )
//                        System.Diagnostics.Debug.Fail( "Connection '" + connection.ConnectionID + "' is not registered in the session '" + connection.SessionID + "'" );
//                }

//                Connections.Remove( connection.ConnectionID );
//            }
//            finally
//            {
//                Locker.ExitWriteLock();
//            }
//        }

		//internal LockerDisposable GetConnection(string connectionID, bool writeAccess, out IConnection connection)
		//{
		//    var lockerDisposable = new LockerDisposable( Locker, writeAccess );
		//    bool finished = false;
		//    try
		//    {
		//        System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(connectionID), "Missing parameter 'connectionID'" );

		//        connection = Connections[ connectionID ];
		//        finished = true;
		//        return lockerDisposable;
		//    }
		//    finally
		//    {
		//        if(! finished )
		//            lockerDisposable.Dispose();
		//    }
		//}

		//internal LockerDisposable GetConnectionsInSession(string sessionID, bool writeAccess, out IConnection[] connectionsList)
		//{
		//    var lockerDisposable = new LockerDisposable( Locker, writeAccess );
		//    bool finished = false;
		//    try
		//    {
		//        System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(sessionID), "Missing parameter 'sessionID'" );

		//        List<IConnection> sessionConnections;
		//        if( Sessions.TryGetValue(sessionID, out sessionConnections) )
		//        {
		//            connectionsList = sessionConnections.ToArray();
		//        }
		//        else
		//        {
		//            System.Diagnostics.Debug.Fail( "Session '" + sessionID + "' is not registered" );
		//            connectionsList = new IConnection[]{};
		//        }

		//        finished = true;
		//        return lockerDisposable;
		//    }
		//    finally
		//    {
		//        if(! finished )
		//            lockerDisposable.Dispose();
		//    }
		//}
	}
}
