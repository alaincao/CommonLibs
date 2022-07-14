//
// CommonLibs/ExceptionManager/JsonSerializer.cs
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
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace CommonLibs.ExceptionManager
{
	public class JsonSerializer
	{
		private ObjectElement				Tree;

		public JsonSerializer(Manager manager)
		{
			Tree = manager.Tree;
		}

		public void Write(Stream stream)
		{
			var nodes = SerializerHelper.CreateSerializableNodes( Tree );
			var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer( typeof(CommonLibs.ExceptionManager.SerializerHelper.SerializableNode[]) );
			serializer.WriteObject( stream, nodes );
		}

		public static Manager Read(string str, Func<ObjectElement,Manager> createManager)
		{
			var stream = new MemoryStream( new UTF8Encoding().GetBytes(str) );
			return Read( stream, createManager );
		}

		public static Manager Read(Stream stream, Func<ObjectElement,Manager> createManager)
		{
			var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer( typeof(CommonLibs.ExceptionManager.SerializerHelper.SerializableNode[]) );
			var nodes = (SerializerHelper.SerializableNode[])serializer.ReadObject( stream );
			var root = SerializerHelper.RecreateObjectTree( nodes );
			return createManager( root );
		}

		public string GetString()
		{
			var memoryStream = new MemoryStream();
			Write( memoryStream );
			memoryStream.Position = 0;
			return (new StreamReader(memoryStream)).ReadToEnd();
		}
	}
}
