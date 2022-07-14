//
// CommonLibs/Utils/Tasks/TaskEntry.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2013 Alain CAO
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
using System.Linq;
using System.Threading;
using System.Globalization;
using System.Web;

namespace CommonLibs.Utils.Tasks
{
// NB: !!! Embedded locks must be in the order "TasksQueue" then "TaskEntry" or TaskEntry alone. NEVER "TaskEntry" then "TaskQueue" This can lead to dead-locks !!!
// So, be sure that everything that is inside a "lock(this.LockObject)" in this class will not "lock(Tasks.LockObject)"

	public class TaskEntry
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		public enum Statuses
		{
			Undefined			= 0,  // Default value
			Delayed,
			Queued,
			Running,
			Removed
		}

		internal Guid							ID						{ get; private set; }
		private readonly object					LockObject;
		private volatile ManualResetEvent		WaitLocker				= null;
		private volatile int					WaitLockerCount			= 0;

		/// <summary>The TaskQueue containing this object</summary>
		private readonly TasksQueue				Tasks;
		internal DateTime						ExecutionDate			{ get; private set; }
		public CultureInfo						CreatorCultureInfo		{ get; private set; }
		public CultureInfo						CreatorUICultureInfo	{ get; private set; }
		private Statuses						Status;
		private volatile bool					Terminated				= false;
		internal Thread							Thread					= null;
		internal Action<TaskEntry>				Callback;
		internal volatile Exception				CallbackException		= null;
// TODO: Alain: TaskEntry.OnException/OnTerminated: Rewrite with callback list ; Hint: replace AddOnExceptionCallback
		private event Action					OnTerminated;
		private event Action<Exception>			OnException;
		private event Action					OnRemoved;

		internal TaskEntry(TasksQueue tasks, DateTime executionDate, Statuses initialStatus, Action<TaskEntry> callback)
		{
			ASSERT( (initialStatus == Statuses.Delayed) || (initialStatus == Statuses.Queued), "Invalid 'initialStatus'" );
			ASSERT( executionDate.Kind == DateTimeKind.Utc, "'executionDate' is not in UTC" );

			ID = Guid.NewGuid();
			LockObject = this;
			Tasks = tasks;
			ExecutionDate = executionDate;
			Status = initialStatus;
			Callback = callback;

			if( Tasks.TransferCultureInfo )
			{
				CreatorCultureInfo = Thread.CurrentThread.CurrentCulture;
				CreatorUICultureInfo = Thread.CurrentThread.CurrentUICulture;
			}
			else
			{
				CreatorCultureInfo = null;
				CreatorUICultureInfo = null;
			}
		}

		public bool IsRemoved()
		{
			// NB: Not using 'GetStatus()' for performance and because once it is in 'Removed' state, it won't change anymore
			return Status == Statuses.Removed;
		}

		public bool IsTerminated()
		{
			return Terminated;
		}

