//
// CommonLibs/Web/LongPolling/IConnection.cs
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
using System.Web;

namespace CommonLibs.Web.LongPolling
{
	public interface IConnection
	{
		string			SessionID					{ get; }
		string			ConnectionID				{ get; }

		/// <summary>Send message to peer</summary>
		void			SendResponseMessage(Message systemMessage);

///// <summary>Send a response message message to peer</summary>
///// <param name="requestID">The request this response is related to</param>
//void		SendMessage(string requestID, object messageContent);
///// <summary>Send an exception notification to peer</summary>
///// <param name="messageID">The request that generated the exception</param>
//void		SendException(string requestID, Exception exception);
///// <summary>Ask the peer to reconnect (to avoid stale connections)</summary>
//void		SendReset();
///// <summary>Inform the peer that the session has been closed</summary>
//void		SendLogout();
	}
}


// TODO: Alain: ILocalConnection with all methods and IConnection with only SendMessage() & SendException()
