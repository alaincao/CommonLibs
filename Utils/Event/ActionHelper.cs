//
// CommonLibs/Utils/Event/ActionHelper.cs
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
using System.Text;

namespace CommonLibs.Utils.Event
{
	using E=CommonLibs.Utils.ExceptionShield;

	public class ActionHelper : ITriggerHoldable
	{
		public class NoDelayHolder : IDisposable
		{
			private ActionHelper	ActionHelper;
			private int				OriginalDelaySecons;

			public NoDelayHolder(ActionHelper helper)
			{
				ActionHelper = helper;
				OriginalDelaySecons = helper.DelaySeconds;
				helper.DelaySeconds = 0;
			}

			public void Dispose()
			{
				ActionHelper.DelaySeconds = OriginalDelaySecons;
			}
		}

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

			if(! Delay.HasValue )
			{
				// Trigger the Action right now
				E.E( Action );
			}
			else
			{
				// Trigger the action in 'Delay' miliseconds
				var self = this;  // NB: Copy to local variable for the lambda fct so that changing 'Action' before the timer triggers doesn't impact
				Timer = new System.Windows.Forms.Timer{ Interval=Delay.Value };
				Timer.Tick += (sender,e)=>
					{
						self.Timer.Dispose();
						self.Timer = null;
						E.E( self.Action );
					};
				Timer.Start();
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

		public NoDelayHolder NewNoDelayHolder()
		{
			return new NoDelayHolder( this );
		}
	}
}
