//
// CommonLibs/ExceptionManager/ObjectElement.cs
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
