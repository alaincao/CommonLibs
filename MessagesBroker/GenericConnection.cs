//
// CommonLibs/MessagesBroker/GenericConnection.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2020 - 2021 Alain CAO
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
