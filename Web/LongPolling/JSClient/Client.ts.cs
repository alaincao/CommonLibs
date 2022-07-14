//
// CommonLibs/Web/LongPolling/JSClient/LongPollingClient.js.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2013 Alain CAO
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
using System.Linq;
using System.Web;
using System.Web.UI;

using CommonLibs.Utils;

namespace CommonLibs.Web.LongPolling
{
	public static partial class JSClient
	{
		/// <param name="httpHandlerUrl">The URL of the http handler that implement 'LongPollingHandler'</param>
		/// <param name="webSocketHandlerUrl">The URL of the http handler that implements 'WebSocketHandler')</param>
		/// <param name="webSocketKeepAliveUrl">The URL used by the WebSocket client to "ping" the server so the ASP.NET session doesn't timeout</param>
		/// <param name="webSocketKeepaliveTimeoutMinutes">The delay between "pings" to 'webSocketKeepAliveUrl'</param>
		/// <param name="syncedHandlerUrl">The URL to the Synced HTTP handler(used for e.g. file uploads)</param>
		/// <param name="logoutUrl">The URL to redirect to when the server asks to logout</param>
		public static IDictionary<string,object> CreateClientParameters(Func<string,string> resolveUrl, string httpHandlerUrl=null, string webSocketHandlerUrl=null, string webSocketKeepAliveUrl="~", int? webSocketKeepaliveTimeoutSeconds=null, string syncedHandlerUrl=null, string logoutUrl="~", bool? debug=false)
		{
			var dict = new Dictionary<string,object>();
			if( httpHandlerUrl != null )
				dict["httpHandlerUrl"] = resolveUrl( httpHandlerUrl );
			if( webSocketHandlerUrl != null )
				dict["webSocketHandlerUrl"] = resolveUrl( webSocketHandlerUrl );
			if( syncedHandlerUrl != null )
				dict["syncedHandlerUrl"] = resolveUrl( syncedHandlerUrl );
			if( logoutUrl != null )
				dict["logoutUrl"] = resolveUrl( logoutUrl );
			if( webSocketKeepAliveUrl != null )
				dict["webSocketKeepAliveUrl"] = resolveUrl( webSocketKeepAliveUrl );
			if( webSocketKeepaliveTimeoutSeconds != null )
				dict["webSocketKeepAliveTimeout"] = webSocketKeepaliveTimeoutSeconds * 1000;  // NB: Parameter in miliseconds
			if( debug == true )
				dict["debug"] = debug;
			return dict;
		}
	}
}
