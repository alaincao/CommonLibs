//
// CommonLibs/Web/LongPolling/SyncedHttpHandler.cs
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
