//
// CommonLibs/MessagesBroker/BrokerBase.cs
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
	using EndPointsDict	= Dictionary<string,IEndPoint>;

	/// <summary>
	/// Base class for remote Broker implementation (ie. not Local)
	/// </summary>
	public abstract class BrokerBase : IBroker
	{
		[System.Diagnostics.Conditional("DEBUG")] protected void LOG(string message)				{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected void FAIL(string message)				{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		[Serializable]
		public class EndpointAlreadyRegisteredException : CommonException
		{
			public readonly IEndPoint	EndPoint;
			internal EndpointAlreadyRegisteredException(string message, IEndPoint endPoint) : base(message)
			{
				EndPoint = endPoint;
			}
			protected EndpointAlreadyRegisteredException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
			{
				throw new NotImplementedException( $"Serialization of '{nameof(EndpointAlreadyRegisteredException)}' is not implemented" );
			}
		}

		public const int				DefaultMessagesExpireSeconds	= 30;

		private EndPointsDict			RegisteredEndpoints				{ get; } = new EndPointsDict();
		private object					Locker							{ get; } = new object();
		public int						MessagesExpireSeconds			{ get; set; } = DefaultMessagesExpireSeconds;

		TMessage	IBroker.	NewMessage(IDictionary<string,object> src)									=> NewMessage( src );
		public virtual TMessage	NewMessage(IDictionary<string,object> src=null)								=> src == null ? new CMessage() : new CMessage( src );
		T			IBroker.	FillAsResponse<T>(TMessage source, T destination, string senderID)			{ this.FillAsResponse( source, destination, senderID ); return destination; }
		public virtual void		FillAsResponse(TMessage source, TMessage destination, string senderID=null)	=> BrokerLocal.FillAsResponseStatic( source, destination, senderID );
		T			IBroker.	FillException<T>(T message, Exception exception)							{ this.FillException( message, exception ); return message; }
		public virtual void		FillException(TMessage message, Exception exception)						=> BrokerLocal.FillExceptionStatic( message, exception );

		protected abstract Task SaveMessages(string endPointID, List<TMessage> messages);
		protected abstract Task<List<TMessage>> RestoreMessages(string endPointID);

		protected BrokerBase()  {}

		public async Task RegisterEndpoint(IEndPoint endPoint)
		{
			ASSERT( (endPoint != null) && (!string.IsNullOrWhiteSpace(endPoint.ID)), $"Missing or invalid parameter '{nameof(endPoint)}'" );

			Action checkExisting = ()=>
				{
					var existing = RegisteredEndpoints.TryGet( endPoint.ID );  // nb: read-only => no need for "lock{}"
					if( existing == null )
						// OK
						return;
					// Not OK
					throw new EndpointAlreadyRegisteredException( $"The endpoint '{endPoint.ID}' has already been registered", existing );
				};
			checkExisting();

			if( endPoint.IsOneShot )
			{
				// Check pending messages
				var messages = await RestoreMessages( endPoint.ID );
				if( messages != null )
				{
					ASSERT( messages.Count > 0, $"'{nameof(messages)}' is not supposed to be empty here" );
					await endPoint.ReceiveMessages( messages );
					// nb: one-shot -> No need to register this endpoint ...
				}
				else  // There's no pending messages
				{
					// Register endpoint
					lock( Locker )
					{
						checkExisting();
						RegisteredEndpoints.Add( endPoint.ID, endPoint );
					}

					// Re-check pending messages (in case they've been added in the mean-time ...)
					await CheckPendingMessages( endPoint.ID );
				}
			}
			else  // not endPoint.IsOneShot
			{
				// Register endpoint
				lock( Locker )
				{
					checkExisting();
					RegisteredEndpoints.Add( endPoint.ID, endPoint );
				}

				// Re-check pending messages (in case they've been added in the mean-time ...)
				await CheckPendingMessages( endPoint.ID );
			}
		}

		public Task UnRegisterEndpoint(string id)
		{
			ASSERT( ! string.IsNullOrWhiteSpace(id), $"Missing parameter '{nameof(id)}" );

			lock( Locker )
			{
				var rc = RegisteredEndpoints.Remove( id );
				ASSERT( rc, $"Could not unregister endpoint '{id}'" );  // nb: can happen in case of race condition, but should be very rare
			}

			return Task.FromResult( 0 );
		}

		public async Task ReceiveMessages(IEnumerable<TMessage> messages)
		{
			ASSERT( messages != null, $"Missing parameter '{nameof(messages)}'" );
			ASSERT( messages.Any(v=>string.IsNullOrWhiteSpace(v.TryGetString(MessageKeys.KeyReceiverID))), $"There are messages without '{MessageKeys.KeyReceiverID}' defined" );

			var messagesToSave = (new{ EndPointID=(string)null, Messages=(List<TMessage>)null }).NewAnonymousList();
			var messagesToSend = (new{ EndPoint=(IEndPoint)null, Messages=(List<TMessage>)null }).NewAnonymousList();

			var byEndPointIDs = messages.GroupBy( v=>v.TryGetString(MessageKeys.KeyReceiverID) ).ToDictionary( v=>v.Key, v=>v.ToList() );
			lock( Locker )
			{
				foreach( var pair in byEndPointIDs )
				{
					var endPointID	= pair.Key;
					var msgs		= pair.Value;

					var endPoint = RegisteredEndpoints.TryGet( endPointID );
					if( endPoint == null )
					{
						// These messages are for an endpoint that is not managed by me => Save them to Redis
						messagesToSave.Add(new{ EndPointID=endPointID, Messages=msgs });
					}
					else if( endPoint.IsOneShot )
					{
						// These messages are for an endpoint that is managed by me but needs to be unregistered when it receives messages
						var rc = RegisteredEndpoints.Remove( endPointID );
						ASSERT( rc, $"Could not unregister endpoint '{endPointID}'" );

						messagesToSend.Add(new{ EndPoint=endPoint, Messages=msgs });
					}
					else
					{
						// These messages are for an endpoint that is managed by me
						messagesToSend.Add(new{ EndPoint=endPoint, Messages=msgs });
					}
				}
			}

			var exception = (System.Exception)null;  // The first exception encountered if any

			foreach( var item in messagesToSend )
			{
				Task.Run( async ()=>
					{
						try { await item.EndPoint.ReceiveMessages( item.Messages ); }
						catch( System.Exception ex ) { exception = (exception ?? ex); }
					} )
					.FireAndForget();
			}

			foreach( var item in messagesToSave )
			{
				try { await SaveMessages( item.EndPointID, item.Messages ); }
				catch( System.Exception ex ) { exception = (exception ?? ex); }
			}

			if( exception != null )
				// An exception has been encountered => Rethrow the first one received
				throw new CommonException( $"An error occurred while receiving a message ({exception.GetType().FullName}): {exception.Message}", innerException:exception );
		}

		protected async Task CheckPendingMessages(string endPointID)
		{
			ASSERT( ! string.IsNullOrWhiteSpace(endPointID), $"Missing parameter '{nameof(endPointID)}" );

			// Is this endpoint registered against me ?
			lock( Locker )
			{
				if( ! RegisteredEndpoints.ContainsKey(endPointID) )
					// No -> No need to continue
					return;
			}

			var messages = await RestoreMessages( endPointID );
			if( messages == null )
				// No messages available for this endpoint
				return;

			IEndPoint endPoint;
			lock( Locker )
			{
				endPoint = RegisteredEndpoints.TryGet( endPointID );
				if( endPoint == null )
				{
					// endPoint was unregistered in the mean-time (nb: should not happen often ...)
					goto PUSH_MESSAGES_BACK;
				}
				else if( endPoint.IsOneShot )
				{
					// Unregister this endpoint before sending the messages
					var rc = RegisteredEndpoints.Remove( endPointID );
					ASSERT( rc, $"Could not unregister endpoint '{endPointID}'" );
					goto SEND_MESSAGES;
				}
				else  // not one-shot
				{
					goto SEND_MESSAGES;
				}
			}
			#pragma warning disable 0162
			FAIL( "Unreachable code reached ..." );
			#pragma warning restore 0162

		SEND_MESSAGES:
			ASSERT( endPoint != null, $"Logic error: '{nameof(endPoint)}' is supposed to be set here" );
			await endPoint.ReceiveMessages( messages );
			return;

		PUSH_MESSAGES_BACK:
			// The messages could not be delivered => Put them back in the queue
			await ReceiveMessages( messages );
			return;
		}
	}
}
