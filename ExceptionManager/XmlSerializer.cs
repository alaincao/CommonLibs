using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace CommonLibs.ExceptionManager
{
	public class XmlSerializer
	{
		[Serializable]
		[XmlRoot(ElementName="exceptions")]
		public class XmlRoot
		{
			public SerializerHelper.SerializableNode[]	objects;
		}

		private ObjectElement				Tree;

		public XmlSerializer(Manager manager)
		{
			Tree = manager.Tree;
		}

		public void Write(System.IO.TextWriter writer)
		{
			var nodes = SerializerHelper.CreateSerializableNodes( Tree );
			var root = new XmlRoot{ objects=nodes };
			var serializer = new System.Xml.Serialization.XmlSerializer( typeof(XmlRoot) );
			serializer.Serialize( writer, root );
		}

		public static Manager Read(System.IO.TextReader reader, Func<ObjectElement,Manager> createManager)
		{
			var serializer = new System.Xml.Serialization.XmlSerializer( typeof(XmlRoot) );
			var doc = (XmlRoot)serializer.Deserialize( reader );
			var root = SerializerHelper.RecreateObjectTree( doc.objects );
			return createManager( root );
		}

		public string GetString()
		{
			var writer = new System.IO.StringWriter();
			Write( writer );
			return writer.ToString();
		}
	}
}
