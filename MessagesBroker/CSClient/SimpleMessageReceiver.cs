
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
