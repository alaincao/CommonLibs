//
// CommonLibs/Web/LongPolling/Utils/ExtensionMethods.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2018 Alain CAO
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

		public static List<Newtonsoft.Json.JsonConverter>		ToJSONConverters			= null;
		public static bool										ToJSONFirstLetterLowerCased	= false;
		public static List<Newtonsoft.Json.JsonConverter>		FromJSONConverters			= new List<Newtonsoft.Json.JsonConverter>{
																									new JsonDictionaryConverter(),  // Serialize JSON dictionaries to 'IDictionary<string,object>' instead of Newtonsoft's 'JObject'
																								};

		public static string ToJSON(this object obj, IEnumerable<Newtonsoft.Json.JsonConverter> converters=null, bool indented=false)
		{
			if( ToJSONConverters != null )
			{
				if( converters == null )
					converters = ToJSONConverters;
				else
					converters = converters.Concat( ToJSONConverters );
			}

			var settings = new Newtonsoft.Json.JsonSerializerSettings();
			settings.Formatting = indented ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None;
			if( converters != null )
				settings.Converters = converters.ToList();
			if( ToJSONFirstLetterLowerCased )
				settings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
			return Newtonsoft.Json.JsonConvert.SerializeObject( obj, settings );
		}

		public static IDictionary<string,object> FromJSONDictionary(this string str, IEnumerable<Newtonsoft.Json.JsonConverter> converters=null)
		{
			if( FromJSONConverters != null )
			{
				if( converters == null )
					converters = FromJSONConverters;
				else
					converters = converters.Concat( FromJSONConverters );
			}
			if( converters == null )
				return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>( str );
			else
				return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>( str, converters:converters.ToArray() );
		}
	}
}
