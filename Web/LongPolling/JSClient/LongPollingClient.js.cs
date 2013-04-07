//
// CommonLibs/Web/LongPolling/JSClient/LongPollingClient.js.cs
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
using System.Linq;
using System.Web;
using System.Web.UI;

using CommonLibs.Utils;

namespace CommonLibs.Web.LongPolling
{
/*
	- Include the 'LongPollingClient.js' file as embedded resource
	- Add this line to the "AssemblyInfo.cs" of the DLL that contains this file (i.e. this DLL) to enable web resources:
		[assembly: System.Web.UI.WebResource( "<<Your DLL name>>."+CommonLibs.Web.LongPolling.JSClient.JSClientPath, "text/javascript" )]
*/

	public static partial class JSClient
	{
		public const string						JSClientPath					= "CommonLibs.Web.LongPolling.JSClient.LongPollingClient.js";
		public static string					JSClientPathFull				{ get { return jsClientPathFull ?? (jsClientPathFull = typeof(JSClient).Assembly.GetName().Name + "." + JSClientPath); } }
		private static string					jsClientPathFull				= null;

		/// <remarks>JQuery JavaScript files must be included before this script declaration (i.e. in the page's 'head' tag)</remarks>
		public static string CreateJSClientInitializationBlock(Page page, string jsObjectName, string longPollingHandlerUrl, string longPollingSyncedHandlerUrl, string logoutUrl, bool startDirectly=false)
		{
			//page.ClientScript.RegisterClientScriptInclude( "b9aa3021-1492-6bab-31e0-03caf8ca46d3", path );  <= Not soon enough in the declarations (inside the <body> tag ; We want it in the <head> tag)

			// Include JS file
			var path = page.ClientScript.GetWebResourceUrl( typeof(JSClient), JSClientPathFull );
			var script = "<script type='text/javascript' src='"+path.EscapeQuotes()+"'></script>\n";

			if( jsObjectName != null )
				// Create JS message handler
				script += "<script type='text/javascript'>\n"
						+ "window."+jsObjectName+" = new LongPollingClient('"+longPollingHandlerUrl.EscapeQuotes()+"','"+longPollingSyncedHandlerUrl.EscapeQuotes()+"','"+logoutUrl.EscapeQuotes()+"');\n"
						+ (startDirectly ? (jsObjectName+".start();\n") : "" )  // Start if requested
						+ "</script>\n";

			return script;
		}
	}
}
