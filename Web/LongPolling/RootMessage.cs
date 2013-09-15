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
		private const string					TypeReset						= "reset";
		private const string					TypeLogout						= "logout";

		// Message keys:
		internal const string					KeySenderID						= "sender";
		internal const string					KeyMessageMessagesList			= "messages";

		private RootMessage() : base()  {}
		private RootMessage(IDictionary<string,object> content) : base(content)  {}

		internal static RootMessage CreateInitRootMessage(string connectionID)
		{
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(connectionID), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'connectionID'" );

			var message = new RootMessage {
								{ RootMessage.TypeKey, RootMessage.TypeInit },
								{ RootMessage.KeySenderID, connectionID } };
			return message;
		}

		/// <remarks>Once the connection has been registered to the ConnectionList, this message can only be sent by this ConnectionList (inside its lock()) to avoid multiple threads trying to send message through the same connection</remarks>
		internal static RootMessage CreateResetRootMessage()
		{
			var message = new RootMessage {
								{ RootMessage.TypeKey, RootMessage.TypeReset } };
			return message;
		}

		internal static RootMessage CreateEmptyResponseMessage()
		{
			return CreateRootMessage( new Message[]{} );
		}

		internal static RootMessage CreateRootMessage(IEnumerable<Message> messageContents)
		{
			var message = new RootMessage {
								{ RootMessage.TypeKey, RootMessage.TypeMessages },
								{ RootMessage.TypeMessages, messageContents } };
			return message;
		}

		/// <remarks>Once the connection has been registered to the ConnectionList, this message can only be sent by this ConnectionList (inside its lock()) to avoid multiple threads trying to send message through the same connection</remarks>
		internal static RootMessage CreateLogoutRootMessage()
		{
			var message = new RootMessage {
								{ RootMessage.TypeKey, RootMessage.TypeLogout } };
			return message;
		}
	}
}
