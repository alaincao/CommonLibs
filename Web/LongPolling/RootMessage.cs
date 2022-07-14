//
// CommonLibs/Web/LongPolling/RootMessage.cs
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
using System.Web;

namespace CommonLibs.Web.LongPolling
{
	public class RootMessage : Dictionary<string,object>
	{
		internal const string					TypeKey							= "type";

		// Message types:
		internal const string					TypeInit						= "init";
		internal const string					TypePoll						= "poll";
		internal const string					TypeMessages					= "messages";
		internal const string					TypeReset						= "reset";
		internal const string					TypeLogout						= "logout";

		// Message keys:
		internal const string					KeySenderID						= "sender";
		internal const string					KeyMessageMessagesList			= "messages";

		private RootMessage() : base()  {}
		private RootMessage(IDictionary<string,object> content) : base(content)  {}

		internal static RootMessage CreateServer_Init(string connectionID)
		{
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(connectionID), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'connectionID'" );

			var message = new RootMessage {
								{ RootMessage.TypeKey, RootMessage.TypeInit },
								{ RootMessage.KeySenderID, connectionID } };
			return message;
		}

		/// <remarks>Once the connection has been registered to the ConnectionList, this message can only be sent by this ConnectionList (inside its lock()) to avoid multiple threads trying to send message through the same connection</remarks>
		internal static RootMessage CreateServer_Reset()
		{
			var message = new RootMessage {
								{ RootMessage.TypeKey, RootMessage.TypeReset } };
			return message;
		}

		internal static RootMessage CreateServer_EmptyResponse()
		{
			return CreateServer_MessagesList( new Message[]{} );
		}

		internal static RootMessage CreateServer_MessagesList(IEnumerable<Message> messageContents)
		{
			var message = new RootMessage {
								{ RootMessage.TypeKey, RootMessage.TypeMessages },
								{ RootMessage.TypeMessages, messageContents } };
			return message;
		}

		/// <remarks>Once the connection has been registered to the ConnectionList, this message can only be sent by this ConnectionList (inside its lock()) to avoid multiple threads trying to send message through the same connection</remarks>
		internal static RootMessage CreateServer_Logout()
		{
			var message = new RootMessage {
								{ RootMessage.TypeKey, RootMessage.TypeLogout } };
			return message;
		}

		internal static RootMessage CreateServer_Exception(System.Exception ex)
		{
			return CreateServer_MessagesList( new Message[]{ Message.CreateExceptionMessage(exception:ex) } );
		}

		/// <remarks>Used by client only</remarks>
		internal static RootMessage CreateClientInit()
		{
			var message = new RootMessage {
								{ RootMessage.TypeKey, RootMessage.TypeInit } };
			return message;
		}

		internal static RootMessage CreateClient_Poll(string connectionID)
		{
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrWhiteSpace(connectionID), typeof(RootMessage), "Missing parameter 'connectionID'" );

			var message = new RootMessage {
								{ RootMessage.TypeKey, RootMessage.TypePoll },
								{ RootMessage.KeySenderID, connectionID } };
			return message;
		}

		internal static RootMessage CreateClient_ServerResponse(IDictionary<string,object> content)
		{
			return new RootMessage( content );
		}

		internal static RootMessage CreateClient_MessagesList(string connectionID, IEnumerable<Message> messageContents)
		{
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrWhiteSpace(connectionID), typeof(RootMessage), "Missing parameter 'connectionID'" );
			CommonLibs.Utils.Debug.ASSERT( messageContents != null, typeof(RootMessage), "Missing parameter 'messageContents'" );

			var message = new RootMessage {
								{ RootMessage.TypeKey, RootMessage.TypeMessages },
								{ RootMessage.KeySenderID, connectionID },
								{ RootMessage.TypeMessages, messageContents } };
			return message;
		}
	}
}
