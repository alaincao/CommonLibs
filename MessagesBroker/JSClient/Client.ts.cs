//
// CommonLibs/MessagesBroker/JSClient/LongPollingClient.js.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2018 Alain CAO
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

namespace CommonLibs.MessagesBroker
{
	public static partial class JSClient
	{
		/// <param name="httpHandlerUrl">The URL of the http handler that implement 'LongPollingHandler'</param>
		public static IDictionary<string,object> CreateClientParameters(Func<string,string> resolveUrl, string httpHandlerUrl=null, object initMessageTemplate=null, bool? debug=false)
		{
			var dict = new Dictionary<string,object>();
			if( httpHandlerUrl != null )
				dict["httpHandlerUrl"] = resolveUrl( httpHandlerUrl );
			if( initMessageTemplate != null )
				dict["initMessageTemplate"] = initMessageTemplate;
			if( debug == true )
				dict["debug"] = debug;
			return dict;
		}
	}
}
