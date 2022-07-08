﻿//
// CommonLibs/Web/LongPolling/ConnectionList.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2013 Alain CAO
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Web;

using CommonLibs.Utils;
using CommonLibs.Utils.Tasks;

namespace CommonLibs.Web.LongPolling
{
	// Warning: MessageHandler and ConnectionList are heavily dependents.
	// To any avoid dead-locks, all these interactions must be performed outside their respective locks() (or Enter{Read|Write}Lock())

	public class ConnectionList
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		private sealed class ConnectionEntry : IDisposable
		{
			internal ConnectionList					ConnectionList					{ get; private set; }
			internal string							SessionID						{ get; private set; }
			internal string							ConnectionID					{ get; private set; }
			internal IConnection					Connection						= null;
			internal bool							Available						{ get { return (Connection != null) && (!Connection.Sending) && (!Disposed); } }
			internal bool							Disposed						{ get; private set; }
			internal TaskEntry						DisconnectionTimeout			= null;
			internal TaskEntry						StaleTimeout					= null;
			internal Dictionary<string,object>		CustomObjects					{ get { return customObjects ?? (customObjects = new Dictionary<string,object>()); } }
			private Dictionary<string,object>		customObjects					= null;

			internal ConnectionEntry(ConnectionList connectionList, string sessionID, string connectionID)
			{
				CommonLibs.Utils.Debug.ASSERT( connectionList != null, this, "Missing parameter 'connectionList'" );
				CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(connectionID), this, "Missing parameter 'connectionID'" );

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
					ConnectionList.FAIL( "Disposing a ConnectionEntry but there is still a registered connection" );
					try { Connection.Close( RootMessage.CreateServer_Reset() ); }
					catch( System.Exception ex ) { ConnectionList.FatalExceptionHandler( "Call to Connection.Close() threw an exception", ex ); }
				}
				if( DisconnectionTimeout != null )
				{
					try { DisconnectionTimeout.Remove(); }
					catch( System.Exception ex ) { ConnectionList.FatalExceptionHandler( "Call to DisconnectionTimeout.Remove() threw an exception", ex ); }
					DisconnectionTimeout = null;
				}
				if( StaleTimeout != null )
				{
					try { StaleTimeout.Remove(); }
					catch( System.Exception ex ) { ConnectionList.FatalExceptionHandler( "Call to StaleTimeout.Remove() threw an exception", ex ); }
					StaleTimeout = null;
				}
			}
		}

		private sealed class SessionEntry
		{
			internal List<string>					ConnectionIDs					= new List<string>();
			internal Dictionary<string,object>		CustomObjects					{ get { return customObjects ?? (customObjects = new Dictionary<string,object>()); } }
			private Dictionary<string,object>		customObjects					= null;
		}

		public static string						AspSessionCookie				{ get { return ( aspSessionCookie ?? (aspSessionCookie = GetSessionCookieName()) ); } set { aspSessionCookie = value; } }
		private static string						aspSessionCookie				= null;  // NB: Default: "ASP.NET_SessionId"

		private object								LockObject						{ get { return AllConnections; } }

		/// <summary>If a connection stays disconnected more than this amount of seconds, it is considered definitely disconnected</summary>
		public int									DisconnectionSeconds			{ get; set; } = 15;
		/// <summary>If a polling connections stays inactive more than this amount of seconds, it is closed so the client can reopen it</summary>
		public int									StaleConnectionSeconds			{ get; set; } = 15;

		public CommonLibs.Utils.Tasks.TasksQueue	TasksQueue						{ get; private set; }

		/// <summary>
		/// Key=ConnectionID ; Value=The ConnectionEntry
		/// </summary>
		private readonly Dictionary<string,ConnectionEntry>	AllConnections			= new Dictionary<string,ConnectionEntry>();

		/// <summary>
		/// Key=SessionID ; Value: List of ConnectionIDs
		/// </summary>
		private readonly Dictionary<string,SessionEntry>	AllSessions				= new Dictionary<string,SessionEntry>();

		/// <summary>
		/// Bind to this event to accept or reject incomming connections from clients<br/>
		/// If there are callbacks assigned then they are called for each 'init' messages received.<br/>
		/// If all the callbacks return false, then the connection will be refused to the client.<br/>
		/// If at least one of them returns true, then the 'init' will be accepted and a new ConnectionID will be replied to the client.<br/>
		/// Parameter 1: The message's dictionnary received from the client's 'init' message.<br/>
		/// Parameter 2: The SessionID
		/// Returns: true if the 'init' must be accepted.
		/// </summary>
		/// <remarks>Adding/removing callbacks to this event is NOT thread-safe and so should be done once at application initialization</remarks>
		public event Func<IDictionary<string,object>,string,bool>	CheckInit			{ add { CheckInitCallbacks.Add(value); } remove { var rc=CheckInitCallbacks.Remove(value); ASSERT( rc, "Failed to remove CheckInit event's callback " + value ); } }
		private readonly List<Func<IDictionary<string,object>,string,bool>>	CheckInitCallbacks	= new List<Func<IDictionary<string,object>,string,bool>>();

		/// <summary>
		/// Triggered when a session has been allocated (when a connection not yet associated to any session has been received)<br/>
		/// Parameter 1: The SessionID that just got allocated.
		/// </summary>
		public event Action<string>					SessionAllocated				{ add { SessionAllocatedAddCallback(value); } remove { SessionAllocatedRemoveCallback(value); } }
		private readonly List<Action<string>>		SessionAllocatedCallbacks		= new List<Action<string>>();

		/// <summary>
		/// Triggered when an ASP session and all its associated connections were discarded<br/>
		/// Parameter 1: The SessionID that just got discarded.
		/// </summary>
		public event Action<string>					SessionClosed					{ add { SessionClosedAddCallback(value); } remove { SessionClosedRemoveCallback(value); } }
		private readonly List<Action<string>>		SessionClosedCallbacks			= new List<Action<string>>();

		/// <summary>
		/// Triggered when a new ConnectionID has been allocated by this ConnectionList.<br/>
		/// Parameter 1: The ConnectionID that just registered.
		/// </summary>
		public event Action<string>					ConnectionAllocated				{ add { ConnectionAllocatedAddCallback(value); } remove { ConnectionAllocatedRemoveCallback(value); } }
		private readonly List<Action<string>>		ConnectionAllocatedCallbacks	= new List<Action<string>>();

		/// <summary>
		/// Triggered when a long polling request has just connected to the server.<br:>
		/// Parameter 1: The ConnectionID that just connected.
		/// </summary>
		/// <remarks>Be aware that due to multithreading, the connection is not guaranteed to be available at the time of the event processing</remarks>
		public event Action<string>					ConnectionRegistered			{ add { ConnectionRegisteredAddCallback(value); } remove { ConnectionRegisteredRemoveCallback(value); } }
		private readonly List<Action<string>>		ConnectionRegisteredCallbacks	= new List<Action<string>>();

		/// <summary>
		/// Triggered when a ConnectionID is considered lost by this ConnectionList.<br/>
		/// Parameter 1: The lost ConnectionID.
		/// </summary>
