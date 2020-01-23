//
// CommonLibs/Web/LongPolling/Utils/ExtensionMethods.cs
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
