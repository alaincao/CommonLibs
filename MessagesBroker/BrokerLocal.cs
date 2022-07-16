//
// CommonLibs/MessagesBroker/BrokerLocal.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2010 - 2020 Alain CAO
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
using System.Threading.Tasks;

using CommonLibs.Utils;

namespace CommonLibs.MessagesBroker
{
	using TMessage = IDictionary<string,object>;
	using CMessage = Dictionary<string,object>;

	public interface IBroker
	{
		/// <summary>Factory function for Messages instances</summary>
		TMessage NewMessage(IDictionary<string,object> src=null);
		T FillAsResponse<T>(TMessage source, T destination, string senderID=null) where T:TMessage;
		T FillException<T>(T message, System.Exception exception) where T:TMessage;
		Task RegisterEndpoint(IEndPoint endPoint);
		Task UnRegisterEndpoint(string id);
		Task ReceiveMessages(IEnumerable<TMessage> messages);
	}

	public class BrokerLocal : IBroker, IDisposable
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		private class MessagesQueueItem
		{
			internal static DateTime	Now				{ get { return DateTime.Now; } }
			internal DateTime			LastReceived;
			internal List<TMessage>		Messages;
		}

		public const int		DefaultMessagesExpireSeconds	= BrokerBase.DefaultMessagesExpireSeconds;
		public const int		DefaultCleanupIntervalSeconds	= 60;

		private object									Locker						= new object();
		private Dictionary<string,IEndPoint>			EndPoints					= new Dictionary<string,IEndPoint>();
		private Dictionary<string,MessagesQueueItem>	MessagesQueues				= new Dictionary<string,MessagesQueueItem>();
		public int										MessagesExpireSeconds		{ get; set; } = DefaultMessagesExpireSeconds;
		private System.Timers.Timer						CleanupTimer;
		public double									CleanupIntervalMilisecs		{ get { return CleanupTimer.Interval; } }

		TMessage	IBroker.	NewMessage(IDictionary<string,object> src)									=> NewMessage( src );
		public virtual TMessage	NewMessage(IDictionary<string,object> src=null)								=> (src == null) ? new CMessage() : new CMessage( src );
		T			IBroker.	FillAsResponse<T>(TMessage source, T destination, string senderID)			{ this.FillAsResponse( source, destination, senderID ); return destination; }
		public virtual void		FillAsResponse(TMessage source, TMessage destination, string senderID=null)	=> FillAsResponseStatic( source, destination, senderID );
		T			IBroker.	FillException<T>(T message, Exception exception)							{ this.FillException( message, exception ); return message; }
		public virtual void		FillException(TMessage message, Exception exception)						=> FillExceptionStatic( message, exception );

		public BrokerLocal(double? cleanupIntervalSeconds=null)
		{
			var milisecs = (cleanupIntervalSeconds ?? DefaultCleanupIntervalSeconds) * 1000;

			CleanupTimer = new System.Timers.Timer( milisecs );
			CleanupTimer.Elapsed += (s,e)=>{ Cleanup(); };
			CleanupTimer.Start();
		}

		public void Dispose()
		{
			CleanupTimer.Stop();
		}

		private void Cleanup()
		{
			lock( Locker )
			{
				var limit = MessagesQueueItem.Now.AddSeconds( -MessagesExpireSeconds );
				var toDelete = new List<string>();
				foreach( var pair in MessagesQueues )
					if( pair.Value.LastReceived < limit )
						toDelete.Add( pair.Key );
				foreach( var id in toDelete )
					MessagesQueues.Remove( id );
			}
		}

		public static void FillAsResponseStatic(TMessage source, TMessage destination, string senderID=null)
		{
			CommonLibs.Utils.Debug.ASSERT( source != null, System.Reflection.MethodInfo.GetCurrentMethod(), $"Missing parameter '{nameof(source)}'" );
			CommonLibs.Utils.Debug.ASSERT( destination != null, System.Reflection.MethodInfo.GetCurrentMethod(), $"Missing parameter '{nameof(destination)}'" );

			destination.AddRange( source );  // Copy all fields from source

			// Replace the message handler by the reply-to handler (if any)
			destination[ MessageKeys.KeyMessageHandler ] = source.TryGet(MessageKeys.KeyMessageResponseHandler) ?? destination[ MessageKeys.KeyMessageHandler ];
			destination.Remove( MessageKeys.KeyMessageResponseHandler );

			// Replace the ReceiverID by the SenderID (if any)
			destination[ MessageKeys.KeyReceiverID ] = source.TryGet(MessageKeys.KeySenderID) ?? destination[ MessageKeys.KeyReceiverID ];
			destination[ MessageKeys.KeySenderID ] = senderID;  // Replace/clear the original SenderID from the response
		}

