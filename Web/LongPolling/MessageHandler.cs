//
// CommonLibs/Web/LongPolling/MessageHandler.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Globalization;
using System.Web;

using CommonLibs.Utils.Tasks;

namespace CommonLibs.Web.LongPolling
{
	// Warning: MessageHandler and ConnectionList are heavily dependents.
	// To any avoid dead-locks, all these interactions must be performed outside their respective locks() (or Enter{Read|Write}Lock())

	public class MessageHandler
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		private class CallbackItem
		{
			internal bool							IsThreaded						{ get { return (CallbackThreaded != null) ? true : false; } }
			// If CallbackDirect is null, CallbackThreaded must be set ; And if CallbackThreaded is null, CallbackDirect must be set
			internal Action<Message>				CallbackDirect					= null;
			internal Action<TaskEntry,Message>		CallbackThreaded				= null;
		}

		/// <summary>
		/// Class used to save the context of the HTTP context's thread to the message handler's thread
		/// </summary>
		private class MessageContext
		{
			internal MessageHandler					MessageHandler;
			internal CallbackItem					CallbackItem;
			internal Message						Message;
			internal object							ContextObject;

			internal MessageContext(MessageHandler messageHandler, CallbackItem callbackItem, Message message)
			{
				MessageHandler = messageHandler;
				CallbackItem = callbackItem;
				Message = message;

				if( MessageHandler.SaveMessageContextObject != null )
					try { ContextObject = MessageHandler.SaveMessageContextObject(); }
					catch( System.Exception ex )  { MessageHandler.FAIL( "'MessageHandler.SaveMessageContextObject()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
				else
					ContextObject = null;
			}

			internal void RestoreContext()
			{
				if( MessageHandler.RestoreMessageContextObject != null )
					// Restore custom ContextObject
					try { MessageHandler.RestoreMessageContextObject( ContextObject ); }
					catch( System.Exception ex )  { MessageHandler.FAIL( "'MessageHandler.RestoreMessageContextObject()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
			}
		}

		private TasksQueue							TaskQueue;
// TODO: Alain: Once stable, check that all uses of 'ConnectionList' are outside lock()s (c.f. warning above)
		public ConnectionList						ConnectionList					{ get; private set; }
		private Dictionary<string,CallbackItem>		HandlerCallbacks				= new Dictionary<string,CallbackItem>();
		#if DEBUG
			private bool							MessageReceived					= false;
		#endif

		/// <summary>
		/// The list of messages currently waiting to be delivered<br/>
		/// Parameter 1: The ConnectionID
		/// </summary>
		private Dictionary<string,List<Message>>	PendingMessages					= new Dictionary<string,List<Message>>();

		/// <summary>
		/// The list of connections to which messages are not to be sent right now<br/>
		/// Parameter 1: The ConnectionID
		/// </summary>
		private HashSet<string>						ConnectionsHeld					= new HashSet<string>();

		/// <summary>
		/// Set this callback to determine the action to take when a fatal exception is detected whithin the MessageHandler
		/// </summary>
		public Action<string,Exception>				FatalExceptionHandler;

		/// <summary>
		/// Set this callback if any object must be kept from the HTTP handler's thread to the message handler's thread (e.g. session object)
		/// </summary>
		public Func<object>							SaveMessageContextObject		= null;
		/// <summary>
		/// Set this callback to restore the object saved by the "GetHttpContextObject" callback
		/// </summary>
		public Action<object>						RestoreMessageContextObject		= null;
		/// <summary>
		/// Set this callback to undo the action performed by RestoreMessageContextObject when the message handler is terminated (e.g. to clear any [ThreadStatic] objects assigned to this thread)
		/// </summary>
		public Action								ClearMessageContextObject		= null;

		public MessageHandler(TasksQueue tasksQueue, ConnectionList connectionList)
		{
			LOG( "Constructor" );

			TaskQueue = tasksQueue;
			ConnectionList = connectionList;
			FatalExceptionHandler = DefaultFatalExceptionHandler;

			ConnectionList.ConnectionRegistered += (value)=>
				{
					CheckPendingQueueForConnection( value );
				};
			ConnectionList.ConnectionLost += ConnectionList_ConnectionLost;
		}

		public string[] GetRegisteredMessageHandlers()
		{
			return HandlerCallbacks.Select( v=>v.Key ).ToArray();
		}

		#if DEBUG
		/// <remarks>This method is not thread-safe and may crash => Use only for debugging purposes</remarks>
		public void AddOrReplaceMessageHandler(string messageType, Action<Message> callback)
		{
			HandlerCallbacks[ messageType ] = new CallbackItem{ CallbackDirect = callback };
		}

		/// <remarks>This method is not thread-safe and may crash => Use only for debugging purposes</remarks>
		public void AddOrReplaceMessageHandler(string messageType, Action<TaskEntry,Message> callback)
		{
			HandlerCallbacks[ messageType ] = new CallbackItem{ CallbackThreaded = callback };
		}
		#endif

		public void AddMessageHandler(string messageType, Action<Message> callback)
		{
			ASSERT( !string.IsNullOrEmpty(messageType), "Missing parameter 'messageType'" );
			ASSERT( callback != null, "Missing parameter 'callback'" );

			#if DEBUG
				// NB: Access to HandlerCallbacks is not thread-safe protected. All calls to this method must be performed once at application start and not after.
				ASSERT( !MessageReceived, "At least one message has already been received. AddMessageHandler() cannot be called anymore." );
			#endif

			ASSERT( !HandlerCallbacks.ContainsKey(messageType), "The message type '" + messageType + "' is already defined" );
			HandlerCallbacks[ messageType ] = new CallbackItem{ CallbackDirect = callback };
		}

		public void AddMessageHandler(string messageType, Action<TaskEntry,Message> callback)
		{
			ASSERT( !string.IsNullOrEmpty(messageType), "Missing parameter 'messageType'" );
			ASSERT( callback != null, "Missing parameter 'callback'" );

			#if DEBUG
				// NB: Access to HandlerCallbacks is not thread-safe protected. All calls to this method must be performed once at application start and not after.
				ASSERT( !MessageReceived, "At least one message has already been received. AddMessageHandler() cannot be called anymore." );
			#endif

			ASSERT( !HandlerCallbacks.ContainsKey(messageType), "The message type '" + messageType + "' is already defined" );
			HandlerCallbacks[ messageType ] = new CallbackItem{ CallbackThreaded = callback };
		}

		private void DefaultFatalExceptionHandler(string description, Exception exception)
		{
			ASSERT( exception != null, "Missing parameter 'exception'" );
			string message = "" + description + ": " + exception;
			FAIL( message );
		}

		private void ConnectionList_ConnectionLost(string connectionID)
		{
			ASSERT( !string.IsNullOrEmpty(connectionID), "Missing parameter 'connectionID'" );

			LOG( "ConnectionList_ConnectionLost(" + connectionID + ") - Start" );
			lock( PendingMessages )
			{
				LOG( "ConnectionList_ConnectionLost(" + connectionID + ") - Lock acquired" );

				PendingMessages.Remove( connectionID );
			}
			LOG( "ConnectionList_ConnectionLost(" + connectionID + ") - Exit" );
		}

		internal void ReceiveMessage(Message message)
		{
			ASSERT( message != null, "Missing parameter 'message'" );
			LOG( "ReceiveMessage(" + message + ") - Start" );
			#if DEBUG
				MessageReceived = true;
			#endif

			CallbackItem callbackItem;
			if(! HandlerCallbacks.TryGetValue(message.HandlerType, out callbackItem) )
				throw new NotImplementedException( "There is no message handler defined for messages of type '" + message.HandlerType + "'" );

			var messageContext = new MessageContext( this, callbackItem, message );
			if(! callbackItem.IsThreaded )
			{
				// Process message right now
				HandleMessageThread( messageContext, /*taskEntry=*/null );
			}
			else
			{
				// Handle the message inside its own thread
				TaskQueue.CreateTask( (taskEntry)=>{ HandleMessageThread(messageContext, taskEntry); } );
			}
			LOG( "ReceiveMessage(" + message + ") - Exit" );
		}

		/// <summary>
		/// Method used by the message handlers to check if the just successfully processed message has inner messages to process
		/// </summary>
		/// <param name="originalMessage">The message that the handler successfully processed</param>
		public void CheckChainedMessages(Message originalMessage, Dictionary<string,object> additionalInfo=null)
		{
			ASSERT( originalMessage != null, "Missing parameter 'originalMessage'" );

			object obj;
			if(! originalMessage.TryGetValue(Message.KeyMessageChainedMessages, out obj) )
				// Nothing chained
				return;

			var list = (IEnumerable)obj;
			foreach( var item in list )
			{
				var chainedMessage = Message.CreateReceivedMessage( originalMessage.SenderConnectionID, (IDictionary<string,object>)item );

				if( additionalInfo != null )
				{
					foreach( var pair in additionalInfo )
						chainedMessage[ pair.Key ] = pair.Value;
				}

				ReceiveMessage( chainedMessage );
			}
		}

		/// <returns>The number of connection this message was sent to</returns>
		public int SendMessageToSession(string receiverSessionID, Message message)
		{
			ASSERT( !string.IsNullOrEmpty(receiverSessionID), "Missing parameter 'sessionID'" );
			ASSERT( message != null, "Missing parameter 'message'" );
			LOG( "SendMessageToSession(" + receiverSessionID + "," + message + ") - Start" );

			var connectionIDs = ConnectionList.GetSessionConnectionIDs( receiverSessionID );
			foreach( var connectionID in connectionIDs )
				SendMessageToConnection( connectionID, message );
			return connectionIDs.Length;
		}

		public void SendMessageToConnection(string receiverConnectionID, Message message)
		{
			ASSERT(! string.IsNullOrEmpty(receiverConnectionID), "Missing parameter 'receiverConnectionID'" );
			ASSERT( message != null, "Missing parameter 'message'" );
			LOG( "SendMessageToConnection(" + receiverConnectionID + "," + message + ") - Start" );

			lock( PendingMessages )
			{
				LOG( "SendMessageToConnection(" + receiverConnectionID + "," + message + ") - Lock aquired" );

				List<Message> messagesList;
				if( PendingMessages.TryGetValue(receiverConnectionID, out messagesList) )
				{
					messagesList.Add( message );
				}
				else
				{
					messagesList = new List<Message>();
					messagesList.Add( message );
					PendingMessages[ receiverConnectionID ] = messagesList;
				}
			}
			LOG( "SendMessageToConnection(" + receiverConnectionID + "," + message + ") - Lock released" );

			CheckPendingQueueForConnection( receiverConnectionID );
			LOG( "SendMessageToConnection(" + receiverConnectionID + "," + message + ") - Exit" );
		}

		/// <summary>
		/// Register the specified connection to not to receive messages right now
		/// </summary>
		public void HoldConnectionMessages(string receiverConnectionID)
		{
			lock( ConnectionsHeld )
			{
				ConnectionsHeld.Add( receiverConnectionID );
			}
		}

		/// <summary>
		/// Unregister connections that have been registered by HoldConnectionMessages()
		/// </summary>
		public void UnholdConnectionMessages(string receiverConnectionID)
		{
			lock( ConnectionsHeld )
			{
				ConnectionsHeld.Remove( receiverConnectionID );
			}

			CheckPendingQueueForConnection( receiverConnectionID );
		}

		private void CheckPendingQueueForConnection(string receiverConnectionID)
		{
			LOG( "CheckPendingQueueForConnection(" + receiverConnectionID + ") - Start" );

			lock( ConnectionsHeld )
			{
				if( ConnectionsHeld.Contains(receiverConnectionID) )
					// Messages to this connection must currently be held => NOOP
					return;
			}

			List<Message> messagesList;
			lock( PendingMessages )
			{
				LOG( "CheckPendingQueueForConnection(" + receiverConnectionID + ") - Lock acquired" );

				// Add the messages to the queue
				if(! PendingMessages.TryGetValue(receiverConnectionID, out messagesList) )
				{
					LOG( "CheckPendingQueueForConnection(" + receiverConnectionID + ") - Exit - No pending message available" );
					return;
				}
				// Remove those messages from the list before exiting the lock so a concurrent call will not try to send them twice
				PendingMessages.Remove( receiverConnectionID );
			}
			LOG( "CheckPendingQueueForConnection(" + receiverConnectionID + ") - Lock released" );

			bool messagesSent;
			try
			{
				messagesSent = ConnectionList.SendMessagesToConnectionIfAvailable( receiverConnectionID, messagesList );
			}
			catch( System.Exception ex )
			{
				// Something went wrong while sending the messages to the peer. Discard the messages so they are not tryed to be resent again (and maybe enter an infinite loop...)
				FatalExceptionHandler( "Could not deliver messages - Error while sending data to peer", ex );
				messagesSent = true;  // Discard the messages...
			}

			if(! messagesSent )
			{
				LOG( "CheckPendingQueueForConnection(" + receiverConnectionID + ") - Messages were not delivered => Requeue them" );
				bool recheckAfter;
				lock( PendingMessages )
				{
					LOG( "CheckPendingQueueForConnection(" + receiverConnectionID + ") - Lock acquired" );

					List<Message> newMessagesList;
					if( PendingMessages.TryGetValue(receiverConnectionID, out newMessagesList) )
					{
						// New messages were queued for this connection while trying to send the ones we were already managing => Add the old ones to the new list
						ASSERT( newMessagesList != null && newMessagesList.Count > 0, "New messages were queued but there is nothing in the list..." );
						newMessagesList.AddRange( messagesList );
						recheckAfter = false;  // If messages were added for this connection in the mean-time by another thread, then let this other thread manage the sending...
					}
					else
					{
						// Requeue the messages we were managing
						PendingMessages[ receiverConnectionID ] = messagesList;
						recheckAfter = true;  // The connection might have been made available in the mean time => Check again the availability of the connection
					}
				}
				LOG( "CheckPendingQueueForConnection(" + receiverConnectionID + ") - Lock released" );

				// Check if the connection has been made available in the mean time (which could have called "ConnectionList_ConnectionRegistered" before the messages were requeued in the "PendingMessages")
				if( recheckAfter && ConnectionList.CheckConnectionIsAvailable(receiverConnectionID) )
					// Re-invoke me (should never be too much recursive...)
					CheckPendingQueueForConnection( receiverConnectionID );
			}
			LOG( "CheckPendingQueueForConnection(" + receiverConnectionID + ") - Exit" );
		}

		private void HandleMessageThread(MessageContext messageContext, TaskEntry taskEntry)
		{
			ASSERT( messageContext != null, "Missing parameter 'messageContext'" );
			// ASSERT( taskEntry != null, "Missing parameter 'taskEntry'" ); => 'null' means "CallbackItem.CallbackThreaded == null"

			var message = messageContext.Message;
			var callbackItem = messageContext.CallbackItem;

			LOG( "HandleMessageThread(" + taskEntry + "," + message + ") - Start" );

			bool contextRestored = false;
			try
			{
				if(! callbackItem.IsThreaded )
				{
					// Inline message handler => This thread is still the HTTP handler's thread => No need to restore thread's context

					ASSERT( callbackItem.CallbackDirect != null, "If not 'IsThreaded' then 'callbackItem.CallbackDirect' is supposed to be set" );
					ASSERT( callbackItem.CallbackThreaded == null, "If not 'IsThreaded' then 'callbackItem.CallbackThreaded' is supposed to be null" );

					callbackItem.CallbackDirect( message );
				}
				else  // Threaded message handler => Must restore some of the HTTP handler's thread context
				{
					ASSERT( callbackItem.CallbackDirect == null, "If 'IsThreaded' then 'callbackItem.CallbackDirect' is supposed to be null" );
					ASSERT( callbackItem.CallbackThreaded != null, "If 'IsThreaded' then 'callbackItem.CallbackThreaded' is supposed to be set" );

					// Restore message's context
					messageContext.RestoreContext();
					contextRestored = true;

					callbackItem.CallbackThreaded( taskEntry, message );
				}
				LOG( "HandleMessageThread(" + taskEntry + "," + message + ") - Exit" );
			}
			catch( System.Reflection.TargetInvocationException ex )
			{
				LOG( "HandleMessageThread(" + taskEntry + "," + message + ") - TargetInvocationException" );
				SendMessageToConnection( message.SenderConnectionID, Message.CreateExceptionMessage(message, ex.InnerException) );
			}
			catch( System.Exception ex )
			{
				LOG( "HandleMessageThread(" + taskEntry + "," + message + ") - Exception" );
				SendMessageToConnection( message.SenderConnectionID, Message.CreateExceptionMessage(message, ex) );
			}
			finally
			{
				if( contextRestored && (ClearMessageContextObject != null) )
					// Clear the restored message context
					try { ClearMessageContextObject(); }
					catch( System.Exception ex )  { FAIL( "'ClearMessageContextObject()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
			}
		}
	}
}
