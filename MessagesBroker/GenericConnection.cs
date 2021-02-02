//
// CommonLibs/MessagesBroker/GenericConnection.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2020 - 2021 Alain CAO
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
using System.Threading;
using System.Threading.Tasks;

namespace CommonLibs.MessagesBroker
{
	using TMessage = IDictionary<string,object>;
	using CMessage = Dictionary<string,object>;
	using MessageQueue = CommonLibs.Utils.AsyncQueue<IDictionary<string,object>>;

	public class GenericConnection : IEndPoint, IAsyncDisposable
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		public string			ID				=> id;
		private readonly string	id;
		public readonly IBroker	Broker;
		bool IEndPoint.			IsOneShot		=> false;
		private int				Disposed		= 0;

		public readonly MessageQueue	ReceiveMessagesQueue		= new MessageQueue();
		/// <summary>This message is sent when disposed</summary>
		public const TMessage			EndMessage					= null;

		public GenericConnection(IBroker broker, string id=null)
		{
			Broker	= broker;
			this.id	= id ?? Guid.NewGuid().ToString();
		}

		public async Task<TMessage> Start()
		{
			var initResponse = new CMessage {	{ RootMessageKeys.KeyType,		RootMessageKeys.TypeInit },
												{ RootMessageKeys.KeySenderID,	ID } };
			await Broker.RegisterEndpoint( this );
			return initResponse;
		}

		public async ValueTask DisposeAsync()
		{
			if( Interlocked.Exchange(ref Disposed, 1) != 0 )
				// Already disposed
				return;
			await Broker.UnRegisterEndpoint( ID );
			await ReceiveMessagesQueue.Push( EndMessage );
		}

		async Task IEndPoint.ReceiveMessages(IEnumerable<TMessage> messages)
		{
			await ReceiveMessagesQueue.Push( messages );
		}
	}
}
