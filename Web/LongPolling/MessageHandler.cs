using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CommonLibs.Web.LongPolling
{
	// Warning: MessageHandler and ConnectionList are heavily dependents.
	// To any avoid dead-locks, all these interactions must be performed outside their respective locks() (or Enter{Read|Write}Lock())

	public class MessageHandler
	{
		private class PendingMessage
		{
			internal string							SenderConnectionID		= null;
			internal int?							RequestID				= null;
			internal object							MessageContent			= null;
		}
		private Queue<PendingMessage>				PendingMessages			= new Queue<PendingMessage>();

		private Utils.TasksQueue					TaskQueue;
// TODO: Alain: Check that all uses of 'ConnectionList' are outside lock()s (c.f. warning above)
		private ConnectionList						ConnectionList;

		private Utils.TaskEntry						CheckMessageQueueTask	= null;

		public MessageHandler(Utils.TasksQueue tasksQueue, ConnectionList connectionList)
		{
			TaskQueue = tasksQueue;
			ConnectionList = connectionList;
		}

		/// <param name="messages">The list of messages. RequestID => MessageContent</param>
		public void ReceiveMessages(string senderConnectionID, IEnumerable<KeyValuePair<int?,object>> messages)
		{
			lock( this )
			{
				foreach( var message in messages )
					ReceiveMessage( senderConnectionID, message.Key, message.Value, /*callCheckMessageQueue=*/false );
			}
			CheckMessageQueue();
		}

		private void ReceiveMessage(string senderConnectionID, int? requestID, object messageContent)
		{
			ReceiveMessage( senderConnectionID, requestID, messageContent, /*callCheckMessageQueue=*/true );
		}

		private void ReceiveMessage(string senderConnectionID, int? requestID, object messageContent, bool callCheckMessageQueue)
		{
			lock( this )
			{
				System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(senderConnectionID), "Missing parameter 'senderConnectionID'" );
				System.Diagnostics.Debug.Assert( messageContent != null, "Missing parameter 'messageContent'" );

				if( PendingMessages == null )
					PendingMessages = new Queue<PendingMessage>();
				PendingMessages.Enqueue( new PendingMessage{ SenderConnectionID=senderConnectionID, RequestID=requestID, MessageContent=messageContent } );
			}
			if( callCheckMessageQueue )
				CheckMessageQueue();
		}

		public void SendMessageToConnection(string requestID, object message, string receiverConnectionID)
		{
			lock( this )
			{
				System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(receiverConnectionID), "Missing parameter 'connectionID'" );

				throw new NotImplementedException( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "()" );
			}
		}

		public void SendMessageToSession(string senderConnectionID, object message, string receiverSessionID)
		{
			lock( this )
			{
				System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(receiverSessionID), "Missing parameter 'sessionID'" );

				throw new NotImplementedException( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "()" );
			}
		}

		private void CheckMessageQueue()
		{
			lock( this )
			{
				if( CheckMessageQueueTask != null )
					// the task is already scheduled (or running)
					return;

				CheckMessageQueueTask = TaskQueue.CreateTask( CheckMessageQueue_Thread );
				throw new NotImplementedException( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "()" );
			}
		}

		private void CheckMessageQueue_Thread(Utils.TaskEntry taskEntry)
		{
			while( true )
			{
				PendingMessage pendingMessage;
				lock( this )
				{
					if( PendingMessages.Count == 0 )
					{
						// There is no more message
						CheckMessageQueueTask = null;  // Allow another instance of this thread to be created
						return;  // End this thread
					}
					pendingMessage = PendingMessages.Dequeue();
				}

// TODO: Alain: Not implemented
System.Diagnostics.Debug.Fail( "New message '" + pendingMessage.RequestID + "' from '" + pendingMessage.SenderConnectionID + "': " + pendingMessage.MessageContent );
			}
		}
	}
}
