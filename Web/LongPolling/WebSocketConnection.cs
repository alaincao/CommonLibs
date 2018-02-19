using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Web;

using CommonLibs.Utils;
using CommonLibs.Web.LongPolling.Utils;

using HttpContext = Microsoft.AspNetCore.Http.HttpContext;

namespace CommonLibs.Web.LongPolling
{
	internal class WebSocketConnection : IConnection
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		internal MessageHandler		MessageHandler		{ get; set; }
		internal ConnectionList		ConnectionList		{ get { return MessageHandler.ConnectionList; } }
		internal bool				Registered			{ get; private set; }

		private HttpContext			HttpContext;
		internal WebSocket			Socket;
		private object				MessageContext;

		#region For IConnection

		public string				SessionID			{ get; private set; }
		public string				ConnectionID		{ get; private set; }
		public bool					Sending				{ get; set; }

		#endregion

		internal WebSocketConnection(MessageHandler messageHandler, HttpContext httpContext, WebSocket webSocket, string sessionID, object messageContext)
		{
			ASSERT( messageHandler != null, "Missing parameter 'messageHandler'" );
			ASSERT( httpContext != null, "Missing parameter 'context'" );
			ASSERT( webSocket != null, "Missing parameter 'webSocket'" );
			ASSERT( !string.IsNullOrEmpty(sessionID), "Missing parameter 'sessionID'" );
			LOG( "WebSocketConnection "+SessionID+" - Constructor" );

			MessageHandler = messageHandler;
			Registered = false;
			HttpContext = httpContext;
			Socket = webSocket;
			MessageContext = messageContext;
			SessionID = sessionID;
			ConnectionID = null;  // NB: Set in'ReceiveInitMessage()' below
			Sending = false;
		}

		internal async Task ReceiveInitMessage(HttpContext context, int bufferSize, int initTimeoutSeconds)
		{
			ASSERT( Socket.State == System.Net.WebSockets.WebSocketState.Open, "'WebSocket' state is not 'Open'" );
			LOG( "WebSocketConnection "+SessionID+" - ReceiveInitMessage" );

			// Wait for the initial message from the peer
			LOG( "WebSocketConnection "+SessionID+" - ReceiveInitMessage()" );
			var initMessage = await ReceiveJSon( bufferSize:bufferSize, timeOutSeconds:initTimeoutSeconds );

			var messageType = initMessage.TryGet( RootMessage.TypeKey ) as string;
			if( messageType != RootMessage.TypeInit )
				throw new WebSocketMiddleWare.CloseConnectionException( "Invalid message type '"+(messageType??"<NULL>")+"'. Expected '"+RootMessage.TypeInit+"'" );

			// Validate the init message against the application
			if( MessageHandler.RestoreMessageContextObject != null )
				MessageHandler.RestoreMessageContextObject( MessageContext );  // NB: Not in the HttpContext anymore
			bool initAccepted = ConnectionList.CheckInitAccepted( initMessage, HttpContext );
			if(! initAccepted )
			{
				LOG( "WebSocketConnection "+SessionID+" - Init refused" );

				// Send 'logout'
				var logoutMessage = RootMessage.CreateServer_Logout();
				Sending = true;
				await SendJSon( logoutMessage );

				// Close connection
				throw new WebSocketMiddleWare.CloseConnectionException();
			}

			// Allocate a new ConnectionID
			ConnectionID = ConnectionList.AllocateNewConnectionID( context, SessionID );

			// Assign this connection to this ConnectionID
			if(! ConnectionList.RegisterConnection(this, startStaleTimeout:false) )
				throw new WebSocketMiddleWare.CloseConnectionException( "Could not register connection. Invalid SessionID/ConnectionID" );
			Registered = true;

			// Send the ConnectionID to the peer
			var responseMessage = RootMessage.CreateServer_Init( ConnectionID );
			LOG( "WebSocketConnection "+SessionID+" - Response message set to 'init'" );
			Sending = true;
			await SendJSon( responseMessage );
		}

