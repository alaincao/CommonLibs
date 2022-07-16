//
// CommonLibs/Web/LongPolling/Utils/ExtensionMethods.cs
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
