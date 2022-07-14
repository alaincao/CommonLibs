//
// CommonLibs/Web/LongPolling/CSClient/WebSocketClient.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2020 - 2021 Alain CAO
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
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;

using CommonLibs.Web.LongPolling.Utils;

namespace CommonLibs.Web.LongPolling.CSClient
{
	internal class WebSocketClient : BaseClient
	{
		public string				HandlerUrl		{ get; private set; }
		private ClientWebSocket		WebSocket		= null;

		/// <param name="cookies">Contains the ASP.NET session cookie</param>
		public WebSocketClient(MessageHandler messageHandler, System.Net.CookieContainer cookies, string handlerUrl, string keepaliveUrl=null) : base(messageHandler, cookies)
		{
			// TODO: Alain: KeepAliveUrl (?)
			ASSERT( !string.IsNullOrWhiteSpace(handlerUrl), "Missing parameter 'handlerUrl'" );

			HandlerUrl = handlerUrl;
		}

		protected internal override async Task<string> SendInitMessage()
		{
			ASSERT( WebSocket == null, "The 'WebSocket' is not supposed to be set here" );

			var socket = new ClientWebSocket();
			try
			{
				LOG( "Connect & send init" );
				socket.Options.Cookies = Cookies;
				await socket.ConnectAsync( new Uri(HandlerUrl), System.Threading.CancellationToken.None );
				await WebSocketConnection.SendJSon( socket, InitMessage );

				LOG( "Receive init response" );
				var response = await WebSocketConnection.ReceiveJSon( socket, WebSocketHandler.InitTimeoutSeconds );
				var rootMessageType = (string)response[RootMessage.TypeKey];
				if( rootMessageType != RootMessage.TypeInit )
					throw new ArgumentException( "The server returned an invalid message type. Expected 'init', received '"+response[RootMessage.TypeKey]+"'" );

				var connectionID = (string)response[RootMessage.KeySenderID];
				LOG( "ConnectionID: "+connectionID );
				if( string.IsNullOrWhiteSpace(connectionID) )
					throw new ArgumentException( "The server did not return the ConnectionID" );

				WebSocket = socket;
				socket = null;
				return connectionID;
			}
			finally
			{
				if( socket != null )
				{
					try
					{
						// Not terminated correctly
						socket.Dispose();
					}
					catch( System.Exception ex )
					{
						FAIL( "'socket.Dispose()' threw an exception ("+ex.GetType().FullName+"): "+ex.Message );
					}
				}
			}
		}

		protected internal override async Task MainLoop()
		{
			LOG( "MainLoop()" );
			ASSERT( WebSocket != null, "Property 'WebSocket' is supposed to be set here" );
			ASSERT( Status == ConnectionStatus.Connected, "'Status' is supposed to be 'Connected' here" );

			try
			{
				while( WebSocket.State == System.Net.WebSockets.WebSocketState.Open )
				{
					LOG( "Wait for message" );
					var rootMessage = RootMessage.CreateClient_ServerResponse(await WebSocketConnection.ReceiveJSon(WebSocket) );
					if( rootMessage == null )
						break;

					var rootMessageType = rootMessage[RootMessage.TypeKey] as string;
					LOG( "Root message received: "+rootMessageType );
					switch( rootMessageType )
					{
						case RootMessage.TypeReset:
							// Ignore
							FAIL( "WebSocketClient received a 'reset' message (should not happen)" );  // The server is not supposed to send these messages on a WebSocket connection
							continue;

						case RootMessage.TypeLogout:
							// Terminate MainLoop
							break;

						case RootMessage.TypeMessages:
							ReceiveMessages( rootMessage );
							break;

						default:
							throw new NotImplementedException( "Unsupported response message type '" + rootMessageType + "'" );
					}
				}
			}
			catch( System.Exception ex )
			{
				switch( Status )
				{
					case ConnectionStatus.Closing:
					case ConnectionStatus.Disconnected:
						// Error received while closing the WebSocket (aborting socket) => No need to report
						break;
					default:
						TriggerInternalError( "Error while reading message from the WebSocket", ex );
						break;
				}
			}

			Stop();
		}

		protected internal override async Task CloseConnection(string connectionID)
		{
			LOG( "CloseConnection()" );
			ASSERT( Status == ConnectionStatus.Closing, "Property 'Status' is supposed to be 'Closing' here" );
			ASSERT( WebSocket != null, "Property 'WebSocket' is supposed to be set here" );

			// Take possession of the WebSocket instance
			ClientWebSocket webSocket;
			lock( LockObject )
			{
				webSocket = WebSocket;
				WebSocket = null;
			}

			try
			{
				await webSocket.CloseOutputAsync( WebSocketCloseStatus.NormalClosure, string.Empty, System.Threading.CancellationToken.None );
			}
			catch( System.Exception ex )
			{
				FAIL( "Could not gracefully close the websocket: " + ex.Message );
			}
			ASSERT( webSocket.State != WebSocketState.Open, "The 'webSocket' should be closing now, but its state is still 'Open'" );

			try
			{
				webSocket.Dispose();
			}
			catch( System.Exception ex )
			{
				FAIL( "'webSocket.Dispose()' threw an exception ("+ex.GetType().FullName+"): "+ex.Message );
			}
		}

		protected internal override async Task SendRootMessage(RootMessage rootMessage)
		{
			ASSERT( rootMessage != null, "Missing parameter 'rootMessage'" );
			ASSERT( WebSocket != null, "Property 'WebSocket' is supposed to be set here" );
			await WebSocketConnection.SendJSon( WebSocket, rootMessage );
		}
	}
}
