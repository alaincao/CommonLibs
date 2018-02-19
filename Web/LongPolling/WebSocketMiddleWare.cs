//
// CommonLibs/Web/LongPolling/WebSocketMiddleWare.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2018 SigmaConso
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
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CommonLibs.Web.LongPolling
{
	public static class WebSocketMiddleWare
	{
		internal const int		DefaultBufferSize	= 1024;

		internal class CloseConnectionException : ApplicationException
		{
			internal bool	IsError		{ get; private set; }
			internal CloseConnectionException()					: base()		{ IsError = false; }
			internal CloseConnectionException(string message)	: base(message)	{ IsError = true; }
		}

		public static IApplicationBuilder UseMessageHandlerWebSocket(this IApplicationBuilder app, string route, int bufferSize=DefaultBufferSize, int initTimeoutSeconds=5)
		{
			app.Use( async (context, next) =>
				{
					if( context.Request.Path != route )
						goto NEXT;
					if(! context.WebSockets.IsWebSocketRequest )
						goto NEXT;

					var messageHandler = context.RequestServices.GetRequiredService<MessageHandler>();
					await ProcessWebSocketRequest( messageHandler, context, bufferSize:bufferSize, initTimeoutSeconds:initTimeoutSeconds );

					return;
				NEXT:
					await next();
				} );
			return app;
		}

		private static async Task ProcessWebSocketRequest(MessageHandler messageHandler, HttpContext httpContext, int bufferSize, int initTimeoutSeconds)
		{
			object messageContext = null;
			if( messageHandler.SaveMessageContextObject != null )
				messageContext = messageHandler.SaveMessageContextObject();

			var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
			var sessionID = messageHandler.ConnectionList.GetSessionIDFromHttpContext( httpContext );
			if( string.IsNullOrEmpty(sessionID) )
				throw new ArgumentException( "No SessionID available for this connection" );

			var conn = new WebSocketConnection( messageHandler, httpContext, webSocket, sessionID, messageContext );
			System.Exception exception = null;
			try
			{
				await conn.ReceiveInitMessage( httpContext, bufferSize:bufferSize, initTimeoutSeconds:initTimeoutSeconds );
				await conn.MainLoop( bufferSize:bufferSize );
			}
			catch( CloseConnectionException ex )
			{
				if( ex.IsError )
					exception = ex;
			}
			catch( System.Exception ex )
			{
				exception = ex;
			}

			if( conn.Registered )
				messageHandler.ConnectionList.UnregisterConnection( conn );

			// Close socket
			if( conn.Socket.State == System.Net.WebSockets.WebSocketState.Open ) try
			{
				if( exception != null )
				{
					var exceptionMessage = RootMessage.CreateServer_MessagesList( new Message[]{ Message.CreateExceptionMessage(exception:exception) } );
					conn.Sending = true;
					await conn.SendJSon( exceptionMessage );
				}

				await conn.Socket.CloseOutputAsync( System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, string.Empty, System.Threading.CancellationToken.None );
			}
			catch( System.Exception ex )
			{
				CommonLibs.Utils.Debug.ASSERT( false, typeof(WebSocketMiddleWare), "Could not gracefully close the websocket: " + ex.Message );
			}
		}
	}
}
