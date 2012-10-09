using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Web;
using System.Web.SessionState;

using CommonLibs.Web.LongPolling.Utils;

namespace CommonLibs.Web.LongPolling
{
	public class LongPollingHandler : IHttpAsyncHandler, /*IRequiresSessionState*/IReadOnlySessionState, IAsyncResult, IConnection
	{
		public static ConnectionList	ConnectionList;
		public static MessageHandler	MessageHandler;

		#region For IHttpAsyncHandler

		public bool						IsReusable								{ get { return false; } }

		#endregion

		#region For IAsyncResult

		public object					AsyncState								{ get; private set; }
		public WaitHandle				AsyncWaitHandle							{ get { throw new NotImplementedException( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "()" ); } }
		public bool						CompletedSynchronously					{ get; private set; }
		public bool						IsCompleted								{ get; private set; }

		private AsyncCallback			Callback;

		#endregion

		#region For IConnection

		public string					SessionID								{ get; set; }
		public string					ConnectionID							{ get; set; }

		#endregion

		private const string			RequestTypeKey							= "type";
		private const string			RequestTypeInit							= "init";
		private const string			RequestTypePoll							= "poll";
		private const string			RequestTypeMessages						= "messages";

		private const string			RequestConnectionIDKey					= "connection";
		private const string			RequestMessagesListKey					= "contents";
		private const string			RequestMessageContentKey				= "content";
		//private const string			RequestRequestIDKey						= "request";

		private const string			ResponseTypeKey							= RequestTypeKey;
		private const string			ResponseTypeInit						= RequestTypeInit;
		private const string			ResponseTypeMessages					= RequestTypeMessages;
		private const string			ResponseTypeException					= "exception";
		private const string			ResponseTypeReset						= "reset";
		private const string			ResponseTypeLogout						= "logout";

		private const string			ResponseMessagesListKey					= RequestMessagesListKey;
		private const string			ResponseExceptionContentKey				= RequestMessageContentKey;
		private const string			ResponseConnectionIDKey					= RequestConnectionIDKey;
		//private const string			ResponseRequestIDKey					= RequestRequestIDKey;

		private HttpContext				HttpContext;

		public LongPollingHandler()
		{
			CompletedSynchronously = false;
		}

		public void ProcessRequest(HttpContext context)
		{
			System.Diagnostics.Debug.Fail( "Should not be used by IHttpAsyncHandler: " + GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "()" );
		}

		IAsyncResult IHttpAsyncHandler.BeginProcessRequest(HttpContext context, AsyncCallback callback, object extraData)
		{
			System.Diagnostics.Trace.WriteLine( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "() - started" );

			try
			{
				HttpContext = context;
				Callback = callback;
				AsyncState = extraData;
				IsCompleted = false;

				// Get SessionID
				SessionID = HttpContext.Current.Session.SessionID;

				// Read request
				var binData = context.Request.BinaryRead( context.Request.TotalBytes );
				var strData = System.Text.UTF8Encoding.UTF8.GetString( binData );
				var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
				var requestMessage = (Dictionary<string,object>)serializer.DeserializeObject( strData );

				string messageType = (string)requestMessage[ RequestTypeKey ];
				switch( messageType )
				{
					case RequestTypeInit: {
						// Allocate a new ConnectionID and send it to the peer
						var connectionID = ConnectionList.AllocateNewConnectionID( SessionID );
						var responseMessage = new Dictionary<string,object>();
						responseMessage[ ResponseTypeKey ] = ResponseTypeInit;
						responseMessage[ ResponseConnectionIDKey ] = connectionID;
						SendResponse( responseMessage );
						CompletedSynchronously = true;
						break; }

					case RequestTypePoll: {
						// Get ConnectionID
						ConnectionID = requestMessage.TryGetString( RequestConnectionIDKey );
						if( string.IsNullOrEmpty(ConnectionID) )
							throw new ApplicationException( "Missing ConnectionID in message" );

						// Register this connection for this ConnectionID
						if(! ConnectionList.RegisterConnection(this) )
						{
							System.Diagnostics.Debug.Fail( "The SessionID/ConnectionID could not be found in the ConnectionList. Sending Logout message" );  // Remove this assert if happens too often (and there is a good reason?)
							SendLogout();
							CompletedSynchronously = true;
							break;
						}
						break; }

					case RequestTypeMessages: {
						// Get ConnectionID
						ConnectionID = requestMessage.TryGetString( RequestConnectionIDKey );
						if( string.IsNullOrEmpty(ConnectionID) )
							throw new ApplicationException( "Missing ConnectionID in message" );

						if(! ConnectionList.CheckConnectionIsValid(SessionID, ConnectionID) )
						{
							System.Diagnostics.Debug.Fail( "The SessionID/ConnectionID could not be found in the ConnectionList. Sending Logout message" );  // Remove this assert if happens too often (and there is a good reason?)
							SendLogout();
							CompletedSynchronously = true;
							break;
						}

						var messages = new List<IDictionary<string,object>>();  // RequestID => MessageContent
						foreach( var messageItem in ((IEnumerable)requestMessage[ RequestMessagesListKey ]).Cast<IDictionary<string,object>>() )
						{
							var content = (IDictionary<string,object>)messageItem[ RequestMessageContentKey ];
							messages.Add( content );
						}
						MessageHandler.ReceiveMessages( ConnectionID, messages );

						// Send an empty message list as response
						// TODO: Alain: Check if there are no pending messages in the MessageHandler to reurn instead of an empty message list
						var responseMessage = new Dictionary<string,object>();
						responseMessage[ ResponseTypeKey ] = ResponseTypeMessages;
						responseMessage[ ResponseTypeMessages ] = new object[]{};
						SendResponse( responseMessage );
						CompletedSynchronously = true;
						break; }

					default:
						throw new NotImplementedException( "Unsupported message type '" + messageType + "'" );
				}
			}
			catch( System.Exception ex )
			{
				System.Diagnostics.Trace.WriteLine( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "() - *** EXCEPTION '" + ex.GetType().FullName + "': " + ex.Message );

				// Send exception to peer
				var responseMessage = new Dictionary<string,object>();
				responseMessage[ ResponseTypeKey ] = ResponseTypeException;
				responseMessage[ ResponseExceptionContentKey ] = ex.Message;
				SendResponse( responseMessage );
				CompletedSynchronously = true;
			}

			System.Diagnostics.Trace.WriteLine( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "() - ended" );
			return this;
		}

