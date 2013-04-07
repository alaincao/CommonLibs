//
// CommonLibs/Web/LongPolling/JSClient/LongPollingFileUploader.cs
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
	- Include the 'LongPollingFileUploader.js' file as embedded resource
	- Add this line to the "AssemblyInfo.cs" of the DLL that contains this file (i.e. this DLL) to enable web resources:
		[assembly: System.Web.UI.WebResource( "<<Your DLL name>>."+CommonLibs.Web.LongPolling.JSClient.JSUploaderPath, "text/javascript" )]
*/

	public static partial class JSClient
	{
		public const string						JSUploaderPath					= "CommonLibs.Web.LongPolling.JSClient.LongPollingFileUploader.js";
		public static string					JSUploaderPathFull				{ get { return jsUploaderPathFull ?? (jsUploaderPathFull = typeof(JSClient).Assembly.GetName().Name + "." + JSUploaderPath); } }
		private static string					jsUploaderPathFull				= null;

		/// <remarks>JQuery JavaScript files must be included before this script declaration (i.e. in the page's 'head' tag)</remarks>
		public static string CreateJSUploaderBlock(Page page)
		{
			var path = page.ClientScript.GetWebResourceUrl( typeof(JSClient), JSUploaderPathFull );
			var script = string.Format( "<script type='text/javascript' src='{0}'></script>", path.EscapeQuotes() );
			return script;
		}
	}
}
