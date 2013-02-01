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
			//page.ClientScript.RegisterClientScriptInclude( "b9aa3021-1492-6bab-31e0-03caf8ca46d3", path );  <= Not soon enough in the declarations (inside the 'body' tag ; We want it in the 'head' tag)

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
