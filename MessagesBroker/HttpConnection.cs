//
// CommonLibs/MessagesBroker/MessageHandler.cs
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
		public string		DefaultReceiverID			= null;
		public int			StaleTimeoutMilisec			= DefaultStaleTimeoutMilisec;
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
						throw new ArgumentNullException( "ID" );
					LOG( $"ReceiveRequest() - Respond with 'init' message: {ID}" );

					var response = new CRootMessage {	{ RootMessageKeys.KeyType,		RootMessageKeys.TypeInit },
														{ RootMessageKeys.KeySenderID,	ID } };
					return response; }

				case RootMessageKeys.TypePoll: {
					// Validate ID for this connection
					ID = request.TryGetString( RootMessageKeys.KeySenderID );
					if( string.IsNullOrWhiteSpace(ID) )
						throw new ArgumentNullException( $"Missing '{RootMessageKeys.KeySenderID}' parameter from message" );
					await ValidateInboundMessages( context, new List<TMessage>() );

					// This object will receive the 'RootMessage' to send to the client as response
					var completionSource = new TaskCompletionSource<TRootMessage>();
					CompletionSource = completionSource;

					// Create a timeout to close this connection and avoid keeping it open too long
					var staleCancellation = new System.Threading.CancellationTokenSource();
					var staleTimeout = Task.Run( async ()=>  // nb: Run asynchroneously
						{
							await Task.Delay( StaleTimeoutMilisec, staleCancellation.Token );
							ResetNow();
						} );

					// Register against the broker
					await Broker.RegisterEndpoint( this );

					// Wait for any messages to send through this connection
					var response = await completionSource.Task;

					// Cancel the stale timeout if still running
					staleCancellation.Cancel();

					return response; }

				case RootMessageKeys.TypeMessages: {
					// Validate ID for this connection
					ID = request.TryGetString( RootMessageKeys.KeySenderID );
					if( string.IsNullOrWhiteSpace(ID) )
						throw new ArgumentNullException( $"Missing '{RootMessageKeys.KeySenderID}' parameter from message" );

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

		async Task IEndPoint.ReceiveMessages(IEnumerable<TMessage> msgs)
		{
			ASSERT( Broker != null, $"Property '{nameof(Broker)}' is supposed to be set here" );
			ASSERT( msgs != null, $"Missing parameter '{nameof(msgs)}'" );

			var completionSource = System.Threading.Interlocked.Exchange( ref CompletionSource, null );
			if( completionSource == null )
			{
				// Race condition: This connection has already been processed
				FAIL( $"Sending messages to a closed connection" );  // nb: CAN happen, but should be exceptional ; If this happens often, something's wrong ...

				// Put the messages back on the queue
				await Broker.ReceiveMessages( msgs );
				return;
			}

			var messages = msgs.ToList();
			try
			{
				await ValidateOutboundMessages( messages );
			}
			catch( System.Exception ex )
			{
				// 'ValidateOutboundMessages()' is not supposed to fail ... => Sending one single error message instead
				FAIL( $"{nameof(ValidateOutboundMessages)}() threw an exception ({ex.GetType().FullName}): {ex.Message}" );
				var errorMessage = Broker.FillException( Broker.NewMessage(), ex );
				messages = new List<TMessage>{ errorMessage };
			}

			var response = new CRootMessage {	{ RootMessageKeys.KeyType,		RootMessageKeys.TypeMessages },
												{ RootMessageKeys.TypeMessages,	messages } };
			completionSource.SetResult( response );
		}

		public void ResetNow()
		{
			Broker.UnRegisterEndpoint( ID );

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
