using System;
using System.Collections.Generic;
using System.Text;

namespace CommonLib.Utils.Event
{
	public class ActionHelper : ITriggerHoldable
	{
		public Action						Action;
		public int							DelaySeconds		{	get { return Delay.HasValue ? (int)Delay.Value/1000 : 0; }
																	set { Delay = (value > 0) ? (value*1000) : (int?)null; } }
		public bool							NoTrigger			{ get; set; }

		private int?						Delay;
		private System.Windows.Forms.Timer	Timer;

		public void Init(Action action)
		{
			Init( null, action );
		}

		public void Init(int? delaySeconds, Action action)
		{
			Action = action;
			NoTrigger = false;
			if( delaySeconds.HasValue )
				Delay = delaySeconds * 1000;
			else
				Delay = null;
			Timer = null;
		}

		public void Trigger()
		{
			if( Timer != null )
				Timer.Stop();
			Timer = null;
			if( Action == null )
				return;
			if( NoTrigger )
				return;

			if( Delay.HasValue )
			{
				var self = this;
				Timer = new System.Windows.Forms.Timer{ Interval=Delay.Value };
				Timer.Tick += (sender,e)=>
					{
						self.Timer.Dispose();
						self.Timer = null;
						self.Action();
					};
				Timer.Start();
			}
			else
			{
				Action();
			}
		}

		public void Abort()
		{
			if( Timer == null )
				return;
			Timer.Stop();
			Timer.Dispose();
			Timer = null;
		}

		public TriggerHolder<ActionHelper> NewHolder()
		{
			return new TriggerHolder<ActionHelper>( this );
		}
	}
}
