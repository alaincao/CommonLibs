//
// CommonLibs/Web/LongPolling/SyncedHttpHandler.cs
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
using System.Linq;
using System.Text;
using System.Web;
using System.Web.SessionState;

namespace CommonLibs.Web.LongPolling
{
	public abstract class SyncedHttpHandler : IHttpHandler, IReadOnlySessionState
	{
		[System.Diagnostics.Conditional("DEBUG")] protected void LOG(string message)				{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected void FAIL(string message)				{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		public interface ISyncedRequestHandler
		{
			void Get(string connectionID, HttpContext context);
			void Post(string connectionID, HttpContext context);
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
					handler.Get( connectionID, context );
					break;
				case "POST":
					handler.Post( connectionID, context );
					break;
				default:
					throw new NotImplementedException( "Unknown request type '" + context.Request.HttpMethod + "'" );
			}
			context.Response.End();
		}
	}
}
