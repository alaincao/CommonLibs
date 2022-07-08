//
// CommonLibs/ExceptionManager/HtmlWriter.cs
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
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.Web;

namespace CommonLibs.ExceptionManager
{
	public class HtmlWriter
	{
		public string					CssRoot			{ get; set; } = "root";
		public string					CssException	{ get; set; } = "exception";
		public string					CssStackTrace	{ get; set; } = "stack";
		public string					CssObject		{ get; set; } = "object";
		public string					CssField		{ get; set; } = "field";

		private readonly Manager		Manager;
		private ObjectElement			Tree			{ get { return Manager.Tree; } }

		#region Used by Explore

		private const int				MaxDepth		= ObjectExplorer.MaximumDepth;
		private System.IO.TextWriter	Writer			= null;

		#endregion

		public HtmlWriter(Manager manager)
		{
			Manager = manager;
		}

		public static string GetHtml(Manager manager, string rootNodeID)
		{
			var stringWriter = new StringWriter();
			var htmlWriter = new HtmlWriter( manager );
			htmlWriter.Write( stringWriter, rootNodeID );
			return stringWriter.ToString();
		}

		public void Write(System.IO.TextWriter writer, string rootNodeID)
		{
			System.Diagnostics.Debug.Assert( Manager != null && Tree != null, "'Tree' is not supposed to be null" );
			System.Diagnostics.Debug.Assert( Writer == null, "'Writer' property should have been reset at the end of the method" );

			try
			{
				Writer = writer;

				Writer.Write( "<div id='" + rootNodeID + "' class='" + CssRoot + "'>" );
				foreach( var element in Tree.Children )
					Explore( element, MaxDepth );
				Writer.Write( "</div>" );
			}
			finally
			{
				Writer = null;
			}
		}

		/// <remarks>Recursive</remarks>
		private void Explore(ObjectElement element, int depth)
		{
			System.Diagnostics.Debug.Assert( Writer != null, "Property 'Writer' is not set"  );
			if( depth <= 0 )
				return;

			bool closeDiv;
			switch( element.Type )
			{
				case ObjectElement.Types.Field: {
					closeDiv = false;

					string name = HttpUtility.HtmlEncode( CommonLibs.ExceptionManager.TextWriter.GetElementName(element.Name, Manager) );
					string value = HttpUtility.HtmlEncode( CommonLibs.ExceptionManager.TextWriter.GetElementValue(element.Value, Manager) );
					string className = HttpUtility.HtmlEncode( element.ClassName );
					Writer.Write( "<div class='" + CssField + "'><span>" + name + "</span><span>" + value + "</span><span>" + className + "</span></div>" );
					break; }

				case ObjectElement.Types.Exception: {
					closeDiv = true;

					string message = HttpUtility.HtmlEncode( CommonLibs.ExceptionManager.TextWriter.GetElementValue(element.Value, Manager) );
					string className = HttpUtility.HtmlEncode( element.ClassName );
					Writer.Write( "<div class='" + CssException + "'><div><span>" + message + "</span><span>" + className + "</span></div>" );
					break; }

				case ObjectElement.Types.Object: {
					closeDiv = true;

					string name = HttpUtility.HtmlEncode( CommonLibs.ExceptionManager.TextWriter.GetElementName(element.Name, Manager) );
					string className = HttpUtility.HtmlEncode( element.ClassName );
					Writer.Write( "<div class='" + CssObject + "'><div><span>" + name + "</span><span>" + className + "</span></div>" );
					break; }

				default:
					throw new NotImplementedException( "Element type '" + element.Type.ToString() + "' is not supported" );
			}

			--depth;
			foreach( var child in element.Children )
				Explore( child, depth );

			if( element.StackTrace != null )
			{
				Writer.Write( "<div class='" + CssStackTrace + "'>" );
				foreach( string line in element.StackTrace )
					Writer.Write( "<span>" + line + "</span>" );
				Writer.Write( "</div>" );
			}

			if( closeDiv )
				Writer.Write( "</div>" );
		}
	}
}
