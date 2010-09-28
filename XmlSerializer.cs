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
			public XmlNode[]	objects;
		}

		[Serializable]
		public class XmlNode
		{
			[XmlAttribute]
			public int					id;
			[XmlAttribute]
			public ObjectElement.Types	type;
			[XmlAttribute]
			public string				name_string;
			public TranslatableElement	name_translatable;
			[XmlAttribute("class")]
			public string				class_;
			public object				value_primitive;
			public TranslatableElement	value_translatable;
			[XmlArrayItem("line")]
			public string[]				stack_trace;
			[XmlArrayItem("id")]
			public int[]				children;
		}

		private ObjectElement				Tree;

		#region Used by Explore()

		private Dictionary<int,XmlNode>		VisitedObjects	= null;

		#endregion

		public XmlSerializer(Manager manager)
		{
			Tree = manager.Tree;
		}

		public void Write(System.IO.TextWriter writer)
		{
			// NB: The Tree might contain circular references. So a simple XML serialization is not possible

			System.Diagnostics.Debug.Assert( VisitedObjects == null, "Property 'VisitedObjects' should have been reset at the end of this method" );
			try
			{
				// Get the list of objects contained in the Tree
				VisitedObjects = new Dictionary<int, XmlSerializer.XmlNode>();
				Explore( Tree );

				// Create the XML document
				var objects = new XmlNode[ VisitedObjects.Count ];
				int i=0;
				foreach( var obj in VisitedObjects.Values )
					objects[i++] = obj;
				var root = new XmlRoot { objects = objects };

				var serializer = new System.Xml.Serialization.XmlSerializer( typeof(XmlRoot) );
				serializer.Serialize( writer, root );
			}
			finally
			{
				VisitedObjects = null;
			}
		}

		public static Manager Read(System.IO.TextReader reader, Func<ObjectElement,Manager> createManager)
		{
			var serializer = new System.Xml.Serialization.XmlSerializer( typeof(XmlRoot) );
			var doc = (XmlRoot)serializer.Deserialize( reader );

			// Recreate all objects
			ObjectElement root = null;
			var elements = new Dictionary<int,ObjectElement>();
			foreach( var obj in doc.objects )
			{
				ObjectElement element;
				switch( obj.type )
				{
					case ObjectElement.Types.Root:
						element = ObjectElement.CreateRoot();
						if( root != null )
							throw new ApplicationException( "There is more than 1 root element in the XML document." );
						root = element;
						break;

					case ObjectElement.Types.Exception: {
						string str;
						if( obj.value_translatable != null )
						{
							element = ObjectElement.CreateException( obj.value_translatable, obj.class_, obj.stack_trace );
						}
						else if( (str = obj.value_primitive as string) != null )
						{
							element = ObjectElement.CreateException( str, obj.class_, obj.stack_trace );
						}
						else
						{
							System.Diagnostics.Debug.Fail( "Unsupported element type '" + obj.value_primitive.GetType().FullName + "'" );
							continue;
						}
						break; }

					case ObjectElement.Types.Object:
						if( obj.name_translatable != null )
							element = ObjectElement.CreateObject( obj.name_translatable, obj.class_ );
						else
							element = ObjectElement.CreateObject( obj.name_string, obj.class_ );
						break;

					case ObjectElement.Types.Field:
						if( obj.name_translatable != null )
							element = ObjectElement.CreateField( obj.name_translatable, obj.class_, obj.value_primitive != null ? obj.value_primitive : obj.value_translatable );
						else
							element = ObjectElement.CreateField( obj.name_string, obj.class_, obj.value_primitive != null ? obj.value_primitive : obj.value_translatable );
						break;

					default:
						throw new NotImplementedException( "The object type '" + obj.type.ToString() + "' is not supported" );
				}
				elements.Add( obj.id, element );
			}
			if( root == null )
				throw new ApplicationException( "There is no root element in the XML document." );

			// Recreate parent/child links between objects
			foreach( var obj in doc.objects )
			{
				var parentElement = elements[ obj.id ];
				if( obj.children != null )
					foreach( var childObj in obj.children )
						parentElement.Children.Add( elements[childObj] );
			}

			return createManager( root );
		}

		/// <remarks>Recursive</remarks>
		private void Explore(ObjectElement node)
		{
			System.Diagnostics.Debug.Assert( VisitedObjects != null, "Property 'VisitedElements' is not set" );
			System.Diagnostics.Debug.Assert( node != null, "Parameter 'node' is not set" );
			System.Diagnostics.Debug.Assert( node.Children != null, "Node's children is not set" );

			int id = node.GetHashCode();
			if( VisitedObjects.ContainsKey(id) )
				return;

			int[] children = null;
			if( node.Children.Count > 0 )
			{
				children = new int[ node.Children.Count ];
				for( int i=0; i<node.Children.Count; ++i )
					children[i] = node.Children[i].GetHashCode();
			}

			var name_translatable = node.Name as TranslatableElement;
			var value_translatable = node.Value as TranslatableElement;
			var xmlNode = new XmlNode {
									id = id,
									type = node.Type,
									name_translatable = name_translatable != null ? name_translatable : null,
									name_string = name_translatable == null ? (string)node.Name : null,
									class_ = node.ClassName,
									value_translatable = value_translatable != null ? value_translatable : null,
									value_primitive = value_translatable == null ? node.Value : null,
									stack_trace = node.StackTrace,
									children = children };
			VisitedObjects.Add( node.GetHashCode(), xmlNode );
			foreach( var childNode in node.Children )
				Explore( childNode );
		}

		public string GetString()
		{
			var writer = new System.IO.StringWriter();
			Write( writer );
			return writer.ToString();
		}
	}
}
