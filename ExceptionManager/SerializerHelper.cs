//
// CommonLibs/ExceptionManager/SerializerHelper.cs
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
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public bool?		ValueBool		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public byte?		ValueByte		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public char?		ValueChar		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public short?	ValueShort		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public int?		ValueInt		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public long?		ValueLong		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public ushort?	ValueUShort		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public uint?		ValueUInt		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public ulong?	ValueULong		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public float?	ValueFloat		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public double?	ValueDouble		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public decimal?	ValueDecimal	{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public string	ValueString		{ get; set; } = null;
			[XmlIgnore][DataMember(EmitDefaultValue=false)]public DateTime?	ValueDatetime	{ get; set; } = null;

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

				if( value == null )
					return;

				ValueString	= value as string;
				if( ValueString != null )
				{
					if( urlEncodedString )
						// XML serializer has problems serializing some strings (e.g. "\n", "\r\n") => Serializing an URL-encoded version of the string...
						ValueString = System.Web.HttpUtility.UrlDecode( ValueString );
					return;
				}
				ValueBool		= value as bool?;		if( ValueBool.HasValue )		return;
				ValueByte		= value as byte?;		if( ValueByte.HasValue )		return;
				ValueChar		= value as char?;		if( ValueChar.HasValue )		return;
				ValueShort		= value as short?;		if( ValueShort.HasValue )		return;
				ValueInt		= value as int?;		if( ValueInt.HasValue )			return;
				ValueLong		= value as long?;		if( ValueLong.HasValue )		return;
				ValueUShort		= value as ushort?;		if( ValueUShort.HasValue )		return;
				ValueUInt		= value as uint?;		if( ValueUInt.HasValue )		return;
				ValueULong		= value as ulong?;		if( ValueULong.HasValue )		return;
				ValueFloat		= value as float?;		if( ValueFloat.HasValue )		return;
				ValueDouble		= value as double?;		if( ValueDouble.HasValue )		return;
				ValueDecimal	= value as decimal?;	if( ValueDecimal.HasValue )		return;
				ValueDatetime	= value as DateTime?;	if( ValueDatetime.HasValue )	return;

				System.Diagnostics.Debug.Fail( "Unsupported primitive type '" + value.GetType().FullName + "'" );
				ValueString = value.ToString();  // Fallback, but might crash somewhere else...
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
			public string						Key				{ get; set; } = null;
			[DataMember]
			public SerializablePrimitive[]		Parameters		{ get; set; } = null;

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
			public int								ID					{ get; set; }

			[XmlAttribute]
			[DataMember]
			public ObjectElement.Types				Type				{ get; set; }

			[XmlAttribute]
			[DataMember(EmitDefaultValue=false)]
			public string							NameString			{ get; set; }

			[DataMember(EmitDefaultValue=false)]
			public SerializableTranslatable			NameTranslatable	{ get; set; }

			[XmlAttribute]
			[DataMember(EmitDefaultValue=false)]
			public string							Class				{ get; set; }

			[DataMember(EmitDefaultValue=false)]
			public SerializablePrimitive			ValuePrimitive		{ get; set; }

			[DataMember(EmitDefaultValue=false)]
			public SerializableTranslatable			ValueTranslatable	{ get; set; }

			[XmlArrayItem("line")]
			[DataMember(EmitDefaultValue=false)]
			public string[]							StackTrace			{ get; set; }

			[XmlArrayItem("id")]
			[DataMember(EmitDefaultValue=false)]
			public int[]							Children			{ get; set; }
		}

		private readonly Dictionary<int,SerializableNode>	VisitedObjects	= new Dictionary<int,SerializableNode>();

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
							throw new CommonException( "There is more than 1 root element in the XML document." );
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
				throw new CommonException( "There is no root element in the XML document." );

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
