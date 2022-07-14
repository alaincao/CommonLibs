//
// CommonLibs/Utils/WebSocket.cs
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
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;

using CommonLibs.MessagesBroker.Utils;

namespace CommonLibs.Utils
{
	internal static class WebSocketUtils
	{
		public const int		DefaultBufferSize		= 1024 * 4;

		public static async Task SendJsonDictionary(WebSocket socket, IDictionary<string,object> message)
		{
			CommonLibs.Utils.Debug.ASSERT( message != null, System.Reflection.MethodInfo.GetCurrentMethod(), $"Missing parameter '{nameof(message)}'" );

			var json = message.ToJSON();
			await SendString( socket, json );
		}

		public static async Task SendString(WebSocket socket, string str)
		{
			CommonLibs.Utils.Debug.ASSERT( socket != null, System.Reflection.MethodInfo.GetCurrentMethod(), $"Missing parameter '{nameof(socket)}'" );
			CommonLibs.Utils.Debug.ASSERT( str != null, System.Reflection.MethodInfo.GetCurrentMethod(), $"Missing parameter '{nameof(str)}'" );

			var buffer = System.Text.Encoding.UTF8.GetBytes( str );
			await socket.SendAsync( new ArraySegment<byte>(buffer), System.Net.WebSockets.WebSocketMessageType.Text, true, System.Threading.CancellationToken.None );
		}

		public static async Task<IDictionary<string,object>> ReceiveJsonDictionary(WebSocket socket, int? timeOutSeconds=null, int bufferSize=DefaultBufferSize)
		{
			var json = await ReceiveString( socket, timeOutSeconds, bufferSize );
			if( string.IsNullOrEmpty(json) )
				return null;
			var dict = json.FromJSONDictionary();
			return dict;
		}

		public static async Task<string> ReceiveString(WebSocket socket, int? timeOutSeconds=null, int bufferSize=DefaultBufferSize)
		{
			CommonLibs.Utils.Debug.ASSERT( socket != null, System.Reflection.MethodInfo.GetCurrentMethod(), $"Missing parameter '{nameof(socket)}'" );

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
			WebSocketReceiveResult result;
			do {
				result = await socket.ReceiveAsync( buffer, cancellationToken  );
				stream.Write( buffer.Array, 0, result.Count );
			} while( ! result.EndOfMessage );

			// Parse byte array
			var rv = System.Text.Encoding.UTF8.GetString( stream.ToArray() );
			return rv;
		}

		public static async Task Close(WebSocket socket, string statusDescription="Close requested")
		{
			CommonLibs.Utils.Debug.ASSERT( socket != null, System.Reflection.MethodInfo.GetCurrentMethod(), $"Missing parameter '{nameof(socket)}'" );

			await socket.CloseOutputAsync( System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, statusDescription, System.Threading.CancellationToken.None );
		}
	}
}
