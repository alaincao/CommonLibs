//
// CommonLibs/Web/LongPolling/HttpConnection.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2018 Alain CAO
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
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

using Microsoft.AspNetCore.Http;

using CommonLibs.Web.LongPolling.Utils;

namespace CommonLibs.Web.LongPolling
{
	internal class HttpConnection : IConnection
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		public MessageHandler	MessageHandler		{ get; private set; }
		internal HttpContext	HttpContext			{ get; private set; }

		#region For IConnection

		public string			SessionID			{ get; private set; }
		public string			ConnectionID		{ get; private set; }
		public bool				Sending				{ get; set; }

		#endregion

		private TaskCompletionSource<RootMessage>	CompletionSource	= null;

		internal HttpConnection(MessageHandler messageHandler, HttpContext httpContext, string sessionID)
		{
			ASSERT( messageHandler != null, "Missing parameter 'messageHandler'" );
			ASSERT( httpContext != null, "Missing parameter 'httpContext'" );
			ASSERT( !string.IsNullOrEmpty(sessionID), "Missing parameter 'sessionID'" );

			MessageHandler = messageHandler;
			HttpContext = httpContext;
			SessionID = sessionID;
			ConnectionID = null;  // NB: Set later
			Sending = false;
		}

		internal async Task<RootMessage> ReceiveRequest(HttpContext context, RootMessage requestMessage)
		{
			// Check message type
			var connectionList = MessageHandler.ConnectionList;
			string messageType = (string)requestMessage[ RootMessage.TypeKey ];
			LOG( "ReceiveRequest() - Received message of type '" + messageType + "'" );
			switch( messageType )
			{
				case RootMessage.TypeInit:
				{
					bool initAccepted = connectionList.CheckInitAccepted( requestMessage, HttpContext );
					if(! initAccepted )
					{
						// Init refused => Send 'logout'
						LOG( "ReceiveRequest() - Respond with 'logout' message" );
						return RootMessage.CreateServer_Logout();
					}

					// Allocate a new ConnectionID and send it to the peer
					ConnectionID = connectionList.AllocateNewConnectionID( context, SessionID );
					LOG( "ReceiveRequest() - Respond with 'init' message" );
					return RootMessage.CreateServer_Init( ConnectionID );
				}

				case RootMessage.TypePoll:
				{
					// Get ConnectionID
					ConnectionID = requestMessage.TryGetString( RootMessage.KeySenderID );
					if( string.IsNullOrEmpty(ConnectionID) )
						throw new ApplicationException( "Missing sender ID in message" );

					if(! connectionList.CheckConnectionIsValid(SessionID, ConnectionID) )
					{
						LOG( "ReceiveRequest() *** The SessionID/ConnectionID could not be found in the 'connectionList'. Sending Logout message" );
						return RootMessage.CreateServer_Logout();
					}

					// Prepare the 'SendRootMessage()' method so it can be used by the ConnectionList
					CompletionSource = new TaskCompletionSource<RootMessage>();

					// Register this connection for to the ConnectionList
					LOG( "ReceiveRequest() - Nothing to send right now - registering HttpConnection to ConnectionList" );
					if(! connectionList.RegisterConnection(this, startStaleTimeout:true) )
					{
						FAIL( "The SessionID/ConnectionID could not be found in the 'connectionList'. Sending Logout message" );  // This check is already done above (only LOG()ged). This should really not happen often => FAIL()
						return RootMessage.CreateServer_Logout();
					}

					// Wait for any messages to send to this connection
					try
					{
						var response = await CompletionSource.Task;
						ASSERT( Sending, "The ConnectionList did not set the 'Sending' property" );
						return response;
					}
					finally
					{
						// Unregister this connection from the ConnectionList
						try { connectionList.UnregisterConnection( this ); }
						catch( System.Exception ex )  { FAIL( "ReceiveRequest() *** Exception (" + ex.GetType().FullName + ") while calling connectionList.UnregisterConnection()" ); }
					}
				}

				case RootMessage.TypeMessages:
				{
					// Get ConnectionID
					ConnectionID = requestMessage.TryGetString( RootMessage.KeySenderID );
					if( string.IsNullOrEmpty(ConnectionID) )
						throw new ApplicationException( "Missing ConnectionID in message" );

					if(! connectionList.CheckConnectionIsValid(SessionID, ConnectionID) )
					{
						LOG( "ReceiveRequest() *** The SessionID/ConnectionID could not be found in the 'connectionList'. Sending Logout message" );
						return RootMessage.CreateServer_Logout();
					}

					// Hold any messages that must be sent to this connection until all those messages are received
					// so that reply messages are not sent one by one
					LOG( "ReceiveRequest() - Hold connection '" + ConnectionID + "'" );
					MessageHandler.HoldConnectionMessages( ConnectionID );
					try
					{
						foreach( var messageItem in ((IEnumerable)requestMessage[ RootMessage.KeyMessageMessagesList ]).Cast<IDictionary<string,object>>() )
						{
							Message message = null;
							try
							{
								var receivedMessage = Message.CreateReceivedMessage( ConnectionID, messageItem );
								LOG( "ReceiveRequest() - Receiving message '" + receivedMessage + "'" );
								MessageHandler.ReceiveMessage( receivedMessage );
							}
							catch( System.Exception ex )
							{
								LOG( "ReceiveRequest() *** Exception (" + ex.GetType().FullName + ") while receiving message" );

								// Send exception through MessageHandler so that we can continue to parse the other messages
								if( message == null )
									MessageHandler.SendMessageToConnection( ConnectionID, Message.CreateExceptionMessage(exception:ex) );
								else
									MessageHandler.SendMessageToConnection( ConnectionID, Message.CreateExceptionMessage(exception:ex, sourceMessage:message) );
							}
						}
					}
					finally
					{
						LOG( "ReceiveRequest() - Remove the hold on connection '" + ConnectionID + "'" );
						try { MessageHandler.UnholdConnectionMessages( ConnectionID ); }
						catch( System.Exception ex )  { FAIL( "ReceiveRequest() *** Exception (" + ex.GetType().FullName + ") while calling MessageHandler.UnholdConnectionMessages('" + ConnectionID + "')" ); }
					}

					// NB: The first idea was to send pending messages (if there were any available) through the "messages" request instead of waiting for the "polling" request to send them.
					// But since both requests could be sending concurrently, it would be hard for the client-side to receive all messages IN THE RIGHT ORDER
					// => Always reply with an empty response message list ; Don't send the pending messages
					return RootMessage.CreateServer_EmptyResponse();
				}

				default:
					throw new NotImplementedException( "Unsupported root message type '" + messageType + "'" );
			}
		}

		/// <remarks>
		/// WARNING: Every calls to this method should be performed outside any lock()s because the call to "AsyncCallback(this)" will try to create a new thread.<br/>
		/// Example dead-lock scenario:<br/>
		/// Under heavy load, all worker threads are busy and they are all waiting for a lock() to liberate.<br/>
		/// One of the worker thread is calling this method from inside of this lock().<br/>
		/// This thread will try to create a new thread from the "AsyncCallback(this)".<br/>
		/// The problem is that "AsyncCallback(this)" will then hang, waiting for a worker slot thread to liberate...
		/// </remarks>
		public void SendRootMessage(RootMessage responseMessage)
		{
			ASSERT( responseMessage != null, "Missing parameter 'responseMessage'" );
			ASSERT( CompletionSource != null, "'SendRootMessage()' invoked, but the 'CompletionSource' has not been set" );  // NB: Not a POLL request
			ASSERT( CompletionSource.Task.IsCompleted == false, "'SendRootMessage()' invoked, but the connection has already been completed" );

			CompletionSource.SetResult( responseMessage );
		}

		public void Close(RootMessage rootMessage)
		{
			// NB: Sending a message automatically close the connection ...
			SendRootMessage( rootMessage );
		}
	}
}
