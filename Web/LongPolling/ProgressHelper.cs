//
// CommonLibs/Web/LongPolling/ProgressHelper.cs
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
using System.Linq;
using System.Web;

using CommonLibs.Web.LongPolling;
using CommonLibs.Web.LongPolling.Utils;

namespace CommonLibs.Web.LongPolling
{
	/// <remarks>All instances of this class must be disposed using 'using(){}' clauses.</remarks>
	public abstract class ProgressHelper : IDisposable
	{
		[System.Diagnostics.Conditional("DEBUG")] protected void LOG(string message)				{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected void FAIL(string message)				{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		private const char					ConnectionObjectTag					= 'C';
		private const string				ConnectionObjectNamePrefix			= "ProgressHelper_C_";  // "ProgressHelper_" + ConnectionObjectTag + "_";
		private const char					SessionObjectTag					= 'S';
		private const string				SessionObjectNamePrefix				= "ProgressHelper_S_";  // "ProgressHelper_" + SessionObjectTag + "_";
		private const int					ObjectNameTagPosition				= 15;  // The position of the tag in the custom object name (i.e. "ProgressHelper_".Length)
		private static int					LastUID								= 0;  // An ID to uniquely identify each instances

		private readonly MessageHandler		MessageHandler;
		private ConnectionList				ConnectionList						{ get { return MessageHandler.ConnectionList; } }

		protected string					CustomObjectName;
		public bool							BoundToConnection					{ get { return ConnectionID != null; } }
		public bool							BoundToSession						{ get { return SessionID != null; } }
		private readonly string				ConnectionID;
		private readonly string				SessionID;

		private readonly Action<string>		ConnectionAllocatedCallback			= null;
		private Message						LastMessageSent						= null;
		/// <summary>When this instance is bound to a session, leave this property to 'true' to resend the last message if a new connection arrives in the this session</summary>
		public bool							ResendLastMessageToNewConnections	{ get; set; } = true;

		public bool							Aborted								{ get { return aborted; } }
		private volatile bool				aborted								= false;
		/// <summary>Set the callback that must be invoked when the 'Dispose()' method is called from a different thread than the one that created this instance</summary>
		public Action						AbortCallback						{ get { return abortCallback; } set { abortCallback = value; } }
		private volatile Action				abortCallback						= null;
		private bool						Terminated							= false;

		/// <summary>Create the message to be sent to the peer when a text notification is to be displayed</summary>
		/// <returns>The message to be sent to the peer when a text notification is to be displayed or 'null' if none must be sent</returns>
		protected abstract Message CreateProgressMessage(string message);
		/// <summary>Create the message to be sent to the peer when this instance is disposed</summary>
		/// <returns>The message to be sent to the peer when this instance is disposed or 'null' if none must be sent</returns>
		protected abstract Message CreateTerminationMessage();

		protected ProgressHelper(MessageHandler messageHandler, Message requestMessage, bool sendToSession=false) : this(messageHandler, requestMessage.SenderConnectionID, sendToSession:sendToSession)  {}

		protected ProgressHelper(MessageHandler messageHandler, string connectionID, bool sendToSession=false)
		{
			ASSERT( messageHandler != null, "Missing parameter 'messageHandler'" );
			ASSERT( !string.IsNullOrEmpty(connectionID), "Missing parameter 'connectionID'" );

			string sessionID;
			if( sendToSession )
			{
				sessionID = ConnectionList.GetSessionID( connectionID );
				connectionID = null;
			}
			else
			{
				sessionID = null;
			}

			MessageHandler = messageHandler;
			ConnectionID = connectionID;
			SessionID = sessionID;
			ASSERT( BoundToConnection ^ BoundToSession, "One and only one of the 'connectionID' and 'sessionID' parameters must be defined" );

			var uid = System.Threading.Interlocked.Increment( ref LastUID );
			if( BoundToConnection )
			{
				// Register against the ConnectionEntry
				CustomObjectName = ConnectionObjectNamePrefix + uid;
				var rc = ConnectionList.GetConnectionCustomObject( ConnectionID, CustomObjectName, ()=>this );
				ASSERT( rc.GetHashCode() == this.GetHashCode(), "'ConnectionList.GetConnectionCustomObject()' is supposed to return 'this'" );
			}
			else  // SendToSessionID
			{
				// Register against the SessionEntry
				CustomObjectName = SessionObjectNamePrefix + uid;
				var rc = ConnectionList.GetSessionCustomObject( SessionID, CustomObjectName, ()=>this );
				ASSERT( rc.GetHashCode() == this.GetHashCode(), "'ConnectionList.GetSessionCustomObject()' is supposed to return 'this'" );

				// Watch any new connection arriving to the LongPolling's ConnectionList
				ConnectionAllocatedCallback = new Action<string>( ConnectionList_ConnectionAllocated );
				ConnectionList.ConnectionAllocated += ConnectionAllocatedCallback;
			}
		}

		public void Dispose()
		{
			Dispose( true );
			GC.SuppressFinalize( this );
		}
		protected virtual void Dispose(bool disposing)
		{
			Terminate();
		}

		public void Terminate()
		{
			if( Terminated )
				// Already terminated cleanly => NOOP
				return;
			try
			{
				if( BoundToConnection )
				{
					ASSERT( ConnectionAllocatedCallback == null, "Property 'ConnectionAllocatedCallback' is not supposed to be set here" );

					// Unregister as CustomObject from connection
					ConnectionList.UnregisterConnectionCustomObject( ConnectionID, CustomObjectName );
				}
				else
				{
					try
					{
						// Stop watching for new connections
						ASSERT( ConnectionAllocatedCallback != null, "Property 'ConnectionAllocatedCallback' is supposed to be set here" );
						if( ConnectionAllocatedCallback != null )
							ConnectionList.ConnectionAllocated -= ConnectionAllocatedCallback;
					}
					catch( System.Exception ex )
					{
						FAIL( "'ConnectionList.UnregisterXXXCustomObject()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message );
					}

					// Unregister as CustomObject from session
					ConnectionList.UnregisterSessionCustomObject( SessionID, CustomObjectName );
				}
			}
			catch( System.Exception ex )
			{
				FAIL( "'ConnectionList.UnregisterXXXCustomObject()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message );
			}

			try
			{
				// Send termination message
				var message = CreateTerminationMessage();
				if( message != null )
					Send( message:message );
			}
			catch( System.Exception ex )
			{
				FAIL( "'Send()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message );
			}
			finally
			{
				Terminated = true;
			}
		}

		public void Abort()
		{
			// Notify the abortion to original thread
			aborted = true;
			var callback = AbortCallback;  // NB: Take the reference of the volatile member and work on this one!
			if( callback != null )
			{
				// Invoke the abortion callback if any
				try { callback(); }
				catch( System.Exception ex )  { FAIL( "Abortion callback threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
			}
		}

		/// <returns>The registered ProgressHelper instance or null if none found</returns>
		public static T Get<T>(MessageHandler messageHandler, string connectionID , string customObjectName) where T:ProgressHelper
		{
			CommonLibs.Utils.Debug.ASSERT( messageHandler != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'messageHandler'" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(connectionID), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'connectionID'" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(customObjectName), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'customObjectName'" );

			object instance;
			if( customObjectName[ObjectNameTagPosition] == ConnectionObjectTag )
			{
				// Looking for a ProgressHelper bound to a connection
				CommonLibs.Utils.Debug.ASSERT( customObjectName.StartsWith(ConnectionObjectNamePrefix), System.Reflection.MethodInfo.GetCurrentMethod(), "Invalid parameter 'customObjectName'" );
				instance = messageHandler.ConnectionList.GetConnectionCustomObject( connectionID, customObjectName );
			}
			else  // Looking for a ProgressHelper bound to a session
			{
				CommonLibs.Utils.Debug.ASSERT( customObjectName.StartsWith(SessionObjectNamePrefix), System.Reflection.MethodInfo.GetCurrentMethod(), "Invalid parameter 'customObjectName'" );
				instance = messageHandler.ConnectionList.GetSessionCustomObject( connectionID, customObjectName );
			}

			return (T)instance;
		}

		/// <remarks>This callback is only bound when 'SendToSessionID == true'</remarks>
		private void ConnectionList_ConnectionAllocated(string connectionID)
		{
			ASSERT( BoundToSession, "The method 'ConnectionList_ConnectionAllocated' is not supposed to be called when 'BoundToSession' is false" );
			ASSERT( SessionID != null, "Property 'SessionID' is supposed to be set here" );

			if( Terminated )
				// This instance has been discarded
				return;

			if(! ResendLastMessageToNewConnections )
				// No resend is requested
				return;

			var lastMessageSent = LastMessageSent ;
			if( lastMessageSent == null )
				// No message has been sent yet
				return;

			var sessionID = ConnectionList.GetSessionID( connectionID );
			if( SessionID != sessionID )
				// This connection is in another session
				return;

			// NB: There is a (very thin) chance that this instance is terminated now but the last message will be resent anyway
			// (maybe displaying a persistent notification message that is never removed)
			// But the 'SendMessageToConnection()' cannot be inside any lock{}s to avoid dead-locks...
			// I take the risk...

			// A new connection has arrived and its session is connected to this ProgressHelper instance => Resend the last message to this connection
			LOG( "ConnectionList_ConnectionAllocated(" + connectionID + ") - MessageHandler.SendMessageToConnection()" );
			var newMessage = Message.CreateMessage( lastMessageSent.HandlerType, lastMessageSent );
			MessageHandler.SendMessageToConnection( connectionID, newMessage );
			LOG( "ConnectionList_ConnectionAllocated(" + connectionID + ") - exit" );
		}

		public void SendNotification(string textMessage, Dictionary<string,object> additionalInfo=null)
		{
			try
			{
				var message = CreateProgressMessage( textMessage );
				if( message == null )
					return;

				if( additionalInfo != null )
					foreach( var pair in additionalInfo )
						message[pair.Key] = pair.Value;

				Send( message );
			}
			catch( System.Exception ex )
			{
				FAIL( "An unexpected error occured when sending progress notification message (" + ex.GetType().FullName + "): " + ex.Message );
			}
		}

		protected void Send(Message message)
		{
			ASSERT( message != null, "Missing parameter 'message'" );

			if( Terminated )
				// This instance is not used anymore, but the helper is still receiving messages => Discard them
				return;

			LastMessageSent = message;

			if( BoundToConnection )
			{
				ASSERT( !string.IsNullOrEmpty(ConnectionID), "Property 'ConnectionID' is supposed to be set here" );
				try { MessageHandler.SendMessageToConnection( ConnectionID, message ); }
				catch( System.Exception ex ) {  FAIL( "MessageHandler.SendMessageToConnection( " + ConnectionID + ", message ) threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
			}
			else
			{
				ASSERT( !string.IsNullOrEmpty(SessionID), "Property 'SessionID' is supposed to be set here" );
				try { MessageHandler.SendMessageToSession( SessionID, message ); }
				catch( System.Exception ex ) { FAIL( "MessageHandler.SendMessageToSession( " + SessionID + ", message ) threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
			}
		}
	}
}
