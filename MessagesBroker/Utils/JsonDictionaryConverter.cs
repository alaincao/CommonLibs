//
// CommonLibs/MessagesBroker/JsonDictionaryConverter.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2018 SigmaConso
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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CommonLibs.MessagesBroker.Utils
{
	/// <summary>
	/// Converter for Newtonsoft's serializer to use 'IDictionary<string, object>' instead of Newtonsoft's 'JObject's
	/// </summary>
	/// <remarks>
	/// Loosely copied from the answer of Anish Patel at
	/// https://stackoverflow.com/questions/11561597/deserialize-json-recursively-to-idictionarystring-object/11761761
	/// </remarks>
	public class JsonDictionaryConverter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) { this.WriteValue(writer, value); }

		private void WriteValue(JsonWriter writer, object value) {
			var t = JToken.FromObject(value);
			switch (t.Type) {
				case JTokenType.Object:
					this.WriteObject(writer, value);
					break;
				case JTokenType.Array:
					this.WriteArray(writer, value);
					break;
				default:
					writer.WriteValue(value);
					break;
			}
		}

		private void WriteObject(JsonWriter writer, object value) {
			writer.WriteStartObject();
			var obj = value as IDictionary<string, object>;
			foreach (var kvp in obj) {
				writer.WritePropertyName(kvp.Key);
				this.WriteValue(writer, kvp.Value);
			}
			writer.WriteEndObject();
		}

		private void WriteArray(JsonWriter writer, object value) {
			writer.WriteStartArray();
			var array = value as IEnumerable<object>;
			foreach (var o in array) {
				this.WriteValue(writer, o);
			}
			writer.WriteEndArray();
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
			return ReadValue(reader);
		}

		private object ReadValue(JsonReader reader) {
			while (reader.TokenType == JsonToken.Comment) {
				if (!reader.Read()) throw new JsonSerializationException("Unexpected Token when converting IDictionary<string, object>");
			}

			switch (reader.TokenType) {
				case JsonToken.StartObject:
					return ReadObject(reader);
				case JsonToken.StartArray:
					return this.ReadArray(reader);
				case JsonToken.Integer:
				case JsonToken.Float:
				case JsonToken.String:
				case JsonToken.Boolean:
				case JsonToken.Undefined:
				case JsonToken.Null:
				case JsonToken.Date:
				case JsonToken.Bytes:
					return reader.Value;
				default:
					throw new JsonSerializationException
						(string.Format("Unexpected token when converting IDictionary<string, object>: {0}", reader.TokenType));
			}
		}

		private object ReadArray(JsonReader reader) {
			IList<object> list = new List<object>();

			while (reader.Read()) {
				switch (reader.TokenType) {
					case JsonToken.Comment:
						break;
					case JsonToken.EndArray:
						return list;
					default:
						var v = ReadValue(reader);

						list.Add(v);
						break;
				}
			}

			throw new JsonSerializationException("Unexpected end when reading IDictionary<string, object>");
		}

		private object ReadObject(JsonReader reader) {
			var obj = new Dictionary<string, object>();

			while (reader.Read()) {
				switch (reader.TokenType) {
					case JsonToken.PropertyName:
						var propertyName = reader.Value.ToString();

						if (!reader.Read()) {
							throw new JsonSerializationException("Unexpected end when reading IDictionary<string, object>");
						}

						var v = ReadValue(reader);

						obj[propertyName] = v;
						break;
					case JsonToken.Comment:
						break;
					case JsonToken.EndObject:
						return obj;
				}
			}

			throw new JsonSerializationException("Unexpected end when reading IDictionary<string, object>");
		}

		public override bool CanConvert(Type objectType) { return typeof(IDictionary<string, object>).IsAssignableFrom(objectType); }
	}
}
