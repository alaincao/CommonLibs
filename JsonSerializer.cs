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