		public void Remove()
		{
			if( Status == Statuses.Removed )
				// Already Removed
				return;

			// Remove this entry from the parent queue
			Tasks.Remove( this );  // NB: This will "lock(Tasks.LockObject)"
			ASSERT( Status == Statuses.Removed, "TaskEntry.Remove() is supposed to set the status to Removed" );

			if( OnRemoved != null )
				try { OnRemoved(); }
				catch( System.Exception ex )  { FAIL( "'TaskEntry.Remove()': 'OnRemoved' event threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
		}

		public override string ToString()
		{
			return "{" + ID + ": " + ExecutionDate + "}";
		}

		public void Wait()
		{
			ASSERT( Thread != Thread.CurrentThread, "'TaskEntry.Wait()' is not supposed to be called from inside the TaskEntry's callback..." );

			lock( LockObject )
			{
				if( Terminated )
					// Task already terminated
					return;

				if( WaitLocker == null )
					WaitLocker = new ManualResetEvent( false );
				++ WaitLockerCount;
			}

			// Wait for the Terminate() method to release this lock
			WaitLocker.WaitOne();
		}

		/// <summary>
		/// Returns the status of the current TaskEntry
		/// </summary>
		/// <remarks>Only informational since the Status is subject to change even before the method returns</remarks>
		public Statuses GetStatus()
		{
			return Status;
		}

		/// <summary>
		/// Method used to retreive the TaskEntry's status and optionnally perform an action depending on this status
		/// </summary>
		/// <param name="callback">The callback that will receive the Status. Since this method locks the TasksQueue object, the callback should preferably execute quickly.</param>
		public void GetStatus(Action<Statuses> callback)
		{
			lock( Tasks.LockObject )	// 1: Lock the whole TasksQueue because the given callback is susceptible to also lock it which can lead to dead-locks
			{
				lock( LockObject )		// 2: Then lock this TaskEntry so that any threads touching this object are locked
				{
					callback( Status );
				}
			}
		}

		/// <summary>
		/// Method to set the TaskEntry's status. Can only be called by the TasksQueue, and inside the callback of GetStatus().
		/// </summary>
		internal void SetStatus(Statuses newStatus)
		{
			Status = newStatus;
		}

		public void AddOnExceptionCallback(Action<Exception> callback)
		{
			ASSERT( callback != null, "Missing parameter 'callback'" );

			bool callRightNow = false;
			lock( LockObject )
			{
				OnException += callback;

				if( CallbackException != null )
				{
					// The task is already terminated but SetCallbackException() has already been called
					ASSERT( Status == Statuses.Removed, "An exception is already set on this task but its status is not Removed as it should be" );
					// NB: TaskQueue.TaskThread() first Remove()s the task THEN calls entry.SetCallbackException(). So the status of the entry should always be 'Disposed' if the exception is set.

					// The task is already terminated => call the callback directly
					callRightNow = true;
				}
			}

			if( callRightNow )
			{
				try { callback(CallbackException); }  // NB: This must absolutely be called outside of the lock() to avoid possible dead-locks if this ever locks the TaskQueue
				catch( System.Exception ex ) { FAIL( "The task's OnException event threw an exception: " + ex.Message ); }
			}
		}

		internal void SetCallbackException(Exception exception)
		{
			ASSERT( exception != null, "Missing parameter 'exception'" );
			ASSERT( Status == Statuses.Removed, "The CallbackException is being set but the task is not in Removed status" );

			CallbackException = exception;

			if( OnException != null )
			{
				try
				{
					OnException( exception );
				}
				catch( System.Exception ex )
				{
					FAIL( "The task's OnException event threw an exception: " + ex.Message );
				}
			}
			else
			{
				// There is nobody to notify about this exception. Last resort:
				FAIL( "The task threw an exception: " + exception.Message );
			}
		}

		public void AddOnRemovedCallback(Action callback)
		{
			bool callRightNow = false;
			lock( LockObject )
			{
				if( Status == Statuses.Removed )
					// The task is already terminated => call the callback directly
					callRightNow = true;
				else
					OnRemoved += callback;
			}

			if( callRightNow )
			{
				try { callback(); }  // NB: This must absolutely be called outside of the lock() to avoid possible dead-locks if this ever locks the TaskQueue
				catch( System.Exception ex ) { FAIL( "The task's OnTerminated event threw an exception: " + ex.Message ); }
			}
		}

		public void AddOnTerminatedCallback(Action callback)
		{
			bool callRightNow = false;
			lock( LockObject )
			{
				if( Terminated )
					// The task is already terminated => call the callback directly
					callRightNow = true;
				else
					OnTerminated += callback;
			}

			if( callRightNow )
			{
				try { callback(); }  // NB: This must absolutely be called outside of the lock() to avoid possible dead-locks if this ever locks the TaskQueue
				catch( System.Exception ex ) { FAIL( "The task's OnTerminated event threw an exception: " + ex.Message ); }
			}
		}

		/// <summary>
		/// Called by the TasksQueue when the end of the task's thread is ending
		/// </summary>
		internal void Terminate()
		{
			ASSERT( Status == Statuses.Removed, "Terminate() called but the task is not in 'Removed' state" );
			ASSERT( !Terminated, "Terminate() called but 'Terminated' is already true" );

			LOG( "Terminate() - Start" );

			lock( LockObject )
			{
				LOG( "Terminate() - Lock acquired" );
				Terminated = true;
				for(; WaitLockerCount > 0; --WaitLockerCount )
				{
					ASSERT( WaitLocker != null, "If WaitLockerCount > 0, WaitLocker is supposed to be set" );
					WaitLocker.Set();
					LOG( "Terminate() - 1 waiting thread released" );
				}
			}

			if( OnTerminated != null )
			{
				try { OnTerminated(); }
				catch( System.Exception ex ) { FAIL( "The task's OnTerminated event threw an exception: " + ex.Message ); }
			}

			LOG( "Terminate() - End" );
		}
	}
}
