//
// CommonLibs/MessagesBroker/BrokerLocal.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2010 - 2020 Alain CAO
//
// Permission to use, copy, modify, and/or distribute this software for any
// purpose with or without fee is hereby granted.
// 
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
// REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY
// AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
// INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
// LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR
// OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
// PERFORMANCE OF THIS SOFTWARE.
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

		private sealed class MessagesQueueItem
		{
			internal static DateTime	Now				{ get { return DateTime.Now; } }
			internal DateTime			LastReceived;
			internal List<TMessage>		Messages;
		}

		public const int		DefaultMessagesExpireSeconds	= BrokerBase.DefaultMessagesExpireSeconds;
		public const int		DefaultCleanupIntervalSeconds	= 60;

		private readonly object									Locker						= new object();
		private readonly Dictionary<string,IEndPoint>			EndPoints					= new Dictionary<string,IEndPoint>();
		private readonly Dictionary<string,MessagesQueueItem>	MessagesQueues				= new Dictionary<string,MessagesQueueItem>();
		public int												MessagesExpireSeconds		{ get; set; } = DefaultMessagesExpireSeconds;
		private readonly System.Timers.Timer					CleanupTimer;
		public double											CleanupIntervalMilisecs		{ get { return CleanupTimer.Interval; } }

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
			Dispose( true );
			GC.SuppressFinalize( this );
		}
		protected virtual void Dispose(bool disposing)
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

			var stackTrace = new System.Text.StringBuilder();
			for( var ex = exception.InnerException; ex != null ; ex = ex.InnerException )
				stackTrace.Append( $"\n{ex.Message}\n{ex.StackTrace}" );

			message[MessageKeys.KeyMessageException] = new Dictionary<string,object> {
					{ MessageKeys.KeyMessageExceptionMessage,		exception.Message },
					{ MessageKeys.KeyMessageExceptionClass,			exception.GetType().FullName },
					{ MessageKeys.KeyMessageExceptionStackTrace,	stackTrace.ToString() },
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
				var existing = EndPoints.TryGet( id );
				if( existing != null )
					throw new BrokerBase.EndpointAlreadyRegisteredException( $"The endpoint '{id}' has already been registered", existing );

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
			}
		BREAK:

			if( queue != null )
			{
				// There were messages in the queue => Send them
				Task.Run( async ()=>
					{
						try { await endPoint.ReceiveMessages( queue.Messages ); }
						catch( System.Exception ex ) { FAIL( $"endPoint.ReceiveMessages() threw an exception ({ex.GetType().FullName}): {ex.Message}" ); }
					} )
					.FireAndForget();
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
					Task.Run( async ()=>
						{
							try { await endPoint.ReceiveMessages( pair.Messages ); }
							catch( System.Exception ex ) { FAIL( $"endPoint.ReceiveMessages() threw an exception ({ex.GetType().FullName}): {ex.Message}" ); }
						} )
						.FireAndForget();
				}
			}

			return Task.FromResult( 0 );
		}
	}
}