		public void EndProcessRequest(IAsyncResult r)
		{
			// NB: All the response logic is in SendResponse()
			System.Diagnostics.Trace.WriteLine( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "()" );
		}

		/// <remarks>Once registered to the ConnectionList, can only be called from the ConnectionList to avoid multiple threads trying to send message through the same connection</remarks>
		public void SendMessage(object messageContent)
		{
			System.Diagnostics.Debug.Assert( messageContent != null, "Missing parameter 'messageContent'" );

			var message = new Dictionary<string,object>();
			message[ ResponseTypeKey ] = ResponseTypeMessages;
			message[ ResponseMessagesListKey ] = new object[]{ messageContent };
			SendResponse( message );
		}

		/// <remarks>Once registered to the ConnectionList, can only be called from the ConnectionList to avoid multiple threads trying to send message through the same connection</remarks>
		public void SendMessage(string requestID, object messageContent)
		{
			System.Diagnostics.Debug.Assert( messageContent != null, "Missing parameter 'messageContent'" );
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(requestID), "Missing parameter 'requestID'" );

			var message = new Dictionary<string,object>();
			message[ ResponseTypeKey ] = ResponseTypeMessages;
			message[ ResponseRequestIDKey ] = requestID;
			message[ ResponseMessagesListKey ] = new object[]{ messageContent };
			SendResponse( message );
		}

		/// <remarks>Once registered to the ConnectionList, can only be called from the ConnectionList to avoid multiple threads trying to send message through the same connection</remarks>
		public void SendException(string requestID, Exception exception)
		{
			System.Diagnostics.Debug.Assert( exception != null, "Missing parameter 'exception'" );
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(requestID), "Missing parameter 'requestID'" );

			var message = new Dictionary<string,object>();
			message[ ResponseTypeKey ] = ResponseTypeException;
			message[ ResponseRequestIDKey ] = requestID;
			message[ ResponseExceptionContentKey ] = exception.Message;
			SendResponse( message );
		}

		/// <remarks>Once registered to the ConnectionList, can only be called from the ConnectionList to avoid multiple threads trying to send message through the same connection</remarks>
		public void SendReset()
		{
			var message = new Dictionary<string,object>();
			message[ ResponseTypeKey ] = ResponseTypeReset;
			SendResponse( message );
		}

		/// <remarks>Once registered to the ConnectionList, can only be called from the ConnectionList to avoid multiple threads trying to send message through the same connection</remarks>
		public void SendLogout()
		{
			var message = new Dictionary<string,object>();
			message[ ResponseTypeKey ] = ResponseTypeLogout;
			SendResponse( message );
		}

		private void SendResponse(Dictionary<string,object> responseMessage)
		{
			// Write response to stream
			var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
			var str = serializer.Serialize( responseMessage );
			HttpContext.Response.Write( str );

			// Terminate HTTP response
			IsCompleted = true;
			if( Callback != null )
				Callback( this );
		}
	}
}
