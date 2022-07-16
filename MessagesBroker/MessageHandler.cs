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

		public Func<TMessage,Task>				OnUnknownMessageReceived;
		public Func<TMessage,Exception,Task>	OnHandlerException;

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
				var notAwaited = Task.Run( async ()=>  // nb: Run asynchroneously
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
					} );
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
				try{ throw new ArgumentException($"{nameof(MessageHandler)}: Received message with unknown type '{message.TryGetString(MessageKeys.KeyMessageHandler)}'"); } catch( System.Exception ex ){ Broker.FillException(response, ex); };
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
