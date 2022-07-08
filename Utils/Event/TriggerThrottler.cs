//
// CommonLibs/Utils/Event/TriggerThrottler.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2018 Alain CAO
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