// TODO: Alain: Event directly on the ConnectionEntry object (e.g. with a AddConnectionLost(connectionID,callback) method) so that there is no need to catch all events from all connections to monitor only 1 connection
		public event Action<string>					ConnectionLost					{ add { ConnectionLostAddCallback(value); } remove { ConnectionLostRemoveCallback(value); } }
		private readonly List<Action<string>>		ConnectionLostCallbacks			= new List<Action<string>>();

		protected Action<string,Exception>			FatalExceptionHandler			{ get; set; }

		public ConnectionList(CommonLibs.Utils.Tasks.TasksQueue tasksQueue)
		{
			LOG( "Constructor" );

			FatalExceptionHandler = DefaultFatalExceptionHandler;

			TasksQueue = tasksQueue;
		}

		/// <see cref="https://stackoverflow.com/questions/3739537/how-to-programmatically-get-session-cookie-name"/>
		public static string GetSessionCookieName()
		{
			var sessionStateSection = (System.Web.Configuration.SessionStateSection)System.Configuration.ConfigurationManager.GetSection("system.web/sessionState");
			return sessionStateSection.CookieName;
		}

		public static string GetSessionID(HttpContext httpContext)
		{
			CommonLibs.Utils.Debug.ASSERT( httpContext != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing paramter 'httpContext'" );

			var session = httpContext.Session;
			if( session != null )
			{
				return session.SessionID;
			}
			else
			{
				var request = httpContext.Request;
				CommonLibs.Utils.Debug.ASSERT( request != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Neither session or request are accessible" );
				var sessionID = request.Cookies[ AspSessionCookie ];
				return sessionID.Value;
			}
		}

		public static string GetSessionID(System.Web.WebSockets.AspNetWebSocketContext socketContext)
		{
			CommonLibs.Utils.Debug.ASSERT( socketContext != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing paramter 'socketContext'" );
			var sessionID = socketContext.Cookies[ AspSessionCookie ];
			return sessionID.Value;
		}

		private void DefaultFatalExceptionHandler(string description, Exception exception)
		{
			ASSERT( exception != null, "Missing parameter 'exception'" );
			string message = "" + description + ":" + exception;
			FAIL( message );
			LOG( message );
		}

		private void SessionAllocatedAddCallback(Action<string> callback)
		{
			lock( SessionAllocatedCallbacks )
			{
				SessionAllocatedCallbacks.Add( callback );
			}
		}

		private void SessionAllocatedRemoveCallback(Action<string> callback)
		{
			bool rc;
			lock( SessionAllocatedCallbacks )
			{
				rc = SessionAllocatedCallbacks.Remove( callback );
			}
			ASSERT( rc, "Failed to remove SessionAllocated event's callback " + callback );
		}

		private void SessionAllocatedInvokeCallbacks(string sessionID)
		{
			LOG( "SessionAllocatedInvokeCallbacks(" + sessionID + ") - Start" );
			Action<string>[] callbacks;
			lock( SessionAllocatedCallbacks )
			{
				LOG( "SessionAllocatedInvokeCallbacks(" + sessionID + ") - Lock acquired" );
				callbacks = SessionAllocatedCallbacks.ToArray();
			}
			LOG( "SessionAllocatedInvokeCallbacks(" + sessionID + ") - Calling " + SessionAllocatedCallbacks.Count + " callbacks" );
			foreach( var callback in callbacks )
			{
				try { callback( sessionID ); }
				catch( System.Exception ex ) { FatalExceptionHandler( "SessionAllocatedCallbacks callback threw an exception", ex ); }
			}

			LOG( "SessionAllocatedInvokeCallbacks(" + sessionID + ") - Exit" );
		}

		private void SessionClosedAddCallback(Action<string> callback)
		{
			lock( SessionClosedCallbacks )
			{
				SessionClosedCallbacks.Add( callback );
			}
		}

		private void SessionClosedRemoveCallback(Action<string> callback)
		{
			bool rc;
			lock( SessionClosedCallbacks )
			{
				rc = SessionClosedCallbacks.Remove( callback );
			}
			ASSERT( rc, "Failed to remove SessionClosed event's callback " + callback );
		}

		private void SessionClosedInvokeCallbacks(string sessionID)
		{
			LOG( "SessionClosedInvokeCallbacks(" + sessionID + ") - Start" );
			Action<string>[] callbacks;
			lock( SessionClosedCallbacks )
			{
				LOG( "SessionClosedInvokeCallbacks(" + sessionID + ") - Lock acquired" );
				callbacks = SessionClosedCallbacks.ToArray();
			}
			LOG( "SessionClosedInvokeCallbacks(" + sessionID + ") - Calling " + SessionClosedCallbacks.Count + " callbacks" );
			foreach( var callback in callbacks )
			{
				try { callback( sessionID ); }
				catch( System.Exception ex ) { FatalExceptionHandler( "SessionClosedCallbacks callback threw an exception", ex ); }
			}

			LOG( "SessionClosedInvokeCallbacks(" + sessionID + ") - Exit" );
		}

		private void ConnectionAllocatedAddCallback(Action<string> callback)
		{
			lock( ConnectionAllocatedCallbacks )
			{
				ConnectionAllocatedCallbacks.Add( callback );
			}
		}

		private void ConnectionAllocatedRemoveCallback(Action<string> callback)
		{
			bool rc;
			lock( ConnectionAllocatedCallbacks )
			{
				rc = ConnectionAllocatedCallbacks.Remove( callback );
			}
			ASSERT( rc, "Failed to remove ConnectionAllocated event's callback " + callback );
		}

		private void ConnectionAllocatedInvokeCallbacks(string connectionID)
		{
			LOG( "ConnectionAllocatedInvokeCallbacks(" + connectionID + ") - Start" );
			Action<string>[] callbacks;
			lock( ConnectionAllocatedCallbacks )
			{
				LOG( "ConnectionAllocatedInvokeCallbacks(" + connectionID + ") - Lock acquired" );

				callbacks = ConnectionAllocatedCallbacks.ToArray();
			}
			LOG( "ConnectionAllocatedInvokeCallbacks(" + connectionID + ") - Calling " + ConnectionAllocatedCallbacks.Count + " callbacks" );
			foreach( var callback in callbacks )
			{
				try { callback( connectionID ); }
				catch( System.Exception ex ) { FatalExceptionHandler( "ConnectionAllocated callback threw an exception", ex ); }
			}

			LOG( "ConnectionAllocatedInvokeCallbacks(" + connectionID + ") - Exit" );
		}

		private void ConnectionRegisteredAddCallback(Action<string> callback)
		{
			lock( ConnectionRegisteredCallbacks )
			{
				ConnectionRegisteredCallbacks.Add( callback );
			}
		}

		private void ConnectionRegisteredRemoveCallback(Action<string> callback)
		{
			bool rc;
			lock( ConnectionRegisteredCallbacks )
			{
				rc = ConnectionRegisteredCallbacks.Remove( callback );
			}
			ASSERT( rc, "Failed to remove ConnectionRegistered event's callback " + callback );
		}

		private void ConnectionRegisteredInvokeCallbacks(string connectionID)
		{
			LOG( "ConnectionRegisteredInvokeCallbacks(" + connectionID + ") - Start" );
			Action<string>[] callbacks;
			lock( ConnectionRegisteredCallbacks )
			{
				LOG( "ConnectionRegisteredInvokeCallbacks(" + connectionID + ") - Lock acquired " );
				callbacks = ConnectionRegisteredCallbacks.ToArray();
			}
			LOG( "ConnectionRegisteredInvokeCallbacks(" + connectionID + ") - Calling " + ConnectionRegisteredCallbacks.Count + " callbacks" );
			foreach( var callback in callbacks )
			{
				try { callback( connectionID ); }
				catch( System.Exception ex ) { FatalExceptionHandler( "ConnectionRegistered callback threw an exception", ex ); }
			}

			LOG( "ConnectionRegisteredInvokeCallbacks(" + connectionID + ") - Exit" );
		}

		private void ConnectionLostAddCallback(Action<string> callback)
		{
			ASSERT( callback != null, "Missing parameter 'callback'" );

			lock( ConnectionLostCallbacks )
			{
				ConnectionLostCallbacks.Add( callback );
			}
		}

		private void ConnectionLostRemoveCallback(Action<string> callback)
		{
			ASSERT( callback != null, "Missing parameter 'callback'" );

			bool rc;
			lock( ConnectionLostCallbacks )
			{
				rc = ConnectionLostCallbacks.Remove( callback );
			}
			ASSERT( rc, "Failed to remove ConnectionLost event's callback " + callback );
		}

		private void ConnectionLostInvokeCallbacks(string connectionID)
		{
			LOG( "ConnectionLostInvokeCallbacks(" + connectionID + ") - Start" );
			Action<string>[] callbacks;
			lock( ConnectionLostCallbacks )
			{
				LOG( "ConnectionLostInvokeCallbacks(" + connectionID + ") - Lock acquired" );
				callbacks = ConnectionLostCallbacks.ToArray();
			}
			LOG( "ConnectionLostInvokeCallbacks(" + connectionID + ") - Calling " + ConnectionLostCallbacks.Count + " callbacks" );
			foreach( var callback in callbacks )
			{
				try { callback( connectionID ); }
				catch( System.Exception ex ) { FatalExceptionHandler( "ConnectionLost callback threw an exception", ex ); }
			}

			LOG( "ConnectionLostInvokeCallbacks(" + connectionID + ") - Exit" );
		}

		public void SessionEnded(string sessionID)
		{
			ASSERT( !string.IsNullOrEmpty(sessionID), "Missing parameter 'sessionID'" );
			LOG( "SessionEnded(" + sessionID + ") - Start" );

			var customObjects = new List<object>();

			var finalizeActions = new List<Action>();
			lock( LockObject )
			{
				LOG( "SessionEnded(" + sessionID + ") - Lock aquired" );

				CheckValidity();

				SessionEntry sessionEntry;
				if(! AllSessions.TryGetValue(sessionID, out sessionEntry) )
				{
					LOG( "SessionEnded(" + sessionID + ") - This session does not belong to this ConnectionList" );
					return;
				}
				if( sessionEntry == null )
				{
					FAIL( "The 'connectionList' is not supposed to be null here" );
					return;
				}

				customObjects.AddRange( sessionEntry.CustomObjects.Select(v=>v.Value) );

				// Close all connections in this session
				foreach( var connectionID in sessionEntry.ConnectionIDs )
				{
					try
					{
						ConnectionEntry connectionEntry;
						if(! AllConnections.TryGetValue(connectionID, out connectionEntry) )
						{
							FAIL( "Connection '" + connectionID + "' is not in 'AllConnections'" );
							continue;
						}
						AllConnections.Remove( connectionID );
						if( connectionEntry.Connection != null )
						{
							var connection = connectionEntry.Connection;
							connection.Sending = true;
							finalizeActions.Add( ()=>
								{
									// Send a logout message to the connection and close it
									try { connection.Close( RootMessage.CreateServer_Logout() ); }
									catch( System.Exception ex ){ FAIL( "Call to 'connection.Close()' threw an exception ("+ex.GetType().FullName+"): "+ex.Message ); }

									// Fire ConnectionLost event
									ConnectionLostInvokeCallbacks( connection.ConnectionID );
								} );
							connectionEntry.Connection = null;
						}
						customObjects.AddRange( connectionEntry.CustomObjects.Select(v=>v.Value) );
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

			// Execute all actions that must be performed outside the lock()
			foreach( var finalizeAction in finalizeActions )
				finalizeAction();

			// Fire SessionClosed event
			SessionClosedInvokeCallbacks( sessionID );

			// Dispose CustomObjects that are IDisposable
			if( customObjects != null )
			{
				foreach( var obj in customObjects )
				{
					var disposable = obj as IDisposable;
					if( disposable != null )
					{
						try { disposable.Dispose(); }
						catch(System.Exception ex)  { FAIL( "'disposable.Dispose()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
					}
				}
			}

			LOG( "SessionEnded(" + sessionID + ") - Exit" );
		}

		/// <summary>
		/// Called by the LongPollingHandler when it receives an 'init' message to check if this message must be accepted or not.
		/// </summary>
		/// <returns>True if the 'init' message must be accepted, false otherwise</returns>
		internal bool CheckInitAccepted(IDictionary<string,object> requestMessage, string sessionID)
		{
			// Trigger the CheckInit event to check if the 'init' must be accepted

			bool initAccepted = true;  // Default to 'true' so that if there is no callback, it will always be accepted
			foreach( var initCallback in CheckInitCallbacks )
			{
				initAccepted = initCallback( requestMessage, sessionID );
				if( initAccepted )
					// This callback accepted the 'init'. Don't need to call the others.
					break;
			}
			return initAccepted;
		}

		/// <param name="sessionID">The session this new ConnectionID will belong to</param>
		/// <returns>The new ConnectionID allocated</returns>
		public string AllocateNewConnectionID(string sessionID)
		{
			ASSERT( !string.IsNullOrEmpty(sessionID), "Missing parameter 'sessionID'" );
			LOG( "AllocateNewConnectionID(" + sessionID + ") - Start" );

			bool sessionAllocated = false;
			string connectionID;
			lock( LockObject )
			{
				LOG( "AllocateNewConnectionID(" + sessionID + ") - Lock aquired" );
				CheckValidity();

				SessionEntry sessionEntry;
				if(! AllSessions.TryGetValue(sessionID, out sessionEntry) )
				{
					LOG( "AllocateNewConnectionID(" + sessionID + ") - This is the first connection in the sesion ; Allocating a new SessionEntry" );
					sessionEntry = new SessionEntry();
					AllSessions.Add( sessionID, sessionEntry );
					sessionAllocated = true;
				}

				connectionID = Guid.NewGuid().ToString();
				var connectionEntry = new ConnectionEntry( this, sessionID, connectionID );
				AllConnections.Add( connectionID, connectionEntry );
				sessionEntry.ConnectionIDs.Add( connectionID );

				// The ConnectionID is connected but there is still no connection assigned to it => Must start a DisconnectionTimeout
				LOG( "AllocateNewConnectionID(" + sessionID + ") - Starting DisconnectionTimeout" );
				connectionEntry.DisconnectionTimeout = TasksQueue.CreateTask( 0, 0, 0, DisconnectionSeconds, 0, (taskEntry)=>{ConnectionEntry_DisconnectionTimeout(taskEntry, connectionEntry);} );
				LOG( "AllocateNewConnectionID(" + sessionID + ") - Started DisconnectionTimeout " + connectionEntry.DisconnectionTimeout );

				CheckValidity();
				LOG( "AllocateNewConnectionID(" + sessionID + ") - Release lock: " + connectionID );
			}//lock( LockObject )

			if( sessionAllocated )
				SessionAllocatedInvokeCallbacks( sessionID );
			ConnectionAllocatedInvokeCallbacks( connectionID );

			LOG( "AllocateNewConnectionID(" + sessionID + ") - EXIT" );
			return connectionID;
		}

		/// <returns>'false' if CheckValidity() returns false</returns>
		internal bool RegisterConnection(IConnection connection, bool startStaleTimeout)
		{
			ASSERT( connection != null, "Missing parameter 'connection'" );
			var connectionID = connection.ConnectionID;
			ASSERT( !string.IsNullOrEmpty(connectionID), "The connection has no 'ConnectionID'" );
			var sessionID = connection.SessionID;
			ASSERT( !string.IsNullOrEmpty(sessionID), "The connection has no 'SessionID'" );
			LOG( "RegisterConnection(" + connectionID + ") - Start" );

			var finalizeActions = new List<Action>();
			lock( LockObject )
			{
				LOG( "RegisterConnection(" + connectionID + ") - Lock aquired" );
				CheckValidity();

				if(! CheckConnectionIsValid(sessionID, connectionID) )
				{
					LOG( "RegisterConnection(" + connectionID + ") - *** Trying to register an invalid connection" );  // Happens often during development after rebuild... Assert.Fail() => LOG()
					return false;
				}
				ASSERT( AllConnections.ContainsKey(connectionID), "If the ConnectionIsValid then 'AllConnections' should contain the ConnectionID" );

				// Get the ConnectionEntry
				var connectionEntry = AllConnections[ connectionID ];

				if( connectionEntry.DisconnectionTimeout == null )
				{
					FAIL( "We are supposed to be registering a connection to a currently disconnected IConnection. A DisconnectionTimeout is supposed to be running here" );
				}
				else
				{
					// Stop the DisconnectionTimeout
					LOG( "RegisterConnection(" + connectionID + ") - Unregistering DisconnectionTimeout " + connectionEntry.DisconnectionTimeout );
					connectionEntry.DisconnectionTimeout.Remove();
					connectionEntry.DisconnectionTimeout = null;
				}

				if( connectionEntry.StaleTimeout != null )
				{
					FAIL( "We are supposed to be registering a connection to a currently disconnected IConnection. No StaleTimeout is supposed to be running here" );
					// Remove the (mistakenly) running StaleTimeout
					LOG( "RegisterConnection(" + connectionID + ") - Unregistering StaleTimeout '" + connectionEntry.StaleTimeout + "'" );
					connectionEntry.StaleTimeout.Remove();
					connectionEntry.StaleTimeout = null;
				}

				if( connectionEntry.Connection != null )
				{
					FAIL( "We are supposed to be registering a connection to a currently disconnected IConnection. No Connection is supposed to be present here" );
					// Close & discard the original connection
					var otherConnection = connectionEntry.Connection;
					finalizeActions.Add( ()=>
						{
							// Send a reset message to the connection and close it
							try { otherConnection.Close( RootMessage.CreateServer_Reset() ); }
							catch( System.Exception ex ){ FAIL( "Call to 'connection.Close()' threw an exception ("+ex.GetType().FullName+"): "+ex.Message ); }
						} );
					connectionEntry.Connection = null;
				}

				// Associate the IConnection to the ConnectionEntry
				connectionEntry.Connection = connection;

				if( startStaleTimeout )
				{
					// Enable the timeout to automatically disconnect so the connection does not become stale
					connectionEntry.StaleTimeout = TasksQueue.CreateTask( 0, 0, 0, StaleConnectionSeconds, 0, (taskEntry)=>{ConnectionEntry_StaleTimeout(taskEntry, connectionEntry);} );
					LOG( "RegisterConnection(" + connectionID + ") - Started StaleTimeout '" + connectionEntry.StaleTimeout + "'" );
				}

				CheckValidity();
			}//lock( LockObject )

			// Execute all actions that must be performed outside the lock()
			foreach( var finalizeAction in finalizeActions )
				finalizeAction();

			// Fire all ConnectionRegistered events
			ConnectionRegisteredInvokeCallbacks( connectionID );

			LOG( "RegisterConnection(" + connectionID + ") - Exit" );
			return true;
		}

		/// <summary>Unlink this IConnection from its ConnectionID</summary>
		internal void UnregisterConnection(IConnection connection)
		{
			ASSERT( connection != null, "Missing parameter 'connection'" );
			var connectionID = connection.ConnectionID;
			ASSERT( !string.IsNullOrEmpty(connectionID), "The connection has no 'ConnectionID'" );
			var sessionID = connection.SessionID;
			ASSERT( !string.IsNullOrEmpty(sessionID), "The connection has no 'SessionID'" );
			LOG( "UnregisterConnection(" + connectionID + ") - Start" );

			lock( LockObject )
			{
				LOG( "UnregisterConnection(" + connectionID + ") - Lock aquired" );
				CheckValidity();

				// Get the ConnectionEntry
				var connectionEntry = AllConnections.TryGet( connectionID );
				if( connectionEntry == null )
				{
					FAIL( "UnregisterConnection(" + connectionID + ") - Connection seems already unregistered" );
					goto EXIT;
				}
				if( connectionEntry.Connection != connection )
				{
					FAIL( (connectionEntry.Connection == null) ?
							("UnregisterConnection(" + connectionID + ") - There is no registered connection for this ConnectionID") :
							("UnregisterConnection(" + connectionID + ") - The specified connection is not the registered one") );
					goto EXIT;
				}
				connectionEntry.Connection = null;

				if( connectionEntry.StaleTimeout != null )
				{
					LOG( "UnregisterConnection(" + connectionID + ") - Unregistering StaleTimeout " + connectionEntry.StaleTimeout );
					connectionEntry.StaleTimeout.Remove();
					connectionEntry.StaleTimeout = null;
				}

				if( connectionEntry.DisconnectionTimeout != null )
				{
					FAIL( "We are supposed to be unregistering a connection. No DisconnectionTimeout is supposed to be running here" );
					connectionEntry.DisconnectionTimeout.Remove();
					connectionEntry.DisconnectionTimeout = null;
				}

				LOG( "UnregisterConnection(" + connectionID + ") - Starting DisconnectionTimeout" );
				connectionEntry.DisconnectionTimeout = TasksQueue.CreateTask( 0, 0, 0, DisconnectionSeconds, 0, (taskEntry)=>{ConnectionEntry_DisconnectionTimeout(taskEntry, connectionEntry);} );
				LOG( "UnregisterConnection(" + connectionID + ") - Started DisconnectionTimeout " + connectionEntry.DisconnectionTimeout );

				CheckValidity();
			}
		EXIT:

			LOG( "UnregisterConnection(" + connectionID + ") - Exit" );
		}

		public bool CheckSessionIsValid(string sessionID)
		{
			LOG( "CheckSessionIsValid(" + sessionID + ") - Start" );
			bool rc;
			lock( LockObject )
			{
				LOG( "CheckSessionIsValid(" + sessionID + ") - Lock aquired" );
				CheckValidity();

				rc = AllSessions.ContainsKey( sessionID );
			}
			LOG( "CheckSessionIsValid(" + sessionID + ") - Exit - " + rc );
			return rc;
		}

		public bool CheckConnectionIsValid(string sessionID, string connectionID)
		{
			LOG( "CheckConnectionIsValid(" + sessionID + ", " + connectionID + ") - Start" );
			bool rc;
			lock( LockObject )
			{
				LOG( "CheckConnectionIsValid(" + sessionID + ", " + connectionID + ") - Lock aquired" );
				CheckValidity();

				SessionEntry sessionEntry;
				if(! AllSessions.TryGetValue(sessionID, out sessionEntry) )
					rc = false;
				else
					rc = sessionEntry.ConnectionIDs.Contains( connectionID );
			}
			LOG( "CheckConnectionIsValid(" + sessionID + ", " + connectionID + ") - Exit - " + rc );
			return rc;
		}

		/// <summary>
		/// Check if the specified connection is registered.
		/// </summary>
		public bool CheckConnectionExists(string connectionID)
		{
			LOG( "CheckConnectionIsAvailable(" + connectionID + ") - Start" );
			bool rc;
			lock( LockObject )
			{
				ConnectionEntry connectionEntry;
				if( AllConnections.TryGetValue(connectionID, out connectionEntry) )
					rc = true;
				else
					rc = false;
			}
			LOG( "CheckConnectionIsAvailable(" + connectionID + ") - Exit: " + rc );
			return rc;
		}

		/// <summary>
		/// Check if the specified connection is registered and its associated long polling request is currently connected to the server.
		/// </summary>
		public bool CheckConnectionIsAvailable(string connectionID)
		{
			LOG( "CheckConnectionIsAvailable(" + connectionID + ") - Start" );
			bool rc;
			lock( LockObject )
			{
				ConnectionEntry connectionEntry;
				if( AllConnections.TryGetValue(connectionID, out connectionEntry) )
				{
					if( connectionEntry.Available )
						rc = true;
					else
						rc = false;
				}
				else
				{
					rc = false;
				}
			}
			LOG( "CheckConnectionIsAvailable(" + connectionID + ") - Exit: " + rc );
			return rc;
		}

		/// <param name="createNewCallback">The callback to create the custom object if it has not been registered yet. NB: Since the whole ConnectionList is locked during the invokation, this callback must be fast to execute.</param>
		/// <returns>The custom object registered on this 'connectionID'. Null if there is no custom object registered yet</returns>
		/// <exception cref="KeyNotFoundException">When the connectionID does not exist</exception>
		public object GetConnectionCustomObject(string connectionID, string key, Func<object> createNewCallback=null)
		{
			lock( LockObject )
			{
				ConnectionEntry connectionEntry;
				if(! AllConnections.TryGetValue(connectionID, out connectionEntry) )
					throw new KeyNotFoundException( "The connection ID '" + connectionID + "' could not be found" );

				object customObject;
				if( connectionEntry.CustomObjects.TryGetValue(key, out customObject) )
					// Custom object found
					return customObject;

				if( createNewCallback == null )
					// Not found and don't add it
					return null;

				customObject = createNewCallback();
				connectionEntry.CustomObjects[ key ] = customObject;
				return customObject;
			}
		}

		/// <returns>True if the custom object has been successully unregistered ; false if it was not found.</returns>
		/// <exception cref="KeyNotFoundException">When the connectionID does not exist</exception>
		public bool UnregisterConnectionCustomObject(string connectionID, string key)
		{
			lock( LockObject )
			{
				ConnectionEntry connectionEntry;
				if(! AllConnections.TryGetValue(connectionID, out connectionEntry) )
					throw new KeyNotFoundException( "The connection ID '" + connectionID + "' could not be found" );
				return connectionEntry.CustomObjects.Remove( key );
			}
		}

		/// <param name="createNewCallback">The callback to create the custom object if it has not been registered yet. NB: Since the whole ConnectionList is locked during the invokation, this callback must be fast to execute.</param>
		/// <returns>The custom object registered on this 'sessionID'. Null if there is no custom object registered yet</returns>
		/// <exception cref="KeyNotFoundException">When the sessionID does not exist</exception>
		/// <remarks>Since a session can last quite long (longer than a ConnectionCustomObject), 'UnregisterSessionCustomObject()' should be called when the object is no more used to avoid memory leaks</remarks>
		public object GetSessionCustomObject(string sessionID, string key, Func<object> createNewCallback=null)
		{
			lock( LockObject )
			{
				SessionEntry sessionEntry;
				if(! AllSessions.TryGetValue(sessionID, out sessionEntry) )
					throw new KeyNotFoundException( "The session ID '" + sessionID + "' could not be found" );

				object customObject;
				if( sessionEntry.CustomObjects.TryGetValue(key, out customObject) )
					// Custom object found
					return customObject;

				if( createNewCallback == null )
					// Not found and don't add it
					return null;

				customObject = createNewCallback();
				sessionEntry.CustomObjects[ key ] = customObject;
				return customObject;
			}
		}

		/// <returns>True if the custom object has been successully unregistered ; false if it was not found.</returns>
		/// <exception cref="KeyNotFoundException">When the connectionID does not exist</exception>
		public bool UnregisterSessionCustomObject(string sessionID, string key)
		{
			lock( LockObject )
			{
				SessionEntry sessionEntry;
				if(! AllSessions.TryGetValue(sessionID, out sessionEntry) )
					throw new KeyNotFoundException( "The session ID '" + sessionID + "' could not be found" );
				return sessionEntry.CustomObjects.Remove( key );
			}
		}

		public string GetSessionID(string connectionID)
		{
			ASSERT( !string.IsNullOrEmpty(connectionID), "Missing parameter 'connectionID'" );
			string sessionID;
			lock( LockObject )
			{
				ConnectionEntry connectionEntry;
				if(! AllConnections.TryGetValue(connectionID, out connectionEntry) )
				{
					FAIL( "Could not find connection '" + connectionID + "'" );
					return null;
				}
				ASSERT( connectionEntry != null, "AllConnections.TryGetValue() returned null" );
				sessionID = connectionEntry.SessionID;
			}
			ASSERT( !string.IsNullOrEmpty(sessionID), "The SessionID of the connection '" + connectionID + "' is '" + ((sessionID == null) ? "NULL" : "EMPTY") + "'" );
			return sessionID;
		}

		/// <returns>The list of ConnecionIDs assigned to the specified sessionID if any ; an empty list if none ; 'null' if the session does not exists</returns>
		public string[] GetSessionConnectionIDs(string sessionID)
		{
			ASSERT( !string.IsNullOrEmpty(sessionID), "Missing parameter 'sessionID'" );

			string[] connectionArray;
			lock( LockObject )
			{
				SessionEntry sessionEntry;
				if(! AllSessions.TryGetValue(sessionID, out sessionEntry) )
				{
					//FAIL( "Session '" + sessionID + "' could not be found" );  <= In case of login/logout during development, this happens too often.
					return null;
				}
				ASSERT( sessionEntry != null, "AllSessions.TryGetValue('" + sessionID + "') returned null" );
				connectionArray = sessionEntry.ConnectionIDs.ToArray();
			}
			return connectionArray;
		}

		/// <summary>
		/// Search for the requested connection and invokes a callback if this connection is registered and its associated long polling request is currently connected to the server.
		/// </summary>
		/// <param name="connectionID">The ConnectionID requested</param>
		/// <returns>True if the requested connection was available the callback has been called.</returns>
		internal bool SendMessagesToConnectionIfAvailable(string connectionID, IEnumerable<Message> messages, out bool connectionIdExists)
		{
			LOG( "SendMessagesToConnectionIfAvailable(" + connectionID + ") - Start" );
			IConnection connection;
			ConnectionEntry connectionEntry;
			lock( LockObject )
			{
				if(! AllConnections.TryGetValue(connectionID, out connectionEntry) )
				{
					LOG( "SendMessagesToConnectionIfAvailable(" + connectionID + ") - Exit - Connection is not registered!" );
					connectionIdExists = false;
					return false;
				}
				else
				{
					connectionIdExists = true;
				}

				if(! connectionEntry.Available )
				{
					LOG( "SendMessagesToConnectionIfAvailable(" + connectionID + ") - Exit - Connection is not available" );
					return false;
				}

				LOG( "SendMessagesToConnectionIfAvailable(" + connectionID + ") - Sending messages" );
				ASSERT( connectionEntry.Connection != null, "If connectionEntry.Available, then connectionEntry.Connection is supposed to be set" );
				connection = connectionEntry.Connection;

				if( connectionEntry.StaleTimeout == null )
				{
					#if( DEBUG )
						if( connectionEntry.Connection is LongPollingConnection )
							FAIL( "We are sending messages through the polling connection ; A StaleTimeout is supposed to be running here" );
					#endif
				}
				else
				{
					LOG( "SendMessagesToConnectionIfAvailable(" + connectionID + ") - Unregistering StaleTimeout '" + connectionEntry.StaleTimeout + "'" );
					connectionEntry.StaleTimeout.Remove();
					connectionEntry.StaleTimeout = null;
				}

				ASSERT( connectionEntry.DisconnectionTimeout == null, "We are sending messages and a connection is available ; No DisconnectionTimeout is supposed to be running here" );

				connection.Sending = true;
			}// lock( LockObject )

			// Send messages (NB: must be outside any lock()s )
			connection.SendRootMessage( RootMessage.CreateServer_MessagesList(messages) );  // NB: May unregister the connection (When it is an HTTP connection)

			LOG( "SendMessagesToConnectionIfAvailable(" + connectionID + ") - Exit" );
			return true;
		}

		private void ConnectionEntry_DisconnectionTimeout(CommonLibs.Utils.Tasks.TaskEntry taskEntry, ConnectionEntry connectionEntry)
		{
			LOG( "ConnectionEntry_DisconnectionTimeout() - Start" );

			var customObjects = new List<object>();

			string connectionID;
			lock( LockObject )
			{
				ASSERT( taskEntry != null, "Missing parameter 'taskEntry'" );
				ASSERT( connectionEntry != null, "Missing parameter 'connectionEntry'" );
				if( (connectionEntry.DisconnectionTimeout == null)
					|| (connectionEntry.DisconnectionTimeout.ID != taskEntry.ID) )
				{
					LOG( "*** The state of the ConnectionEntry changed between the trigger of the timeout and the Locker.EnterWriteLock() in this method" );
					// ^^ Chances this happens are very low. This is more a sign of something wrong is going on.

					// This timeout trigger is not valid anymore => discard it
					return;
				}
				LOG( "ConnectionEntry_DisconnectionTimeout('" + taskEntry + "', " + connectionEntry.ConnectionID + ") - Lock aquired" );

				// Discard the TaskEntry that just triggered
				//connectionEntry.DisconnectionTimeout.Dispose(); <= Don't dispose it, it is still running, it's this one!!!
				connectionEntry.DisconnectionTimeout = null;

				connectionID = connectionEntry.ConnectionID;
				var sessionID = connectionEntry.SessionID;
				ASSERT( !string.IsNullOrEmpty(connectionID), "'connectionEntry.ConnectionID' is missing" );
				ASSERT( !string.IsNullOrEmpty(sessionID), "'connectionEntry.SessionID' is missing" );
				CheckValidity();
				ASSERT( AllConnections.ContainsKey(connectionID), "This ConnectionEntry is not handled by this ConnectionList" );

				// This connection is considered definitely lost => Remove it from 'AllSessions' ...
				SessionEntry sessionEntry;
				if(! AllSessions.TryGetValue(sessionID, out sessionEntry) )
				{
					FAIL( "The connection '" + connectionID + "' is reqested to be removed but SessionID '" + sessionID + "' is not in 'AllSessions'" );
				}
				else
				{
					if(! sessionEntry.ConnectionIDs.Remove(connectionID) )
						FAIL( "The connection '" + connectionID + "' is reqested to be removed but is not in 'AllSessions'" );
				}

				// ... and from 'AllConnections' ...
				if(! AllConnections.Remove(connectionID) )
				{
					FAIL( "The connection '" + connectionID + "' is reqested to be removed but is not in 'AllConnections'" );
				}

				// ... and Dispose() it.
				customObjects.AddRange( connectionEntry.CustomObjects.Select(v=>v.Value) );
				ASSERT( connectionEntry.Connection == null, "We are supposed to declare a long-polling HTTP connection definitely disconnected. An active Connection is not supposed to be present here" );
				connectionEntry.Connection = null;  // Discard the connection if it exists anyways
				connectionEntry.Dispose();

				CheckValidity();
				LOG( "ConnectionEntry_DisconnectionTimeout('" + taskEntry + "', " + connectionEntry.ConnectionID + ") - Release lock" );
			}

			// Fire ConnectionLost event
			ConnectionLostInvokeCallbacks( connectionID );

			// Dispose CustomObjects that are IDisposable
			foreach( var obj in customObjects )
			{
				var disposable = obj as IDisposable;
				if( disposable != null )
				{
					try { disposable.Dispose(); }
					catch(System.Exception ex)  { FAIL( "'disposable.Dispose()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
				}
			}

			LOG( "ConnectionEntry_DisconnectionTimeout('" + taskEntry + "', " + connectionEntry.ConnectionID + ") - Exit" );
		}

		private void ConnectionEntry_StaleTimeout(CommonLibs.Utils.Tasks.TaskEntry taskEntry, ConnectionEntry connectionEntry)
		{
			LOG( "ConnectionEntry_StaleTimeout() - Start" );
			IConnection connection;
			RootMessage messageToSend;
			lock( LockObject )
			{
				ASSERT( connectionEntry != null, "Missing parameter 'connectionEntry'" );

				if( (connectionEntry.StaleTimeout == null)
				 || (connectionEntry.StaleTimeout.ID != taskEntry.ID) )
				{
					LOG( "*** The state of the ConnectionEntry changed between the trigger of the timeout and the Locker.EnterWriteLock() in this method" );
					// ^^ Chances this happens are very low. This is more a sign of something wrong is going on.

					// This timeout trigger is not valid anymore => discard it
					return;
				}
				LOG( "ConnectionEntry_StaleTimeout('" + taskEntry + "', " + connectionEntry.ConnectionID + ") - Lock aquired" );

				// Discard the TaskEntry that just triggered
				//connectionEntry.StaleTimeout.Dispose(); <= Don't dispose it, it is still running, it's this one!!!
				connectionEntry.StaleTimeout = null;

				// Send a 'reset' message to peer
				messageToSend = RootMessage.CreateServer_Reset();
				connection = connectionEntry.Connection;

				connection.Sending = true;

				CheckValidity();
				LOG( "ConnectionEntry_StaleTimeout('" + taskEntry + "', " + connectionEntry.ConnectionID + ") - Exit" );
			}

			// Send messages (NB: must be outside any lock()s )
			connection.SendRootMessage( messageToSend );
		}

		/// <summary>
		/// Check the validity of the lists and properties managed by this object.<br/>
		/// Call this method only inside a Locker.Enter{Read|Write}Lock() statement.
		/// </summary>
		/// <remarks>This method is compiled only in debug mode</remarks>
		[System.Diagnostics.Conditional("DEBUG")]
		private void CheckValidity()
		{
			if( AllSessions == null )
			{
				FAIL( "Property 'AllSessions' is null" );
				return;
			}
			if( AllConnections == null )
			{
				FAIL( "Property 'AllConnections' is null" );
				return;
			}

			// Check 'AllConnections'
			foreach( var connItem in AllConnections )
			{
				ASSERT( !string.IsNullOrEmpty(connItem.Key), "'AllConnections' contains an empty ConnectionID" );
				if( connItem.Value == null )
				{
					FAIL( "'AllConnections contain an empty entry'" );
					continue;
				}
				ASSERT( string.Equals(connItem.Key, connItem.Value.ConnectionID), "An entry in 'AllConnections' does not match its dictionary key" );
				ASSERT( !string.IsNullOrEmpty(connItem.Value.SessionID), "An entry in 'AllConnections' misses its SessionID" );
				ASSERT( AllSessions.ContainsKey(connItem.Value.SessionID), "'AllConnections' contains an entry with its SessionID that is not in 'AllSessions'" );
			}

			// Check 'AllSessions'
			int count = 0;
			foreach( var sessItem in AllSessions )
			{
				ASSERT( !string.IsNullOrEmpty(sessItem.Key), "'AllSessions' contains an empty SessionID" );
				if( sessItem.Value == null )
				{
					FAIL( "'AllConnections contain an empty entry'" );
					continue;
				}
				foreach( var connectionID in sessItem.Value.ConnectionIDs )
				{
					ASSERT( AllConnections.ContainsKey(connectionID), "'AllSessions' contains a ConnectionID that is not in 'AllConnections'" );
					++ count;
				}
			}
			ASSERT( count == AllConnections.Count, "Count in 'AllSessions' does not match count in 'AllConnections'" );
		}
	}
}
