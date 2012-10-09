using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace CommonLibs.Web.LongPolling.Utils
{
	public class TaskEntry : IDisposable
	{
		public enum Statuses
		{
			Undefined			= 0,  // Default value
			Delayed,
			Queued,
			Running,
			Disposed
		}

		internal Guid							ID						{ get; private set; }
		/// <summary>The TaskQueue containing this object</summary>
		private TasksQueue						Tasks;
		internal DateTime						ExecutionDate			{ get; private set; }
		private Statuses						Status;
		internal Thread							Thread					= null;
		internal Action<TaskEntry>				Callback;
		internal volatile Exception				CallbackException		= null;
		private event Action					OnTerminated;
		private event Action<Exception>			OnException;

		internal TaskEntry(TasksQueue tasks, DateTime executionDate, Statuses initialStatus, Action<TaskEntry> callback)
		{
			System.Diagnostics.Debug.Assert( (initialStatus == Statuses.Delayed) || (initialStatus == Statuses.Queued), "Invalid 'initialStatus'" );
			System.Diagnostics.Debug.Assert( executionDate.Kind == DateTimeKind.Utc, "'executionDate' is not in UTC" );

			ID = Guid.NewGuid();
			Tasks = tasks;
			ExecutionDate = executionDate;
			Status = initialStatus;
			Callback = callback;
		}

		public void Dispose()
		{
			GetStatus( (status)=>
				{
					if( status == Statuses.Disposed )
						// Already disposed
						return;

					// Remove this entry from the parent queue
					Tasks.Remove( this );
					System.Diagnostics.Debug.Assert( Status == Statuses.Disposed, "Tasks.Remove() is supposed to set the status to Disposed" );
				} );
		}

		public override string ToString()
		{
			return "" + ID + ": " + ExecutionDate;
		}

		/// <summary>
		/// Method used to retreive the TaskEntry's status and optionnally perform an action depending on this status
		/// </summary>
		/// <param name="callback">The callback that will receive the Status. Since this method locks the TaskEntry object, the callback should preferably execute quickly.</param>
		public void GetStatus(Action<Statuses> callback)
		{
			lock( this )
			{
				callback( Status );
			}
		}

		/// <summary>
		/// Method to set the TaskEntry's status. Can only be called by the TaskQueue, and inside the callback of GetStatus();
		/// </summary>
		internal void SetStatus(Statuses newStatus)
		{
			Status = newStatus;
		}

		internal void Abort()
		{
// TODO: Alain: TaskEntry.Abort()
// Should only call Tasks.Remove() and the Thread.Abort() should be managed by it if the Entry is 'Running'
// => internal -> public (?)
// => Add a property 'CurrentThread' n this class

// Idee2: Pas utiliser de Thread.Abort, mais une variable 'volatile bool Aborted' que les Callbacks devront vérifier de tps en tps.
// => Prévoir des méthodes du genre SetCurrentSqlCommand() -- avec lock(this) -- etc.. pour pouvoir aborter les trucs long en cours
System.Diagnostics.Debug.Fail( "NotImplemented: " + GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "()" );
		}

		public void AddOnExceptionCallback(Action<Exception> callback)
		{
			System.Diagnostics.Debug.Assert( callback != null, "Missing parameter 'callback'" );

			bool callRightNow = false;
			lock( this )
			{
				OnException += callback;

				if( CallbackException != null )
				{
					// The task is already terminated but SetCallbackException() has already been called
					System.Diagnostics.Debug.Assert( Status == Statuses.Disposed, "An exception is already set on this task but its status is not Disposed as it should be" );
					// NB: TaskQueue.TaskThread() first Remove()s the task THEN calls entry.SetCallbackException(). So the status of the entry should always be 'Disposed' if the exception is set.

					// The task is already terminated => call the callback directly
					callRightNow = true;
				}
			}

			if( callRightNow )
			{
				try { callback(CallbackException); }
				catch( System.Exception ex ) { System.Diagnostics.Debug.Fail( "The task's OnException event threw an exception: " + ex.Message ); }
			}
		}

		internal void SetCallbackException(Exception exception)
		{
			System.Diagnostics.Debug.Assert( exception != null, "Missing parameter 'exception'" );
			System.Diagnostics.Debug.Assert( Status == Statuses.Disposed, "The CallbackException is being set but the task is not in Disposed status" );

			CallbackException = exception;

			if( OnException != null )
			{
				try
				{
					OnException( exception );
				}
				catch( System.Exception ex )
				{
					System.Diagnostics.Debug.Fail( "The task's OnException event threw an exception: " + ex.Message );
				}
			}
			else
			{
				// There is nobody to notify about this exception. Last resort:
				System.Diagnostics.Debug.Fail( "The task threw an exception: " + exception.Message );
			}
		}

		public void AddOnTerminatedCallback(Action callback)
		{
			bool callRightNow = false;
			lock( this )
			{
				if( Status == Statuses.Disposed )
					// The task is already terminated => call the callback directly
					callRightNow = true;
				OnTerminated += callback;
			}

			if( callRightNow )
			{
				try { callback(); }
				catch( System.Exception ex ) { System.Diagnostics.Debug.Fail( "The task's OnTerminated event threw an exception: " + ex.Message ); }
			}
		}

		internal void TriggerOnTerminated()
		{
			System.Diagnostics.Debug.Assert( Status == Statuses.Delayed, "OnTerminated event triggered but the task is not in 'Disposed' state" );

			if( OnTerminated != null )
			{
				try { OnTerminated(); }
				catch( System.Exception ex ) { System.Diagnostics.Debug.Fail( "The task's OnTerminated event threw an exception: " + ex.Message ); }
			}
		}
	}
}
