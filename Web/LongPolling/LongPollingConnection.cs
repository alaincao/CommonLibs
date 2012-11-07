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

		internal HttpContext			HttpContext								{ get; private set; }
		private AsyncCallback			AsyncCallback;

		#region For IConnection

		public string					SessionID								{ get; private set; }
		public string					ConnectionID							{ get; private set; }

		#endregion

		internal Message				ResponseMessage							= null;

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
		}

		/// <remarks>Used for fatal exception messages caugth by the LongPollingHandler</remarks>
		internal LongPollingConnection(Message message, HttpContext httpContext, AsyncCallback asyncCallback, object asyncState)
		{
			ASSERT( message != null, "Missing parameter 'message'" );
			ASSERT( httpContext != null, "Missing parameter 'httpContext'" );

			AsyncState = asyncState;

			HttpContext = httpContext;
			AsyncCallback = asyncCallback;

			SendResponseMessageSynchroneously( message );
		}

		/// <remarks>Can only be called from LongPollingHandler</remarks>
		internal void SendResponseMessageSynchroneously(Message responseMessage)
		{
			CompletedSynchronously = true;
			SendResponseMessage( responseMessage );
		}

		/// <remarks>
		/// WARNING: Every calls to this method should be performed outside any lock()s because the call to "AsyncCallback(this)" will try to create a new thread.<br/>
		/// Example dead-lock scenario:<br/>
		/// Under heavy load, all worker threads are busy and they are all waiting for a lock() to liberate.<br/>
		/// One of the worker thread is calling this method from inside of this lock().<br/>
		/// This thread will try to create a new thread from the "AsyncCallback(this)".<br/>
		/// The problem is that "AsyncCallback(this)" will then hang, waiting for a worker slot thread to liberate...
		/// </remarks>
		public void SendResponseMessage(Message responseMessage)
		{
			ASSERT( responseMessage != null, "Missing parameter 'responseMessage'" );

			LOG( "SendResponseMessage(" + responseMessage + ") - Start" );
			try
			{
				ResponseMessage = responseMessage;

				// Declare message as completed
				int oldIsCompleted = Interlocked.Exchange( ref isCompleted, 1 );
				if( oldIsCompleted != 0 )
					throw new InvalidOperationException( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "() can only be called once" );

				// Invoke callback that will invoke LongPollingHandler.EndProcessRequest()
				LOG( "SendResponseMessage(" + responseMessage + ") - AsyncCallback is " + (AsyncCallback != null ? "not null" : "null") );
				if( AsyncCallback != null )
					AsyncCallback( this );

				LOG( "SendResponseMessage(" + responseMessage + ") - Exit" );
			}
			catch( System.Exception ex )
			{
				LOG( "*** Error while terminating the HTTP request - Could not invoke the request's callback (" + ex.GetType().FullName + "): " + ex.Message );
			}
		}
	}
}
