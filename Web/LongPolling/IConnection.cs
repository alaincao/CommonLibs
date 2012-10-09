using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CommonLibs.Web.LongPolling
{
	public interface IConnection
	{
		string			SessionID					{ get; }
		string			ConnectionID				{ get; }

// TODO: Alain: SendMessages(object[])
		/// <summary>Send regular message to peer</summary>
		void		SendMessage(object messageContent);
		/// <summary>Send a response message message to peer</summary>
		/// <param name="requestID">The request this response is related to</param>
		void		SendMessage(string requestID, object messageContent);
		/// <summary>Send an exception notification to peer</summary>
		/// <param name="messageID">The request that generated the exception</param>
		void		SendException(string requestID, Exception exception);
		/// <summary>Ask the peer to reconnect (to avoid stale connections)</summary>
		void		SendReset();
		/// <summary>Inform the peer that the session has been closed</summary>
		void		SendLogout();
	}
}


// TODO: Alain: ILocalConnection with all methods and IConnection with only SendMessage() & SendException()
