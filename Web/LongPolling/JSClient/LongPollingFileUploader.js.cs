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
