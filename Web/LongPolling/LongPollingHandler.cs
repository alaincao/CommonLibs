//
// CommonLibs/Web/LongPolling/LongPollingHandler.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.SessionState;

using CommonLibs.Web.LongPolling.Utils;

namespace CommonLibs.Web.LongPolling
{
	public abstract class LongPollingHandler : IHttpAsyncHandler, IReadOnlySessionState
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		protected abstract MessageHandler		MessageHandler							{ get; }
		protected abstract ConnectionList		ConnectionList							{ get; }

		#region For IHttpAsyncHandler

		public bool								IsReusable								{ get { return true; } }

		#endregion

		void IHttpHandler.ProcessRequest(HttpContext context)
		{
			FAIL( "ProcessRequest() - Should not be used by IHttpAsyncHandler" );  // Should only be used by IHttpHandler
		}

		IAsyncResult IHttpAsyncHandler.BeginProcessRequest(HttpContext context, AsyncCallback callback, object asyncState)
		{
			LOG( "BeginProcessRequest() - Start" );
			ASSERT( MessageHandler != null, "Property 'MessageHandler' is not defined" );
			ASSERT( ConnectionList != null, "Property 'ConnectionList' is not defined" );

			RootMessage responseMessage;
			string sessionID;
			string connectionID = null;
			bool connectionHeld = false;  // Set to true if this ConnectionID has been held to receive the arriving messages
			try
			{
				// Get SessionID
				sessionID = LongPolling.ConnectionList.GetSessionID( context );

				// Read request
				var binData = context.Request.BinaryRead( context.Request.TotalBytes );
				var strData = System.Text.UTF8Encoding.UTF8.GetString( binData );
				var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
				var requestMessage = (Dictionary<string,object>)serializer.DeserializeObject( strData );

				// Check message type
				string messageType = (string)requestMessage[ RootMessage.TypeKey ];
				LOG( "BeginProcessRequest() - Received message of type '" + messageType + "'" );
				switch( messageType )
				{
					case RootMessage.TypeInit: {

						bool initAccepted = ConnectionList.CheckInitAccepted( requestMessage, sessionID );
						if( initAccepted )
						{
							// Allocate a new ConnectionID and send it to the peer
							connectionID = ConnectionList.AllocateNewConnectionID( sessionID );
							responseMessage = RootMessage.CreateInitRootMessage( connectionID );
							LOG( "BeginProcessRequest() - Response message set to 'init'" );
						}
						else
						{
							// Init refused => Send 'logout'
							responseMessage = RootMessage.CreateLogoutRootMessage();
							LOG( "BeginProcessRequest() - Response message set to 'logout'" );
						}
						break; }

					case RootMessage.TypePoll: {

						// Get ConnectionID
						connectionID = requestMessage.TryGetString( RootMessage.KeySenderID );
						if( string.IsNullOrEmpty(connectionID) )
							throw new ApplicationException( "Missing sender ID in message" );

						if(! ConnectionList.CheckConnectionIsValid(sessionID, connectionID) )
						{
							LOG( "BeginProcessRequest() *** The SessionID/ConnectionID could not be found in the ConnectionList. Sending Logout message" );
							responseMessage = RootMessage.CreateLogoutRootMessage();
							break;
						}

						// Register this connection for this ConnectionID
						responseMessage = null;

						// Don't set checkPendingMessages to true: Don't pull the messages from the MessageHandler right now but let it get registered to the ConnectionList first.
						// So under heavy load, if there are always pending messages available each time the polling request reaches the server,
						// the whole connection doesn't get timed-out because it never had the opportunity to register...
						break; }

					case RootMessage.TypeMessages: {

						// Get ConnectionID
						connectionID = requestMessage.TryGetString( RootMessage.KeySenderID );
						if( string.IsNullOrEmpty(connectionID) )
							throw new ApplicationException( "Missing ConnectionID in message" );

						if(! ConnectionList.CheckConnectionIsValid(sessionID, connectionID) )
						{
							LOG( "BeginProcessRequest() *** The SessionID/ConnectionID could not be found in the ConnectionList. Sending Logout message" );
							responseMessage = RootMessage.CreateLogoutRootMessage();
							break;
						}

						// Hold any messages that must be sent to this connection until all those messages are received
						// so that reply messages are not sent one by one
						LOG( "BeginProcessRequest() - Holding connection '" + connectionID + "'" );
						connectionHeld = true;
						MessageHandler.HoldConnectionMessages( connectionID );

						foreach( var messageItem in ((IEnumerable)requestMessage[ RootMessage.KeyMessageMessagesList ]).Cast<IDictionary<string,object>>() )
						{
							Message message = null;
							try
							{
								var receivedMessage = Message.CreateReceivedMessage( connectionID, messageItem );
								LOG( "BeginProcessRequest() - Receiving message '" + receivedMessage + "'" );
								MessageHandler.ReceiveMessage( receivedMessage );
							}
							catch( System.Exception ex )
							{
								LOG( "BeginProcessRequest() *** Exception (" + ex.GetType().FullName + ") while receiving message" );

								// Send exception through MessageHandler so that we can continue to parse the other messages
								if( message == null )
									MessageHandler.SendMessageToConnection( connectionID, Message.CreateExceptionMessage(exception:ex) );
								else
									MessageHandler.SendMessageToConnection( connectionID, Message.CreateExceptionMessage(exception:ex, sourceMessage:message) );
							}
						}

						// NB: The first idea was to send pending messages (if there were any available) through the "messages" request instead of waiting for the "polling" request to send them.
						// But since both requests could be sending concurrently, it would be hard for the client-side to receive all messages IN THE RIGHT ORDER
						// => Always reply with an empty response message list ; Don't send the pending messages
						responseMessage = RootMessage.CreateEmptyResponseMessage();
						break; }

					default:
						throw new NotImplementedException( "Unsupported message type '" + messageType + "'" );
				}
			}
			catch( System.Exception ex )
			{
				LOG( System.Reflection.MethodInfo.GetCurrentMethod().Name + "() *** EXCEPTION WHILE PARSING MESSAGE '" + ex.GetType().FullName + "': " + ex.Message );

				// Send exception to peer (right now ; not through the MessageHandler)
				// NB: The actual message is a message list with only 1 item of type 'exception'
				var exceptionMessage = RootMessage.CreateRootMessage( new Message[]{ Message.CreateExceptionMessage(exception:ex) } );
				var exceptionResult = new LongPollingConnection( exceptionMessage, context, callback, asyncState );
				return exceptionResult;
			}
			finally
			{
				if( connectionHeld && (connectionID != null) )
				{
					// Remove the hold on this connection
					LOG( "BeginProcessRequest() - Unholding messages for connection '" + connectionID + "'" );
					try { MessageHandler.UnholdConnectionMessages( connectionID ); }
					catch( System.Exception ex )  { FAIL( "BeginProcessRequest() *** Exception (" + ex.GetType().FullName + ") while calling MessageHandler.UnholdConnectionMessages('" + connectionID + "')" ); }
				}
			}

			LOG( "BeginProcessRequest() - Creating the LongPollingConnection" );
			var connection = new LongPollingConnection( sessionID, connectionID, context, callback, asyncState );

			if( responseMessage == null )
			{
				LOG( "BeginProcessRequest() - Nothing to send right now - registering LongPollingConnection to ConnectionList" );
				if(! ConnectionList.RegisterConnection(connection) )
				{
					FAIL( "The SessionID/ConnectionID could not be found in the ConnectionList. Sending Logout message" );  // This check is already done above (only LOG()ged). This should really not happen often => FAIL()
					responseMessage = RootMessage.CreateLogoutRootMessage();
				}
			}

			if( responseMessage != null )
			{
				LOG( "BeginProcessRequest() - Sending response right now" );
				connection.SendResponseMessageSynchroneously( responseMessage );
			}

			LOG( "BeginProcessRequest() - Exit" );
			return connection;
		}

		void IHttpAsyncHandler.EndProcessRequest(IAsyncResult result)
		{
			LOG( "EndProcessRequest() - Start" );

			var connection = (LongPollingConnection)result;
			ASSERT( connection.ResponseMessage != null, "EndProcessRequest() called but there are no ResponseMessage available" );  //  This method should be called by LongPollingConnection.SendResponseMessage() only

			// Write response to stream
			var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
			var str = serializer.Serialize( connection.ResponseMessage );
			connection.HttpContext.Response.Write( str );

			LOG( "EndProcessRequest() - End" );
		}
	}
}