		internal async Task MainLoop(int bufferSize)
		{
			while( Socket.State == System.Net.WebSockets.WebSocketState.Open )
			{
				var requestMessage = await ReceiveJSon( bufferSize:bufferSize );
				if( requestMessage == null )
					break;

				var messageType = requestMessage.TryGet( RootMessage.TypeKey ) as string;
				switch( messageType )
				{
					case RootMessage.TypeMessages: {
						ASSERT( ConnectionList.CheckConnectionIsValid(SessionID, ConnectionID), "The WebSocket is not registered in the 'ConnectionList'" );  // The connection should have been closed somewhere ...

						foreach( var objMessageItem in (IEnumerable)requestMessage[RootMessage.KeyMessageMessagesList] )
						{
							Message message = null;
							try
							{
								if( MessageHandler.RestoreMessageContextObject != null )
									MessageHandler.RestoreMessageContextObject( MessageContext );

								var messageItem = (IDictionary<string,object>)objMessageItem;
								var receivedMessage = Message.CreateReceivedMessage( ConnectionID, messageItem );
								LOG( "BeginProcessRequest() - Receiving message '" + receivedMessage + "'" );
								receivedMessage[ Message.KeySenderID ] = ConnectionID;
								MessageHandler.ReceiveMessage( receivedMessage );
							}
							catch( System.Exception ex )
							{
								LOG( "BeginProcessRequest() *** Exception (" + ex.GetType().FullName + ") while receiving message" );

								// Send exception through MessageHandler so that we can continue to parse the other messages
								if( message == null )
									MessageHandler.SendMessageToConnection( ConnectionID, Message.CreateExceptionMessage(exception:ex) );
								else
									MessageHandler.SendMessageToConnection( ConnectionID, Message.CreateExceptionMessage(exception:ex, sourceMessage:message) );
							}
						}
						break; }

					default:
						throw new NotImplementedException( "Unsupported root message type '" + messageType + "'" );
				}
			}
		}

		public void SendRootMessage(RootMessage rootMessage)
		{
			var task = SendJSon( rootMessage );
			task.Wait();
		}

		internal async Task SendJSon(IDictionary<string,object> message)
		{
			ASSERT( message != null, "Missing parameter 'message'" );
			ASSERT( Sending, "Sending a message, but the 'Sending' property is not set" );
			await SendJSon( Socket, message );
			Sending = false;
		}

		public static async Task SendJSon(WebSocket socket, IDictionary<string,object> message)
		{
			CommonLibs.Utils.Debug.ASSERT( socket != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'socket'" );
			CommonLibs.Utils.Debug.ASSERT( message != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'message'" );

			var str = message.ToJSON();
			var buffer = System.Text.Encoding.UTF8.GetBytes( str );
			await socket.SendAsync( new ArraySegment<byte>(buffer), System.Net.WebSockets.WebSocketMessageType.Text, true, System.Threading.CancellationToken.None );
		}

		private async Task<IDictionary<string,object>> ReceiveJSon(int bufferSize=WebSocketMiddleWare.DefaultBufferSize, int? timeOutSeconds=null)
		{
			var dict = await ReceiveJSon( Socket, bufferSize, timeOutSeconds );
			return dict;
		}

		public static async Task<IDictionary<string,object>> ReceiveJSon(WebSocket socket, int bufferSize=WebSocketMiddleWare.DefaultBufferSize, int? timeOutSeconds=null)
		{
			CommonLibs.Utils.Debug.ASSERT( socket != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'socket'" );

			var cancellationToken = System.Threading.CancellationToken.None;
			if( timeOutSeconds != null )
			{
				var tokenSource = new System.Threading.CancellationTokenSource();
				tokenSource.CancelAfter( millisecondsDelay:timeOutSeconds.Value * 1000 );
				cancellationToken = tokenSource.Token;
			}

			// Read from socket
			var stream = new System.IO.MemoryStream();
			var buffer = System.Net.WebSockets.WebSocket.CreateClientBuffer( bufferSize, bufferSize );
		RECEIVE_AGAIN:
			var result = await socket.ReceiveAsync( buffer, cancellationToken  );
			stream.Write( buffer.Array, 0, result.Count );
			if(! result.EndOfMessage )
				goto RECEIVE_AGAIN;

			// Parse byte array
			var json = System.Text.Encoding.UTF8.GetString( stream.ToArray() );
			var dict = json.FromJSONDictionary();
			return dict;
		}

		/// <summary>
		/// Close the WebSocket
		/// </summary>
		public void Close(RootMessage rootMessage)
		{
			ASSERT( Socket.State == System.Net.WebSockets.WebSocketState.Open, "WebSocket close requested but its state is not 'Open' but '"+Socket.State+"'" );

			try { SendRootMessage( rootMessage ); }
			catch( System.Exception ex ) { FAIL( "Exception while sending close message ("+ex.GetType().FullName+"): "+ex.Message ); }

			try
			{
				var task = Socket.CloseOutputAsync( System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Close requested", System.Threading.CancellationToken.None );
				task.Wait();
			}
			catch( System.Exception ex )
			{
				//LOG( "*** Error while closing the WebSocket (" + ex.GetType().FullName + "): " + ex.Message );
				FAIL( "Error while closing the WebSocket (" + ex.GetType().FullName + "): " + ex.Message );
			}
		}
	}
}
