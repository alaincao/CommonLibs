using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonLib.Utils.Event
{
	public interface ITriggerHoldable
	{
		bool				NoTrigger			{ get; set; }
	}

	public class TriggerHolder<T> : IDisposable where T : ITriggerHoldable
	{
		private bool		OldValue;
		private T			Helper;

		public TriggerHolder(T helper)
		{
			Helper = helper;
			OldValue = Helper.NoTrigger;
			Helper.NoTrigger = true;
		}

		public void Dispose()
		{
			Helper.NoTrigger = OldValue;
		}
	}
}
