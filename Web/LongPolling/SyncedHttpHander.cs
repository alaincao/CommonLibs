using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.SessionState;

namespace CommonLibs.Web.LongPolling
{
	public abstract class SyncedHttpHandler : IHttpHandler, IReadOnlySessionState
	{
		public interface ISyncedRequestHandler
		{
			void Get(SyncedHttpHandler handler, string connectionID, HttpContext context);
			void Post(SyncedHttpHandler handler, string connectionID, HttpContext context);
		}

		public bool									IsReusable					{ get { return true; } }

		public const string							RequestParmType				= "Type";
		public const string							RequestParmConnectionID		= "ConnectionID";

		public abstract ConnectionList				ConnectionList				{ get; }

		public abstract ISyncedRequestHandler GetHandler(HttpContext context, string handlerType, string sessionID, string connectionID);

		public void ProcessRequest(HttpContext context)
		{
			var sessionID = ConnectionList.GetSessionID( context );
			var connectionID = context.Request.QueryString[ RequestParmConnectionID ];
			if(! ConnectionList.CheckConnectionIsValid(sessionID, connectionID) )
				// Security check
				throw new ArgumentException( "Invalid SessionID/ConnectionID: " + sessionID + "/" + connectionID );

			var handlerType = context.Request.QueryString[ RequestParmType ];
			var handler = GetHandler( context, handlerType, sessionID, connectionID );

			switch( context.Request.HttpMethod )
			{
				case "GET":
					handler.Get( this, connectionID, context );
					break;
				case "POST":
					handler.Post( this, connectionID, context );
					break;
				default:
					throw new NotImplementedException( "Unknown request type '" + context.Request.HttpMethod + "'" );
			}
			context.Response.End();
		}
	}
}
