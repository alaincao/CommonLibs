using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CommonLibs.Web.LongPolling.Utils
{
	public static class ExtensionMethods
	{
		public static string TryGetString(this Dictionary<string,object> dict, string key)
		{
			System.Diagnostics.Debug.Assert( dict != null, "Missing 'this' parameter" );
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(key), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			System.Diagnostics.Debug.Assert( obj is string, "Dictionary's value is not a string" );
			return (string)obj;
		}

		public static List<IConnection> TryGetConnectionList(this Dictionary<string,List<IConnection>> dict, string sessionID)
		{
			System.Diagnostics.Debug.Assert( dict != null, "Missing 'this' parameter" );
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(sessionID), "Missing parameter 'sessionID'" );  // Assuming null or "" is a sign of an error
			List<IConnection> connectionList;
			if(! dict.TryGetValue(sessionID, out connectionList) )
				return null;
			System.Diagnostics.Debug.Assert( connectionList != null, "The connection list of session '" + sessionID + "' is null!" );
			return connectionList;
		}

		public static IConnection TryGetConnection(this Dictionary<string,IConnection> dict, string connectionID)
		{
			System.Diagnostics.Debug.Assert( dict != null, "Missing 'this' parameter" );
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(connectionID), "Missing parameter 'connectionID'" );  // Assuming null or "" is a sign of an error
			IConnection connection;
			if(! dict.TryGetValue(connectionID, out connection) )
				return null;
			System.Diagnostics.Debug.Assert( connection != null, "The connection '" + connectionID + "' exists in the list but is null!" );
			return connection;
		}
	}
}
