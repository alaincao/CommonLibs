//
// CommonLibs/Utils/Event/TriggerThrottler.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2018 Alain CAO
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
using System.Threading;
using System.Threading.Tasks;

namespace CommonLibs.Utils.Event
{
	public class TriggerThrottler
	{
		private readonly object		LockObject			= new object();
		private int					DelayTicks			= 1 * (int)TimeSpan.TicksPerSecond;  // Default: 1 second
		private long				LastTriggerTick		= 0;
		private volatile Timer		Timer				= null;
		private volatile bool		StillRunning		= false;

		public Tasks.TasksQueue		TasksQueue			{ get; private set; }
		public bool					InSeparateThread	{ get; set; } = false;
		public TimeSpan				Delay				{ get { return new TimeSpan(DelayTicks); } set { DelayTicks = (int)value.Ticks; } }
		public Action				CallBackSync		{ get; set; } = null;
		public Func<Task>			CallBackAsync		{ get; set; } = null;

		public TriggerThrottler()  {}

		public void Trigger()
		{
			Trigger( fromTimer:false ).FireAndForget();
		}

		public async Task TriggerAsync()
		{
			await Trigger( fromTimer:false );
		}

		private async Task Trigger(bool fromTimer)
		{
			if( (! fromTimer) && (Timer != null) )
				// Already throttling => Discard
				goto DISCARD;

			lock( LockObject )
			{
				if( (! fromTimer) && (Timer != null) )
					// Throttling launched in the mean-time (very unlikely) => Discard
					goto DISCARD;

				var now = DateTime.UtcNow.Ticks;

				if( fromTimer )
				{
					// Invoked by the expiration of the timer
					Timer.Dispose();
					Timer = null;

					if( StillRunning )
					{
						// Previous invokation not yet terminated => Redelay this one
						var timerMiliseconds = ((long)DelayTicks / TimeSpan.TicksPerMillisecond);
						Timer = new System.Threading.Timer( (state)=>{ Trigger(fromTimer:true).FireAndForget(); }, state:null, dueTime:timerMiliseconds, period:Timeout.Infinite );
						goto DISCARD;
					}

					LastTriggerTick = now;
					StillRunning = true;
					goto INVOKE_CALLBACK;
				}
				else if( (now - LastTriggerTick) < DelayTicks )
				{
					// Delay not yet elapsed => Start the timer
					var timerTicks = DelayTicks - (now - LastTriggerTick);
					var timerMiliseconds = ( timerTicks / TimeSpan.TicksPerMillisecond );
					CommonLibs.Utils.Debug.ASSERT( timerMiliseconds >= 0, this, "Logic error" );
					Timer = new System.Threading.Timer( (state)=>{ Trigger(fromTimer:true).FireAndForget(); }, state:null, dueTime:timerMiliseconds, period:Timeout.Infinite );
					goto DISCARD;
				}
				else
				{
					if( StillRunning )
						// Avoid overlaps
						goto DISCARD;

					// Invoke right now
					LastTriggerTick = now;
					StillRunning = true;
					goto INVOKE_CALLBACK;
				}

				//CommonLibs.Utils.Debug.ASSERT( false, this, "Unreachable code reached" )
			}
		DISCARD:
			return;

		INVOKE_CALLBACK:
			if(! InSeparateThread )
			{
				await InvokeCallback();
			}
			else
			{
				if( TasksQueue == null )
					TasksQueue = new Tasks.TasksQueue();
				TasksQueue.CreateTask( (e)=>{ InvokeCallback().Wait(); } );
			}
			return;
		}

		private async Task InvokeCallback()
		{
			CommonLibs.Utils.Debug.ASSERT( (CallBackSync == null) != (CallBackAsync == null), this, "One and only one of 'CallBackSync' and 'CallBackAsync' must be specified" );

			try
			{
				CommonLibs.Utils.Debug.ASSERT( StillRunning, this, "Property 'StillRunning' is supposed to be 'true' here" );
				if( CallBackSync != null )
					CallBackSync();
				else
					// NB: This is the only case the 'await' is effective: ( (InSeparateThread == false) && (CallBackAsync != null) )
					await CallBackAsync();
			}
			catch( System.Exception ex )
			{
				CommonLibs.Utils.Debug.ASSERT( false, this, "'CallBack()' invokation threw an exception ("+ex.GetType()+"): "+ex.Message );
			}
			finally
			{
				CommonLibs.Utils.Debug.ASSERT( StillRunning, this, "Property 'StillRunning' is still supposed to be 'true' here" );
				StillRunning = false;
			}
		}
	}
}
