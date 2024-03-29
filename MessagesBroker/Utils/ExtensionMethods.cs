﻿//
// CommonLibs/MessagesBroker/Utils/ExtensionMethods.cs
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
