﻿//
// CommonLibs/Utils/Event/ActionHelper.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2018 - 2022 Alain CAO
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
using System.Text;

namespace CommonLibs.Utils.Event
{
	public class ActionHelper : ITriggerHoldable
	{
		public Action						Action				{ get; set; }
		public int							DelaySeconds		{	get { return Delay.HasValue ? Delay.Value/1000 : 0; }
																	set { Delay = (value > 0) ? (value*1000) : (int?)null; } }
		public bool							NoTrigger			{ get; set; }

		private int?						Delay;
		private System.Timers.Timer			Timer;

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
				Timer = new System.Timers.Timer{ Interval=Delay.Value };
				Timer.Elapsed += (sender,e)=>
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

	public class ActionHelper<T> : ITriggerHoldable
	{
		public Action<T>					Action				{ get; set; }
		public int							DelaySeconds		{	get { return Delay.HasValue ? Delay.Value/1000 : 0; }
																	set { Delay = (value > 0) ? (value*1000) : (int?)null; } }
		public bool							NoTrigger			{ get; set; }

		private int?						Delay;
		private System.Timers.Timer			Timer;

		public void Init(Action<T> action)
		{
			Init( null, action );
		}

		public void Init(int? delaySeconds, Action<T> action)
		{
			Action = action;
			NoTrigger = false;
			if( delaySeconds.HasValue )
				Delay = delaySeconds * 1000;
			else
				Delay = null;
			Timer = null;
		}

		public void Trigger(T parm)
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
				Timer = new System.Timers.Timer{ Interval=Delay.Value };
				Timer.Elapsed += (sender,e)=>
					{
						self.Timer.Dispose();
						self.Timer = null;
						self.Action(parm);
					};
				Timer.Start();
			}
			else
			{
				Action(parm);
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

		public TriggerHolder<ActionHelper<T>> NewHolder()
		{
			return new TriggerHolder<ActionHelper<T>>( this );
		}
	}
}