		public static void FillExceptionStatic(TMessage message, Exception exception)
		{
			CommonLibs.Utils.Debug.ASSERT( exception != null, System.Reflection.MethodInfo.GetCurrentMethod(), $"Missing parameter '{nameof(exception)}'" );
			CommonLibs.Utils.Debug.ASSERT( ! message.ContainsKey(MessageKeys.KeyMessageException), System.Reflection.MethodInfo.GetCurrentMethod(), $"The message key '{MessageKeys.KeyMessageException}' will be overwritten on the response message" );

			string stackTrace = exception.StackTrace;
			{
				for( var ex = exception.InnerException; ex != null ; ex = ex.InnerException )
				{
					stackTrace += "\n"+ex.Message+"\n";
					stackTrace += ex.StackTrace;
				}
			}

			message[MessageKeys.KeyMessageException] = new Dictionary<string,object> {
					{ MessageKeys.KeyMessageExceptionMessage,		exception.Message },
					{ MessageKeys.KeyMessageExceptionClass,			exception.GetType().FullName },
					{ MessageKeys.KeyMessageExceptionStackTrace,	stackTrace }
				};
		}

		public Task RegisterEndpoint(IEndPoint endPoint)
		{
			ASSERT( (endPoint != null) && (! string.IsNullOrWhiteSpace(endPoint.ID)), $"Missing or invalid parameter '{nameof(endPoint)}'" );

			var id = endPoint.ID;
			MessagesQueueItem queue;
			lock( Locker )
			{
				// Check if an existing one is already registered
				{
					var existing = EndPoints.TryGet( id );
					if( existing != null )
						throw new BrokerBase.EndpointAlreadyRegistered( $"The endpoint '{id}' has already been registered", existing );
				}

				// Are there any messages in the queue ?
				queue = MessagesQueues.TryGet( id );
				if( queue != null )
				{
					MessagesQueues.Remove( id );

					if( endPoint.IsOneShot && (queue.Messages.Count > 0) )
						// 'ReceiveMessages()' would be invoked immediately => This endpoint does not need to be registered (i.e. would have to unregister it immediately after ...)
						goto BREAK;
				}

				// Register endpoint
				EndPoints.Add( id, endPoint );
			BREAK:;
			}

			if( queue != null )
			{
				// There were messages in the queue => Send them
				var notAwaited = Task.Run( async ()=>  // nb: Run asynchroneously
					{
						try { await endPoint.ReceiveMessages( queue.Messages ); }
						catch( System.Exception ex ) { FAIL( $"endPoint.ReceiveMessages() threw an exception ({ex.GetType().FullName}): {ex.Message}" ); }
					} );
			}

			return Task.FromResult( 0 );
		}

		public Task UnRegisterEndpoint(string id)
		{
			ASSERT( ! string.IsNullOrWhiteSpace(id), $"Missing parameter '{nameof(id)}'" );
			lock( Locker )
			{
				EndPoints.Remove( id );
			}
			return Task.FromResult( 0 );
		}

		public Task ReceiveMessages(IEnumerable<TMessage> messages)
		{
			ASSERT( messages != null, $"Missing paramter '{nameof(messages)}'" );

			var byEndpointIDs = messages.GroupBy( v=>v.TryGetString(MessageKeys.KeyReceiverID) ).Select( v=>new{ EndPointID=v.Key, Messages=v.ToList() } ).ToArray();
			foreach( var pair in byEndpointIDs )
			{
				ASSERT( !string.IsNullOrWhiteSpace(pair.EndPointID), $"The message is missing its '{MessageKeys.KeyReceiverID}'" );

				IEndPoint endPoint;
				lock( Locker )
				{
					endPoint = EndPoints.TryGet( pair.EndPointID );
					if( endPoint != null )
					{
						// Endpoint available
						if( endPoint.IsOneShot )
							// Must be unregistered immediately
							EndPoints.Remove( pair.EndPointID );
					}
					else
					{
						// No endpoint available => Add messages to queue
						var queue = MessagesQueues.TryGet( pair.EndPointID );
						if( queue == null )
						{
							MessagesQueues[ pair.EndPointID ] = new MessagesQueueItem{ LastReceived=MessagesQueueItem.Now, Messages=pair.Messages };
						}
						else
						{
							queue.LastReceived = MessagesQueueItem.Now;
							queue.Messages.AddRange( pair.Messages );
						}
					}
				}

				if( endPoint != null )
				{
					// Endpoint available => Send them
					var notAwaited = Task.Run( async ()=>  // nb: Run asynchroneously
						{
							try { await endPoint.ReceiveMessages( pair.Messages ); }
							catch( System.Exception ex ) { FAIL( $"endPoint.ReceiveMessages() threw an exception ({ex.GetType().FullName}): {ex.Message}" ); }
						} );
				}
			}

			return Task.FromResult( 0 );
		}
	}
}
