//
// CommonLibs/Web/LongPolling/Utils/ExtensionMethods.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2018 Alain CAO
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

namespace CommonLibs.MessagesBroker
{
	public static class MessageKeys
	{
		public const string		KeyMessageHandler				= "type";
		public const string		KeyMessageResponseHandler		= "reply_to_type";
		public const string		KeySenderID						= "sender";
		public const string		KeyReceiverID					= "receiver";
		public const string		KeyMessageException				= "exception";
		public const string		KeyMessageExceptionMessage			= "message";
		public const string		KeyMessageExceptionClass			= "class";
		public const string		KeyMessageExceptionStackTrace		= "stack";
	}

	public static class RootMessageKeys
	{
		public const string	KeyType				= "type";
		public const string	TypeInit				= "init";
		public const string	TypePoll				= "poll";
		public const string	TypeMessages			= "messages";
		public const string	TypeReset				= "reset";
		public const string	KeySenderID			= MessageKeys.KeySenderID;  // NB: use the same
		public const string	KeyMessageMessages	= "messages";
	}
}
