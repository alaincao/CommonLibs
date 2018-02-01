//
// CommonLibs/Web/LongPolling/JSClient/WebSocketClient.ts
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using CommonLibs.Web.LongPolling.Utils;

namespace CommonLibs.Web.LongPolling
{
	public static class HttpMiddleWare
	{
		[System.Diagnostics.Conditional("DEBUG")] private static void LOG(string message)				{ CommonLibs.Utils.Debug.LOG( typeof(HttpMiddleWare), message ); }
		[System.Diagnostics.Conditional("DEBUG")] private static void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, typeof(HttpMiddleWare), message ); }
		[System.Diagnostics.Conditional("DEBUG")] private static void FAIL(string message)				{ CommonLibs.Utils.Debug.ASSERT( false, typeof(HttpMiddleWare), message ); }

		public static IApplicationBuilder UseMessageHandlerHttp(this IApplicationBuilder app, MessageHandler messageHandler, string route)
		{
			app.Use( async (context, next) =>
				{
					if( context.Request.Path != route )
						goto NEXT;
					if( context.Request.Method != "POST" )
						goto NEXT;

					await ProcessRequest( messageHandler, context );

					return;
				NEXT:
					await next();
				} );
			return app;
		}

		private static async Task ProcessRequest(MessageHandler messageHandler, HttpContext context)
		{
			var connectionList = messageHandler.ConnectionList;
			var sessionID = connectionList.GetSessionIDFromHttpContext( context );
			var connection = new LongPollingConnection( messageHandler, context, sessionID );
			RootMessage response;
			try
			{
				RootMessage request;
				{
					var json = new StreamReader( context.Request.Body ).ReadToEnd();
					request = RootMessage.CreateServer_RawRequest( json );
				}
				response = await connection.ReceiveRequest( request );
			}
			catch( System.Exception ex )
			{
				response = RootMessage.CreateServer_Exception( ex );
			}
			await context.Response.WriteAsync( response.ToJSON() );
		}
	}
}
