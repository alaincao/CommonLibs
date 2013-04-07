//
// CommonLibs/ExceptionManager/XmlSerializer.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2013 Alain CAO
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
