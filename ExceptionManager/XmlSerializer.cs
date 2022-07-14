//
// CommonLibs/ExceptionManager/XmlSerializer.cs
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
			public SerializerHelper.SerializableNode[]	objects	{ get; set; }
		}

		private readonly ObjectElement		Tree;

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
