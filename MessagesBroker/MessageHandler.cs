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
using System.Collections.Generic;
using System.Threading.Tasks;

using CommonLibs.Utils;
using CommonLibs.MessagesBroker.Utils;

namespace CommonLibs.MessagesBroker
{
	using TMessage = IDictionary<string,object>;

	public interface IEndPoint
	{
		string	ID			{ get; }
		/// <summary>Set to 'true' if this endpoint must be unregistered immediately after receiving messages (i.e. 'ReceiveMessages()' can only be invoked once)</summary>
		bool	IsOneShot	{ get; }
		Task ReceiveMessages(IEnumerable<TMessage> messages);
	}

	public class MessageHandler : IEndPoint
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		public string		ID			{ get; protected set; } = Guid.NewGuid().ToString();
		bool IEndPoint.		IsOneShot	=> false;
		public IBroker		Broker		{ get; private set; }
		protected object	Locker		{ get; private set; } = new object();

		protected Dictionary<string,Func<TMessage,Task>>	Handlers	= new Dictionary<string, Func<TMessage,Task>>();

		public Func<TMessage,Task>				OnUnknownMessageReceived	{ get; set; }
		public Func<TMessage,Exception,Task>	OnHandlerException			{ get; set; }

		public MessageHandler(IBroker broker)
		{
			ASSERT( broker != null, $"Missing parameter '{nameof(broker)}'" );

			Broker = broker;
			OnUnknownMessageReceived	= OnUnknownMessageReceived_Default;
			OnHandlerException			= OnHandlerException_Default;
		}

		public void AddMessageHandler(string messageType, Func<TMessage,Task> callback)
		{
			ASSERT( !string.IsNullOrWhiteSpace(messageType), $"Missing parameter '{nameof(messageType)}'" );
			ASSERT( callback != null, $"Missing parameter '{nameof(callback)}'" );

			lock( Locker )
			{
				Handlers.Remove( messageType );
				Handlers[ messageType ] = callback;
			}
		}

		public Task ReceiveMessages(IEnumerable<TMessage> messages)
		{
			var callbacks = (new{	Message		= (TMessage)null,
									Callback	= (Func<TMessage,Task>)null,
								}).NewAnonymousList();
			lock( Locker )
			{
				foreach( var message in messages )
				{
					var type = message.TryGetString( MessageKeys.KeyMessageHandler );
					callbacks.Add( new{	Message		= message,
										Callback	= Handlers.TryGet( type ),
									} );
				}
			}

			foreach( var item in callbacks )
			{
				Task.Run( async ()=>
					{
						if( item.Callback != null )
						{
							try{ await item.Callback( item.Message ); }
							catch( System.Exception ex ){ await OnHandlerException( item.Message, ex ); }
						}
						else
						{
							await OnUnknownMessageReceived( item.Message );
						}
					} )
					.FireAndForget();
			}

			return Task.FromResult( 0 );
		}

		private async Task OnUnknownMessageReceived_Default(TMessage message)
		{
			ASSERT( message != null, $"Missing parameter '{nameof(message)}'" );

			if( string.IsNullOrWhiteSpace(message.TryGetString(MessageKeys.KeySenderID)) )
			{
				FAIL( $"{nameof(MessageHandler)}: Received message with unknown type '{message.TryGetString(MessageKeys.KeyMessageHandler)}'" );
				// Discard
			}
			else
			{
				// Send response
				var response = Broker.FillAsResponse( source:message, destination:Broker.NewMessage() );
				try{ throw new ArgumentException($"{nameof(MessageHandler)}: Received message with unknown type '{message.TryGetString(MessageKeys.KeyMessageHandler)}'"); } catch( System.Exception ex ){ Broker.FillException(response, ex); }
				await Broker.ReceiveMessages( new TMessage[]{ response } );
			}
		}

		private async Task OnHandlerException_Default(TMessage message, Exception exception)
		{
			ASSERT( message != null, $"Missing parameter '{nameof(message)}'" );
			ASSERT( exception != null, $"Missing parameter '{nameof(exception)}'" );

			var asyncException = exception as System.Reflection.TargetInvocationException;
			if( asyncException != null )
			{
				// Unwrap those crappy exception ...
				await OnHandlerException_Default( message, asyncException.InnerException );
			}
			else if( string.IsNullOrWhiteSpace(message.TryGetString(MessageKeys.KeySenderID)) )
			{
				FAIL( $"The message handler for '{message.TryGetString(MessageKeys.KeyMessageHandler)}' had no sender defined and threw an exception ({exception.GetType().FullName}): {exception.Message}" );
				// Discard
			}
			else
			{
				// Send response
				var response = Broker.FillAsResponse( source:message, destination:Broker.NewMessage() );
				Broker.FillException( response, exception );
				await Broker.ReceiveMessages( new TMessage[]{ response } );
			}
		}
	}
}
