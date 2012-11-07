using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CommonLibs.Web.LongPolling.Utils
{
	public static class ExtensionMethods
	{
		public static object TryGetValue(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj = null;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			return obj;
		}

		public static string TryGetString(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			return "" + obj;
		}

		public static byte? TryGetByte(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			var a = obj as byte?;
			if( a != null )
				return a.Value;
			byte b;
			if( byte.TryParse(""+obj, out b) )
				return b;
			else
				return null;
		}

		public static short? TryGetShort(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			var a = obj as short?;
			if( a != null )
				return a.Value;
			short b;
			if( short.TryParse(""+obj, out b) )
				return b;
			else
				return null;
		}

		public static int? TryGetInt(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			var a = obj as int?;
			if( a != null )
				return a.Value;
			int b;
			if( int.TryParse(""+obj, out b) )
				return b;
			else
				return null;
		}

		public static long? TryGetLong(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			var a = obj as long?;
			if( a != null )
				return a.Value;
			long b;
			if( long.TryParse(""+obj, out b) )
				return b;
			else
				return null;
		}

		public static bool? TryGetBool(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error

			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			var a = obj as bool?;
			return a;
		}

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

		public static string ToJSON(this IDictionary<string,object> dict)
		{
			var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
			var str = serializer.Serialize( dict );
			return str;
		}
	}
}
