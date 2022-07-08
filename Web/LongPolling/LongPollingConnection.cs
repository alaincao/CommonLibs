//
// CommonLibs/Web/LongPolling/LongPollingConnection.cs
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
