//
// CommonLibs/ExceptionManager/JsonSerializer.cs
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
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace CommonLibs.ExceptionManager
{
	public class JsonSerializer
	{
		private readonly ObjectElement		Tree;

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
