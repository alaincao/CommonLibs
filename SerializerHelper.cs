using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace CommonLibs.ExceptionManager
{
	/// <summary>
	/// Utility class to create a serializable version of the ObjectEment tree
	/// </summary>
	/// <remarks>The Tree might contain circular references. So a simple serialization is not possible</remarks>
	public class SerializerHelper
	{
		/// <remarks>JSON is unable to deserialize an 'object' to its correct type => Using all the Value* to store the object correctly typed</remarks>
		[Serializable]  // Used by XmlSerializer ([XmlElement] & [XmlAttribute] & [XmlIgnore])
		[DataContract]  // Used by JsonSerializer ([DataMember])
		public class SerializablePrimitive
		{
			// This one is not serialized:
			[XmlIgnore]
			public object		Value			{ get { return GetValue( false ); } set { SetValue( value, false ); } }

			// This one is not serialized in XML but not in JSON:
			[XmlElement(ElementName="Value")]
			public object		ValueXml		{ get { return GetValue( true ); } set { SetValue( value, true ); } }

			// These ones are not serialized in XML but are in JSON:
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public bool?		ValueBool		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public byte?		ValueByte		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public char?		ValueChar		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public short?	ValueShort		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public int?		ValueInt		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public long?		ValueLong		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public ushort?	ValueUShort		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public uint?		ValueUInt		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public ulong?	ValueULong		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public float?	ValueFloat		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public double?	ValueDouble		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public decimal?	ValueDecimal	= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public string	ValueString		= null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public DateTime?	ValueDatetime	= null;

			public SerializablePrimitive()  {}
			public SerializablePrimitive(object value)  { SetValue( value, false ); }

			private void SetValue(object value, bool urlEncodedString)
			{
				ValueBool		= null;
				ValueByte		= null;
				ValueChar		= null;
				ValueShort		= null;
				ValueInt		= null;
				ValueLong		= null;
				ValueUShort		= null;
				ValueUInt		= null;
				ValueULong		= null;
				ValueFloat		= null;
				ValueDouble		= null;
				ValueDecimal	= null;
				ValueString		= null;
				ValueDatetime	= null;

				if( value == null )  return;
				else if( (ValueString	= value as string	) != null )
				{
					if( urlEncodedString )
						// XML serializer has problems serializing some strings (e.g. "\n", "\r\n") => Serializing an URL-encoded version of the string...
						ValueString = System.Web.HttpUtility.UrlDecode( ValueString );
					return;
				}
				else if( (ValueBool		= value as bool?	).HasValue )	return;
				else if( (ValueByte		= value as byte?	).HasValue )	return;
				else if( (ValueChar		= value as char?	).HasValue )	return;
				else if( (ValueShort	= value as short?	).HasValue )	return;
				else if( (ValueInt		= value as int?		).HasValue )	return;
				else if( (ValueLong		= value as long?	).HasValue )	return;
				else if( (ValueUShort	= value as ushort?	).HasValue )	return;
				else if( (ValueUInt		= value as uint?	).HasValue )	return;
				else if( (ValueULong	= value as ulong?	).HasValue )	return;
				else if( (ValueFloat	= value as float?	).HasValue )	return;
				else if( (ValueDouble	= value as double?	).HasValue )	return;
				else if( (ValueDecimal	= value as decimal?	).HasValue )	return;
				else if( (ValueDatetime	= value as DateTime?).HasValue )	return;
				else
				{
					System.Diagnostics.Debug.Fail( "Unsupported primitive type '" + value.GetType().FullName + "'" );
					ValueString = value.ToString();  // Fallback, but might crash somewhere else...
					return;
				}
			}

			private object GetValue(bool urlEncodedString)
			{
				if( ValueString != null )
				{
					if( urlEncodedString )
						// XML serializer has problems serializing some strings (e.g. "\n", "\r\n") => Serializing an URL-encoded version of the string...
						return System.Web.HttpUtility.UrlEncode( ValueString );
					else
						return ValueString;
				}
				else
				{
					return	ValueBool		??
							ValueByte		??
							ValueChar		??
							ValueShort		??
							ValueInt		??
							ValueLong		??
							ValueUShort		??
							ValueUInt		??
							ValueULong		??
							ValueFloat		??
							ValueDouble		??
							ValueDecimal	??
							(object)ValueDatetime;
				}
			}
		}

		[Serializable]  // Used by XmlSerializer ([XmlAttribute] & [XmlIgnore])
		[DataContract]  // Used by JsonSerializer ([DataMember])
		public class SerializableTranslatable
		{
			[XmlAttribute]
			[DataMember]
			public string						Key				= null;
			[DataMember]
			public SerializablePrimitive[]		Parameters		= null;

			public SerializableTranslatable()
			{
			}

			public SerializableTranslatable(TranslatableElement translatable)
			{
				Key = translatable.TextKey;
				if( translatable.Parameters != null )
				{
					Parameters = new SerializablePrimitive[ translatable.Parameters.Length ];
					for( int i=0; i<Parameters.Length; ++i )
						Parameters[ i ] = new SerializablePrimitive( translatable.Parameters[i] );
				}
			}

			public TranslatableElement CreateTranslatableElement()
			{
				var translatable = new TranslatableElement{ TextKey = Key };
				if( Parameters != null )
				{
					var parameters = new object[ Parameters.Length ];
					for( int i=0; i<Parameters.Length; ++i )
						parameters[ i ] = Parameters[ i ].Value;
					translatable.Parameters = parameters;
				}
				return translatable;
			}
		}

		[Serializable]  // Used by XmlSerializer ([XmlAttribute] & [XmlIgnore])
		[DataContract]  // Used by JsonSerializer ([DataMember])
		public class SerializableNode
		{
			[XmlAttribute]
			[DataMember]
			public int								ID;

			[XmlAttribute]
			[DataMember]
			public ObjectElement.Types				Type;

			[XmlAttribute]
			[DataMember(EmitDefaultValue=false)]
			public string							NameString;

			[DataMember(EmitDefaultValue=false)]
			public SerializableTranslatable			NameTranslatable;

			[XmlAttribute]
			[DataMember(EmitDefaultValue=false)]
			public string							Class;

			[DataMember(EmitDefaultValue=false)]
			public SerializablePrimitive			ValuePrimitive;

			[DataMember(EmitDefaultValue=false)]
			public SerializableTranslatable			ValueTranslatable;

			[XmlArrayItem("line")]
			[DataMember(EmitDefaultValue=false)]
			public string[]							StackTrace;

			[XmlArrayItem("id")]
			[DataMember(EmitDefaultValue=false)]
			public int[]							Children;
		}

		private Dictionary<int,SerializableNode>		VisitedObjects		= new Dictionary<int,SerializableNode>();

		private SerializerHelper()
		{
		}

		public static SerializableNode[] CreateSerializableNodes(ObjectElement tree)
		{
			var helper = new SerializerHelper();

			// Get the list of objects contained in the Tree
			helper.Explore( tree );

			// Create the list of nodes
			var objects = new SerializableNode[ helper.VisitedObjects.Count ];
			int i=0;
			foreach( var obj in helper.VisitedObjects.Values )
				objects[i++] = obj;

			return objects;
		}

		/// <returns>The root element of the tree</returns>
		public static ObjectElement RecreateObjectTree(SerializableNode[] nodes)
		{
			// Recreate all objects
			ObjectElement root = null;
			var elements = new Dictionary<int,ObjectElement>();
			foreach( var obj in nodes )
			{
				ObjectElement element;
				switch( obj.Type )
				{
					case ObjectElement.Types.Root:
						element = ObjectElement.CreateRoot();
						if( root != null )
							throw new ApplicationException( "There is more than 1 root element in the XML document." );
						root = element;
						break;

					case ObjectElement.Types.Exception: {
						string str;
						if( obj.ValueTranslatable != null )
						{
							element = ObjectElement.CreateException( obj.ValueTranslatable.CreateTranslatableElement(), obj.Class, obj.StackTrace );
						}
						else if( (str = obj.ValuePrimitive.Value as string) != null )
						{
							element = ObjectElement.CreateException( str, obj.Class, obj.StackTrace );
						}
						else
						{
							System.Diagnostics.Debug.Fail( "Unsupported element type '" + obj.ValuePrimitive.Value.GetType().FullName + "'" );
							continue;
						}
						break; }

					case ObjectElement.Types.Object:
						if( obj.NameTranslatable != null )
							element = ObjectElement.CreateObject( obj.NameTranslatable.CreateTranslatableElement(), obj.Class );
						else
							element = ObjectElement.CreateObject( obj.NameString, obj.Class );
						break;

					case ObjectElement.Types.Field:
						if( obj.NameTranslatable != null )
							element = ObjectElement.CreateField( obj.NameTranslatable.CreateTranslatableElement(), obj.Class, obj.ValuePrimitive != null ? obj.ValuePrimitive.Value : obj.ValueTranslatable );
						else
							element = ObjectElement.CreateField( obj.NameString, obj.Class, obj.ValuePrimitive != null ? obj.ValuePrimitive.Value : obj.ValueTranslatable );
						break;

					default:
						throw new NotImplementedException( "The object type '" + obj.Type.ToString() + "' is not supported" );
				}
				elements.Add( obj.ID, element );
			}
			if( root == null )
				throw new ApplicationException( "There is no root element in the XML document." );

			// Recreate parent/child links between objects
			foreach( var obj in nodes )
			{
				var parentElement = elements[ obj.ID ];
				if( obj.Children != null )
					foreach( var childObj in obj.Children )
						parentElement.Children.Add( elements[childObj] );
			}

			return root;
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
			var xmlNode = new SerializableNode {
									ID = id,
									Type = node.Type,
									NameTranslatable = name_translatable != null ? new SerializableTranslatable(name_translatable) : null,
									NameString = name_translatable == null ? (string)node.Name : null,
									Class = node.ClassName,
									ValueTranslatable = value_translatable != null ? new SerializableTranslatable(value_translatable) : null,
									ValuePrimitive = value_translatable == null ? new SerializablePrimitive(node.Value) : null,
									StackTrace = node.StackTrace,
									Children = children };
			VisitedObjects.Add( node.GetHashCode(), xmlNode );
			foreach( var childNode in node.Children )
				Explore( childNode );
		}
	}
}
