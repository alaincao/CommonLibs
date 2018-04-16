using System;
using System.Collections.Generic;
using System.Text;

namespace CommonLibs.Web.LongPolling
{
	public class MessageContextService
	{
		public MessageHandler	MessageHandler	{ get; private set; }
		public object			ContextObject	{ get; internal set; }

		public MessageContextService(MessageHandler messageHandler)
		{
			MessageHandler = messageHandler;
		}
	}
}
