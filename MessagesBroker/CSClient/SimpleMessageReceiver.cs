//
// CommonLibs/MessagesBroker/CSClient/SimpleMessageReceiver.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2010 - 2022 Alain CAO
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
using System.Threading.Tasks;

using CommonLibs.Utils;
using CommonLibs.Utils.Event;

namespace CommonLibs.MessagesBroker.CSClient
{
	using TMessage = IDictionary<string,object>;
	using TCallbacks = Dictionary<string,Func</*TMessage*/IDictionary<string,object>,Task>>;

	public interface IMessageReceiver
	{
		Task ReceiveMessage(TMessage message);
	}

	public class SimpleMessageReceiver : IMessageReceiver
	{
		public TCallbacks		Callbacks		{ get; } = new TCallbacks();

		/// <summary>p1: The received unhandled message</summary>
		public readonly CallbackListAsync<TMessage>			OnUnknownMessageReceived	= new CallbackListAsync<TMessage>();

		public async Task ReceiveMessage(TMessage message)
		{
			var callback = Callbacks.TryGet( message.TryGetString(MessageKeys.KeyMessageHandler) );
			if( callback == null )
			{
				await OnUnknownMessageReceived.Invoke( message );
				return;
			}

			await callback( message );
		}
	}
}
