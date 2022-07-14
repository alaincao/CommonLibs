//
// CommonLibs/Web/LongPolling/LongPollingConnection.cs
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
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace CommonLibs.Web.LongPolling
{
	internal class LongPollingConnection : IAsyncResult, IConnection
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		#region For IAsyncResult

		public bool						IsCompleted								{ get { return (isCompleted != 0); } }
		public int						isCompleted								= 0;
		public bool						CompletedSynchronously					{ get; set; }
		public WaitHandle				AsyncWaitHandle							{ get { throw new NotImplementedException( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "()" ); } }
		public object					AsyncState								{ get; private set; }

		#endregion

		internal bool					RegisteredInConnectionList				= false;
		internal HttpContext			HttpContext								{ get; private set; }
		private readonly AsyncCallback	AsyncCallback;

		#region For IConnection

		public string					SessionID								{ get; private set; }
		public string					ConnectionID							{ get; private set; }
		public bool						Sending									{ get; set; }

		#endregion

		internal RootMessage			ResponseMessage							= null;

		internal LongPollingConnection(string sessionID, string connectionID, HttpContext httpContext, AsyncCallback asyncCallback, object asyncState)
		{
			ASSERT( httpContext != null, "Missing parameter 'httpContext'" );
			ASSERT( !string.IsNullOrEmpty(sessionID), "Missing parameter 'sessionID'" );
			//ASSERT( !string.IsNullOrEmpty(connectionID), "Missing parameter 'connectionID'" );		<= Can be null for logout messages
			//ASSERT( asyncCallback != null, "Missing parameter 'asyncCallback'" );		<= Managed by the system
			//ASSERT( asyncState != null, "Missing parameter 'asyncState'" );		<= Managed by the system

			AsyncState = asyncState;

			HttpContext = httpContext;
			AsyncCallback = asyncCallback;

			SessionID = sessionID;
			ConnectionID = connectionID;
			Sending = false;
		}

		/// <remarks>
		/// WARNING: Every calls to this method should be performed outside any lock()s because the call to "AsyncCallback(this)" will try to create a new thread.<br/>
		/// Example dead-lock scenario:<br/>
		/// Under heavy load, all worker threads are busy and they are all waiting for a lock() to liberate.<br/>
		/// One of the worker thread is calling this method from inside of this lock().<br/>
		/// This thread will try to create a new thread from the "AsyncCallback(this)".<br/>
		/// The problem is that "AsyncCallback(this)" will then hang, waiting for a worker slot thread to liberate...
		/// </remarks>
		public void SendRootMessage(RootMessage rootMessage)
		{
			ASSERT( rootMessage != null, $"Missing parameter '{nameof(rootMessage)}'" );

			LOG( $"SendResponseMessage() - Start" );
			try
			{
				ResponseMessage = rootMessage;

				// Declare message as completed
				int oldIsCompleted = Interlocked.Exchange( ref isCompleted, 1 );
				if( oldIsCompleted != 0 )
					throw new InvalidOperationException( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "() can only be called once" );

				// Invoke callback that will invoke LongPollingHandler.EndProcessRequest()
				LOG( "SendResponseMessage() - AsyncCallback is " + (AsyncCallback != null ? "not null" : "null") );
				if( AsyncCallback != null )
					AsyncCallback( this );

				LOG( "SendResponseMessage() - Exit" );
			}
			catch( System.Exception ex )
			{
				LOG( "*** Error while terminating the HTTP request - Could not invoke the request's callback (" + ex.GetType().FullName + "): " + ex.Message );
			}
		}

		public void Close(RootMessage rootMessage)
		{
			// NB: Sending a message automatically close the connection ...
			SendRootMessage( rootMessage );
		}
	}
}
