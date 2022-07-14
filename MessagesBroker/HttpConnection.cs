//
// CommonLibs/MessagesBroker/MessageHandler.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CommonLibs.Utils;
using CommonLibs.MessagesBroker.Utils;

namespace CommonLibs.MessagesBroker
{
	using TMessage = IDictionary<string,object>;
	using TRootMessage = IDictionary<string,object>;
	using CRootMessage = Dictionary<string,object>;

	public class HttpConnection<THttpContext> : IEndPoint
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		public string		ID							{ get; private set; } = null;
		public IBroker		Broker						{ get; private set; } = null;
		bool IEndPoint.		IsOneShot					=> true;
		public string		DefaultReceiverID			{ get; set; } = null;
		public int			StaleTimeoutMilisec			{ get; set; } = DefaultStaleTimeoutMilisec;
		public const int	DefaultStaleTimeoutMilisec	= 15000;

		private volatile TaskCompletionSource<TRootMessage>	CompletionSource	= null;

		/// <summary>Override to implement security ; Invoked when an 'init' message is received</summary>
		/// <remarks>Can throw an exception for invalid requests</remarks>
		/// <returns>A unique identifier for this connection</returns>
		protected virtual Task<string> ValidateInit(THttpContext context, TRootMessage requestMessage)  { return Task.FromResult( Guid.NewGuid().ToString() ); }
		/// <summary>Override to implement security ; Invoked when messages are received from client</summary>
		/// <param name="messages">nb: Can be empty for 'poll' root messages</param>
		/// <remarks>Can throw an exception for invalid requests</remarks>
		protected virtual Task ValidateInboundMessages(THttpContext context, List<TMessage> messages)  { return /*NOOP*/Task.FromResult(0); }
		/// <summary>Override to implement security ; Invoked when messages are received from the broker</summary>
		/// <remarks>Can modify the provided list, but cannot throw an exception</remarks>
		protected virtual Task ValidateOutboundMessages(List<TMessage> messages)  { return /*NOOP*/Task.FromResult(0); }

		public async Task<TRootMessage> ProcessRequest(IBroker broker, THttpContext context, TRootMessage request)
		{
			ASSERT( Broker == null, $"Property '{nameof(Broker)}' is NOT supposed to be set here" );  // Has this method already been invoked ??
			ASSERT( broker != null, $"Missing parameter '{nameof(broker)}'" );
			ASSERT( context != null, $"Missing parameter '{nameof(context)}'" );
			ASSERT( request != null, $"Missing parameter '{nameof(request)}'" );
			Broker = broker;

			// Check message type
			var messageType = (string)request.TryGet( RootMessageKeys.KeyType );
			LOG( $"{nameof(ProcessRequest)} - Received message of type '" + messageType + "'" );
			switch( messageType )
			{
				case RootMessageKeys.TypeInit: {
					// Create an ID for this connection and send it to client
					ID = await ValidateInit( context, request );
					if( string.IsNullOrWhiteSpace(ID) )
						throw new CommonException( "Init message missing ID parameter" );
					LOG( $"ReceiveRequest() - Respond with 'init' message: {ID}" );

					var response = new CRootMessage {	{ RootMessageKeys.KeyType,		RootMessageKeys.TypeInit },
														{ RootMessageKeys.KeySenderID,	ID } };
					return response; }

				case RootMessageKeys.TypePoll: {
					// Validate ID for this connection
					ID = request.TryGetString( RootMessageKeys.KeySenderID );
					if( string.IsNullOrWhiteSpace(ID) )
						throw new CommonException( $"Missing '{RootMessageKeys.KeySenderID}' parameter from message" );
					await ValidateInboundMessages( context, new List<TMessage>() );

					// This object will receive the 'RootMessage' to send to the client as response
					var completionSource = new TaskCompletionSource<TRootMessage>();
					CompletionSource = completionSource;

					// Create a timeout to close this connection and avoid keeping it open too long (stale connection)
					var staleCancellation = new System.Threading.CancellationTokenSource();
					Task.Run( async ()=>
						{
							await Task.Delay( StaleTimeoutMilisec, staleCancellation.Token );
							await ResetNow();
						} )
						.FireAndForget();

					// Register against the broker
					try
					{
						await Broker.RegisterEndpoint( this );
					}
					catch( BrokerBase.EndpointAlreadyRegisteredException ex )
					{
						// This connection has already been registered?
						// CAN happen when the browser resets the polling connection (e.g. when typing 'ctrl+s' to save the page, all active requests are interrupted)
						// and the client restart the connection immediately.
						// But this should not happen often ; If it does, something's wrong
						FAIL( $"Connection '{this.ID}' has already been registered" );

						// Unregister previous one
						await ( (HttpConnection<THttpContext>)ex.EndPoint ).ResetNow();

						// Retry register (ie. should succeed ...)
						await Broker.RegisterEndpoint( this );
					}

					// Wait for any messages to send through this connection
					var response = await completionSource.Task;

					// Cancel the stale timeout if still running
					staleCancellation.Cancel();

					return response; }

				case RootMessageKeys.TypeMessages: {
					// Validate ID for this connection
					ID = request.TryGetString( RootMessageKeys.KeySenderID );
					if( string.IsNullOrWhiteSpace(ID) )
						throw new CommonException( $"Missing '{RootMessageKeys.KeySenderID}' parameter from message" );

					// Validate messages list
					var messages = ((IEnumerable)request[ RootMessageKeys.KeyMessageMessages ])
													.Cast<IDictionary<string,object>>()
													.Select( v=>
														{
															var message = Broker.NewMessage();
															message.AddRange( v );
															if( string.IsNullOrWhiteSpace(message.TryGetString(MessageKeys.KeySenderID)) )
																message[ MessageKeys.KeySenderID ] = ID;
															return message;
														} )
													.ToList();
					await ValidateInboundMessages( context, messages );
					foreach( var message in messages )
					{
						if( string.IsNullOrWhiteSpace(message.TryGetString(MessageKeys.KeyReceiverID)) )
						{
							// There is no receiver defined for this message => Set the default one
							message[ MessageKeys.KeyReceiverID ] = DefaultReceiverID;

							if( string.IsNullOrWhiteSpace(message.TryGetString(MessageKeys.KeyReceiverID)) )
								// No default defined => Throw fatal exception ...
								throw new MissingFieldException( $"The message has no '{MessageKeys.KeyReceiverID}' defined" );
						}
					}

					// Give the messages to the Broker
					await Broker.ReceiveMessages( messages );

					// NB: The first idea was to send pending messages (if there were any available) through the "messages" request instead of waiting for the "polling" request to send them.
					// But since both requests could be sending concurrently, it would be hard for the client-side to receive all messages IN THE RIGHT ORDER
					// => Always reply with an empty response message list ; Don't send the pending messages

					var response = new CRootMessage {	{ RootMessageKeys.KeyType,		RootMessageKeys.TypeMessages },
														{ RootMessageKeys.TypeMessages,	new TMessage[]{} } };
					return response; }

				default:
					throw new NotImplementedException( $"Unsupported root message type '{messageType}'" );
			}
		}

