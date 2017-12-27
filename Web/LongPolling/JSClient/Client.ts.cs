//
// CommonLibs/Web/LongPolling/JSClient/LongPollingClient.js.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2013 Alain CAO
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
