//
// CommonLibs/ExceptionManager/ObjectElement.cs
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
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace CommonLibs.ExceptionManager
{
	public class TranslatableElement
	{
		private static readonly string		DefaultTextKey		= string.Empty;
		private static readonly object[]	DefaultParameters	= new object[]{};

		public string						TextKey				= DefaultTextKey;
		public object[]						Parameters			{ get { return parameters; } set { parameters = value; System.Diagnostics.Debug.Assert( CheckParameters(value), "Bad assignment to property 'Parameters'" ); } }
		private object[]					parameters			= DefaultParameters;

		private bool CheckParameters(object[] parameters)
		{
			if( parameters == null )
				// Assign an empty list of parameters (e.g. new oject[]{}), instead of null
				return false;

			foreach( var parameter in parameters )
			{
				if( parameter == null )
					// OK: parameter can be null
					continue;
				if(! ObjectExplorer.IsDirectlyInterpretable(parameter.GetType()) )
					// Use only simple objects as parameters (e.g. strings, ints etc...). They must be serializable!
					return false;
			}
			return true;
		}

		public override string ToString()
		{
			return Manager.Translate_DefaultImplementation( TextKey, Parameters );
		}

		public string ToString(Manager manager)
		{
			return manager.Translate( TextKey, Parameters );
		}
	}

	public class ObjectElement
	{
		public enum Types
		{
			Undefined = 0,  // Default
			Root,
			Exception,
			Object,
			Field
		}

		public List<ObjectElement>		Children	= new List<ObjectElement>();

		public Types					Type		= Types.Undefined;
		public string					ClassName	= null;
		public string[]					StackTrace	= null;

		public object					Name		{ get { return name; } set { name = value; System.Diagnostics.Debug.Assert( CheckName(value), "Bad assignment to property 'Name'" ); } }
		private object					name		= null;

		/// <remarks>Assumed never null</remarks>
		public object					Value		{ get { return (value != null) ? value : ObjectExplorer.NullValue; } set { this.value = value; System.Diagnostics.Debug.Assert( CheckValue(value), "Bad assignment to property 'Value'" ); } }
		private object					value		= null;

		internal static ObjectElement CreateRoot()
		{
			return new ObjectElement {
				Type = Types.Root };
		}

		internal static ObjectElement CreateException(string message, string className, string[] stackTrace)
		{
			System.Diagnostics.Debug.Assert( stackTrace != null, "Argument 'stackTrace' is not specified" );
			return new ObjectElement {
				Type = Types.Exception,
				StackTrace = stackTrace,
				Value = message,
				ClassName = className };
		}

		internal static ObjectElement CreateException(TranslatableElement message, string className, string[] stackTrace)
		{
			System.Diagnostics.Debug.Assert( stackTrace != null, "Argument 'stackTrace' is not specified" );
			return new ObjectElement {
				Type = Types.Exception,
				StackTrace = stackTrace,
				Value = message,
				ClassName = className };
		}

		internal static ObjectElement CreateObject(string fieldName, string className)
		{
			return new ObjectElement {
				Type = Types.Object,
				Name = fieldName,
				ClassName = className };
		}

		internal static ObjectElement CreateObject(TranslatableElement fieldName, string className)
		{
			return new ObjectElement {
				Type = Types.Object,
				Name = fieldName,
				ClassName = className };
		}

		internal static ObjectElement CreateField(string fieldName, string className, object value)
		{
			return new ObjectElement {
				Type = Types.Field,
				Name = fieldName,
				ClassName = className,
				Value = value };
		}

		internal static ObjectElement CreateField(TranslatableElement fieldName, string className, object value)
		{
			return new ObjectElement {
				Type = Types.Field,
				Name = fieldName,
				ClassName = className,
				Value = value };
		}

		internal static ObjectElement CloneElement(string name, string className, ObjectElement src)
		{
			return new ObjectElement {
				Type = src.Type,
				Children = src.Children,
				Value = src.Value,

				Name = name,
				ClassName = string.IsNullOrEmpty(src.ClassName) ? className : src.ClassName };
		}

		private bool CheckName(object name)
		{
			if( name == null )
				// OK, Name can be NULL
				return true;
			var type = name.GetType();
			if( type == typeof(string) )
				// Name can only be a string ...
				return true;
			if( type == typeof(TranslatableElement) )
				// ... or a TranslatableElement
				return true;
			return false;
		}

		private bool CheckValue(object value)
		{
			if( value == null )
				// The value is not supposed to be NULL;
				return false;
			var type = value.GetType();
			if( ObjectExplorer.IsDirectlyInterpretable(type) )
				// it can only be directly converted using .ToString()
				return true;
			if( type == typeof(TranslatableElement) )
				// or a translation element
				return true;
			return false;
		}

		public override string ToString()
		{
			switch( Type )
			{
				case Types.Root:
					return "Root node";
				case Types.Exception:
					return "Exception: " + Value.ToString() + " (" + ClassName + ")";
				case Types.Object:
					return "Object: " + Name + " (" + ClassName + ")";
				case Types.Field:
					return "Field " + Name + ": " + Value + " (" + ClassName + ")";
				default:
					System.Diagnostics.Debug.Fail( "The object type '" + Type.ToString() + "' is not supported" );
					return "";
			}
		}
	}
}
