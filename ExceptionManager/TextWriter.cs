//
// CommonLibs/ExceptionManager/TextWriter.cs
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
using System.Text;

namespace CommonLibs.ExceptionManager
{
	public class TextWriter
	{
		private Manager					Manager;
		private ObjectElement			Tree			{ get { return Manager.Tree; } }
		internal int					MaximumDepth	= 5;

		#region Used by Write()

		private string					Indentation;
		private System.IO.TextWriter	Writer			= null;

		#endregion

		public TextWriter(Manager manager)
		{
			Manager = manager;
		}

		public static string GetText(Manager manager)
		{
			var textWriter = new CommonLibs.ExceptionManager.TextWriter( manager );
			return textWriter.GetString();
		}

		public void Write(System.IO.TextWriter writer)
		{
			Write( writer, "\t" );
		}

		public void Write(System.IO.TextWriter writer, string indentation)
		{
			System.Diagnostics.Debug.Assert( Writer == null, "Property 'Writer' should have been reset at exit of Write()" );
			System.Diagnostics.Debug.Assert( Tree != null && Tree.Type == ObjectElement.Types.Root, "" );

			try
			{
				Writer = writer;
				Indentation = indentation;
				foreach( var node in Tree.Children )
					Write( node, "", 0 );
			}
			finally
			{
				Writer = null;
			}
		}

		/// <remarks>Recursive</remarks>
		private void Write(ObjectElement node, string indentation, int currentDepth)
		{
			switch( node.Type )
			{
				case ObjectElement.Types.Exception:
					Writer.WriteLine( string.Format("{0}{1} ({2})", indentation, GetElementValue(node.Value, Manager), node.ClassName) );
					break;
				case ObjectElement.Types.Object:
						Writer.WriteLine( string.Format("{0}{1} ({2}):", indentation, GetElementName(node.Name, Manager), node.ClassName) );
					break;
				case ObjectElement.Types.Field:
					Writer.WriteLine( string.Format("{0}{1}: {2} ({3})", indentation,  GetElementName(node.Name, Manager), GetElementValue(node.Value, Manager), node.ClassName) );
					break;
				default:
					throw new NotImplementedException( "Element type '" + node.Type.ToString() + "' is not supported" );
			}

			indentation += Indentation;
			int childDepth = currentDepth+1;
			if( childDepth < MaximumDepth )
			{
				foreach( var childNode in node.Children )
				{
					Write( childNode, indentation, childDepth );
				}
			}

			switch( node.Type )
			{
				case ObjectElement.Types.Exception:
					Writer.WriteLine();
					foreach( string line in node.StackTrace )
						Writer.WriteLine( line );
					Writer.WriteLine();
					break;
			}
		}

		public string GetString()
		{
			var writer = new System.IO.StringWriter();
			Write( writer, "\t" );
			return writer.ToString();
		}

		internal static string GetElementValue(object value, Manager manager)
		{
			System.Diagnostics.Debug.Assert( value != null, "The 'Value' property of a node is not supposed to be null" );
			var valueTranslatable = value as TranslatableElement;
			if( valueTranslatable != null )
				return valueTranslatable.ToString( manager );
			else
				return value.ToString();
		}

		internal static string GetElementName(object nodeName, Manager manager)
		{
			System.Diagnostics.Debug.Assert( nodeName != null, "The 'Name' property of a node of type 'object' or 'field' is not supposed to be null." );
			System.Diagnostics.Debug.Assert( (nodeName.GetType() == typeof(string)) || (nodeName.GetType() == typeof(TranslatableElement)), "The 'Name' property is of an unknown type." );

			var translatable = nodeName as TranslatableElement;
			if( translatable != null )
				return translatable.ToString( manager );
			else
				return nodeName.ToString();
		}
	}
}