		async Task IEndPoint.ReceiveMessages(IEnumerable<TMessage> messages)
		{
			ASSERT( Broker != null, $"Property '{nameof(Broker)}' is supposed to be set here" );
			ASSERT( messages != null, $"Missing parameter '{nameof(messages)}'" );

			var completionSource = System.Threading.Interlocked.Exchange( ref CompletionSource, null );
			if( completionSource == null )
			{
				// Race condition: This connection has already been processed
				FAIL( $"Sending messages to a closed connection" );  // nb: CAN happen, but should be exceptional ; If this happens often, something's wrong ...

				// Put the messages back on the queue
				await Broker.ReceiveMessages( messages );
				return;
			}

			var messagesList = messages.ToList();
			try
			{
				await ValidateOutboundMessages( messagesList );
			}
			catch( System.Exception ex )
			{
				// 'ValidateOutboundMessages()' is not supposed to fail ... => Sending one single error message instead
				FAIL( $"{nameof(ValidateOutboundMessages)}() threw an exception ({ex.GetType().FullName}): {ex.Message}" );
				var errorMessage = Broker.FillException( Broker.NewMessage(), ex );
				messagesList = new List<TMessage>{ errorMessage };
			}

			var response = new CRootMessage {	{ RootMessageKeys.KeyType,		RootMessageKeys.TypeMessages },
												{ RootMessageKeys.TypeMessages,	messagesList } };
			completionSource.SetResult( response );
		}

		public async Task ResetNow()
		{
			await Broker.UnRegisterEndpoint( ID );

			var completionSource = System.Threading.Interlocked.Exchange( ref CompletionSource, null );
			if( completionSource == null )
			{
				// Race condition: This connection has already been processed
				FAIL( $"Sending messages to a closed connection" );  // nb: CAN happen, but should be very exceptional ; If this happens often, something's wrong ...
				return;
			}

			// Send a reset message to the client
			var rootMessage = new CRootMessage{ { RootMessageKeys.KeyType, RootMessageKeys.TypeReset } };
			completionSource.SetResult( rootMessage );
		}
	}
}
