//
// CommonLibs/MessagesBroker/Utils/ExtensionMethods.cs
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

using System.Collections.Generic;
using System.Linq;

namespace CommonLibs.MessagesBroker.Utils
{
	public static class ExtensionMethods
	{
		public static List<Newtonsoft.Json.JsonConverter>		ToJSONConverters			{ get; set; } = null;
		public static bool										ToJSONFirstLetterLowerCased	{ get; set; } = false;
		public static List<Newtonsoft.Json.JsonConverter>		FromJSONConverters			{ get; set; } = new List<Newtonsoft.Json.JsonConverter>{
																									new JsonDictionaryConverter(),  // Serialize JSON dictionaries to 'IDictionary<string,object>' instead of Newtonsoft's 'JObject'
																								};

		public static string ToJSON(this object obj, IEnumerable<Newtonsoft.Json.JsonConverter> converters=null, bool indented=false, bool ignoreNulls=false)
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
			if( ignoreNulls )
				settings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
			if( converters != null )
				settings.Converters = converters.ToList();
			if( ToJSONFirstLetterLowerCased )
				settings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
			return Newtonsoft.Json.JsonConvert.SerializeObject( obj, settings );
		}

		public static T FromJSON<T>(this string str, IEnumerable<Newtonsoft.Json.JsonConverter> converters=null)
		{
			if( FromJSONConverters != null )
			{
				if( converters == null )
					converters = FromJSONConverters;
				else
					converters = converters.Concat( FromJSONConverters );
			}
			if( converters == null )
				return Newtonsoft.Json.JsonConvert.DeserializeObject<T>( str );
			else
				return Newtonsoft.Json.JsonConvert.DeserializeObject<T>( str, converters:converters.ToArray() );
		}

		public static IDictionary<string,object> FromJSONDictionary(this string str, IEnumerable<Newtonsoft.Json.JsonConverter> converters=null)
		{
			return FromJSON<Dictionary<string, object>>( str, converters );
		}
	}
}
