//
// CommonLibs/Web/LongPolling/Message.cs
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

namespace CommonLibs.Web.LongPolling
{
	public class Message : Dictionary<string,object>
	{
		// Reserved message types:
		private const string					TypeFatalException				= "exception";

		// Reserved message keys for TypeMessages:
		internal const string					KeySenderID						= RootMessage.KeySenderID;  // NB: use the same
		public const string						KeyMessageHandler				= "type";
		public const string						KeyMessageResponseHandler		= "reply_to_type";
		internal const string					KeyMessageChainedMessages		= "chained_messages";
		public const string						KeyMessageException				= "exception";
		private const string					KeyMessageExceptionMessage			= "message";
		private const string					KeyMessageExceptionClass			= "class";
		private const string					KeyMessageExceptionStackTrace		= "stack";

		public string							SenderConnectionID				{ get { object id; return this.TryGetValue(KeySenderID, out id) ? ""+id : null; } }
		internal string							HandlerType						{ get { object handler; return this.TryGetValue(KeyMessageHandler, out handler) ? ""+handler : null; } }

		private Message() : base()  {}
		private Message(IDictionary<string,object> content) : base(content)  {}

		public override string ToString()
		{
			return "{" + HandlerType + " from " + SenderConnectionID + "}";
		}

		public static Message CreateResponseMessage(Message sourceMessage)
		{
			return CreateResponseMessage( sourceMessage, responseHandlerType:null );
		}

		/// <summary>
		/// Create a new message based on a request message
		/// </summary>
		/// <remarks>The sourceMessage must contain the key KeyMessageResponseHandler that will be used as TypeKey of the returned message</remarks>
		public static Message CreateResponseMessage(Message sourceMessage, string responseHandlerType)
		{
			// Get the reply-to message handler
			string handlerTypeString;
			if( responseHandlerType == null )
			{
				object handlerType;
				if( (!sourceMessage.TryGetValue(KeyMessageResponseHandler, out handlerType)) || ((handlerTypeString = handlerType as string) == null) )
					throw new ArgumentException( "The source message doesn't contain key '" + KeyMessageResponseHandler + "' ; Cannot create its response message" );
			}
			else
			{
				if( responseHandlerType.Trim() == "" )
					throw new ArgumentException( "Parameter 'responseHandlerType' is empty" );
				handlerTypeString = responseHandlerType;
			}

			var message = new Message( sourceMessage );
			message[ Message.KeyMessageHandler ] = handlerTypeString;  // Replace the message handler by the reply-to handler
			message.Remove( KeySenderID );  // Remove the original SenderID from the response
			message.Remove( KeyMessageResponseHandler );  // Remove the reply-to handler
			message.Remove( KeyMessageChainedMessages );  // Remove the chained messages if any
			return message;
		}

		internal static Message CreateReceivedMessage(string senderConnectionID, IDictionary<string,object> messageContent)
		{
			var message = new Message( messageContent );

			#if DEBUG  // Do checks only in DEBUGging mode

				// Check SenderIDKey
				CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(senderConnectionID), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'senderConnectionID' parameter" );
				if( messageContent.ContainsKey(KeySenderID) )
					throw new ArgumentException( "The message cannot contain key '" + KeySenderID + "' ; It will be overriden" );

				// Check MessageHandlerKey
				object handler;
				if(! message.TryGetValue(KeyMessageHandler, out handler) )
					throw new ArgumentException( "Message does not contain '" + KeyMessageHandler + "'" );
				var handlerStr = handler as string;
				if( string.IsNullOrEmpty(handlerStr) )
					throw new ArgumentException( "Message does not contain '" + KeyMessageHandler + "'" );

			#endif

			// Copy the SenderConnectionID
			message[ KeySenderID ] = senderConnectionID;
			return message;
		}

		public static Message CreateMessage(string peerHandlerType, IDictionary<string,object> template)
		{
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(peerHandlerType), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'peerHandlerType' parameter" );
			CommonLibs.Utils.Debug.ASSERT( template != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'template' parameter" );

			var message = new Message( template );
			message[ Message.KeyMessageHandler ] = peerHandlerType;
			return message;
		}

		public static Message CreateMessage(IDictionary<string,object> template)
		{
			CommonLibs.Utils.Debug.ASSERT( template != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'template' parameter" );

			var message = new Message( template );
			return message;
		}

		public static Message CreateEmtpyMessage(string peerHandlerType)
		{
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(peerHandlerType), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'peerHandlerType' parameter" );

			var message = new Message {	{ Message.KeyMessageHandler, peerHandlerType } };
			return message;
		}

		public static Message CreateExceptionMessage(Exception exception, Message sourceMessage=null)
		{
			CommonLibs.Utils.Debug.ASSERT( exception != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'exception'" );

			// TODO: Alain: static CreateExceptionMessage(): Use ExceptionManager to create exception content and send it in the message.
			var exceptionTemplate = new Dictionary<string,object> {  // Create the KeyMessageException with the exception content
																	{ KeyMessageExceptionMessage, exception.Message },
																	{ KeyMessageExceptionClass, exception.GetType().FullName },
																	{ KeyMessageExceptionStackTrace, exception.StackTrace }
																};

			// Determine if there is a 'reply_to_type' handler to send to
			bool hasReplyTo;
			if( sourceMessage == null )
			{
				hasReplyTo = false;
			}
			else
			{
				object handlerType;
				if( sourceMessage.TryGetValue(Message.KeyMessageResponseHandler, out handlerType) )
				{
					if(! string.IsNullOrEmpty(handlerType as string) )
						hasReplyTo = true;
					else
						hasReplyTo = false;
				}
				else
				{
					hasReplyTo = false;
				}
			}

			Message message;
			if( hasReplyTo )
			{
				// This is a regular response message to which we add a special 'exception' key (NB: to avoid polluting the root of the message)
				message = CreateResponseMessage( sourceMessage );
				CommonLibs.Utils.Debug.ASSERT(! message.ContainsKey(KeyMessageException), System.Reflection.MethodInfo.GetCurrentMethod(), "The message key 'exception' will be overwritten on the response message" );
				message[ KeyMessageException ] = exceptionTemplate;
			}
			else
			{
				// There is no handler known to receive this exception => Create a new message of type 'exception' with the exception's content directly in the message's root
				message = CreateMessage( peerHandlerType:TypeFatalException, template:exceptionTemplate );
			}

			return message;
		}
	}
}
