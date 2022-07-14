//
// CommonLibs/Web/LongPolling/Utils/ExtensionMethods.cs
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

namespace CommonLibs.Web.LongPolling.Utils
{
	public static class ExtensionMethods
	{

		public static List<IConnection> TryGetConnectionList(this IDictionary<string,List<IConnection>> dict, string sessionID)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(sessionID), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'sessionID'" );  // Assuming null or "" is a sign of an error
			List<IConnection> connectionList;
			if(! dict.TryGetValue(sessionID, out connectionList) )
				return null;
			CommonLibs.Utils.Debug.ASSERT( connectionList != null, System.Reflection.MethodInfo.GetCurrentMethod(), "The connection list of session '" + sessionID + "' is null!" );
			return connectionList;
		}

		public static IConnection TryGetConnection(this Dictionary<string,IConnection> dict, string connectionID)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(connectionID), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'connectionID'" );  // Assuming null or "" is a sign of an error
			IConnection connection;
			if(! dict.TryGetValue(connectionID, out connection) )
				return null;
			CommonLibs.Utils.Debug.ASSERT( connection != null, System.Reflection.MethodInfo.GetCurrentMethod(), "The connection '" + connectionID + "' exists in the list but is null!" );
			return connection;
		}
	}
}
