using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace CommonLibs.Web.LongPolling.Utils
{
	public class TasksQueue : IDisposable
	{
		private const int								MaximumConcurrentTasks		= 10;

		private Dictionary<Guid,TaskEntry>				AllTasks					= new Dictionary<Guid,TaskEntry>();
		private SortedList<DateTime,List<Guid>>			DelayedTasks				= new SortedList<DateTime,List<Guid>>();
		private Queue<Guid>								QueuedTasks					= new Queue<Guid>();
		private HashSet<Guid>							RunningTasks				= new HashSet<Guid>();

		private volatile Timer							CurrentTimer				= null;
		private volatile object							CurrentTimerDateTime		= null;  // NB: This is a 'DateTime?'. Did not declare it as this because 'DateTime?' cannot be volatile (?!?)

		public TasksQueue()
		{
			LOG( "Constructor" );
		}

		[System.Diagnostics.Conditional("DEBUG")]
		private void LOG(string message)
		{
			ConnectionList.LOG( GetType().FullName, message );
		}

		public void Dispose()
		{
			LOG( "Dispose() - Start" );
			try
			{
				lock( this )
				{
					LOG( "Dispose() - Lock aquired" );

					var allEntries = AllTasks.Values.ToArray();
					foreach( var entry in allEntries )
					{
						try
						{
							Remove( entry, /*launchChecksAtEnd=*/false, /*abortRunningTask=*/true );
						}
						catch( System.Exception ex )
						{
							System.Diagnostics.Debug.Fail( "TasksEntry.Remove() threw a '" + ex.GetType().FullName + "' exception: " + ex.Message );
						}
					}
				}
			}
			catch( System.Exception ex )
			{
				System.Diagnostics.Debug.Fail( "TasksQueue.Dispose() threw a '" + ex.GetType().FullName + "' exception: " + ex.Message );
			}
			LOG( "Dispose() - Exit" );
		}

		/// <summary>
		/// Create a task to be executed as soon as possible
		/// </summary>
		public TaskEntry CreateTask(Action<TaskEntry> callback)
		{
			var time = DateTime.MinValue.ToUniversalTime();
			return CreateTask( time, callback );
		}

		/// <summary>
		/// Create a task to be executed in the specified amount of time
		/// </summary>
		public TaskEntry CreateTask(int days, int hours, int minutes, int seconds, int milliseconds, Action<TaskEntry> callback)
		{
			var time = DateTime.UtcNow.Add( new TimeSpan(days, hours, minutes, seconds, milliseconds) );
			return CreateTask( time, callback );
		}

		/// <summary>
		/// The one and only method to add a task to this queue.
		/// </summary>
		private TaskEntry CreateTask(DateTime executionDate, Action<TaskEntry> callback)
		{
			LOG( "CreateTask('" + executionDate + "') - Start" );
			System.Diagnostics.Debug.Assert( executionDate.Kind == DateTimeKind.Utc, "CreateTimer() called with a non-UTC DateTime" );
			var now = DateTime.UtcNow;

			var entry = new TaskEntry( this, executionDate, TaskEntry.Statuses.Delayed, callback );

			lock( this )
			{
				LOG( "CreateTask('" + executionDate + "') - Lock acquired" );
				CheckValidity();

				List<Guid> list;
				if(! DelayedTasks.TryGetValue(entry.ExecutionDate, out list) )
				{
					list = new List<Guid>();
					DelayedTasks.Add( entry.ExecutionDate, list );
				}
				list.Add( entry.ID );

				AllTasks.Add( entry.ID, entry );

				CheckValidity();
			}
			CheckDelayedTasks();

			LOG( "CreateTask('" + executionDate + "') - Exit" );
			return entry;
		}

		internal void Remove(TaskEntry entry)
		{
			Remove( entry, /*launchChecksAtEnd=*/true, /*abortRunningTask=*/true );
		}

		/// <summary>
		/// The one and only method to remove a task from this queue.
		/// </summary>
		private void Remove(TaskEntry entry, bool launchChecksAtEnd, bool abortRunningTask)
		{
			LOG( "Remove('" + entry + "', " + launchChecksAtEnd + ", " + abortRunningTask + ") - Start" );
			System.Diagnostics.Debug.Assert( entry != null, "Missing parameter 'entry'" );

			bool checkDelayedTasks = false;
			bool checkRunningTasks = false;
			lock( this )
			{
				LOG( "Remove('" + entry + "', " + launchChecksAtEnd + ", " + abortRunningTask + ") - Lock acquired" );
				CheckValidity();

				entry.GetStatus( (status)=>
					{
						var id = entry.ID;
						switch( status )
						{
							case TaskEntry.Statuses.Disposed:
								// Already removed
								System.Diagnostics.Debug.Assert( !AllTasks.ContainsKey(id), "The task is declared as Disposed but is still in AllTasks" );
								LOG( "Remove('" + entry + "', " + launchChecksAtEnd + ", " + abortRunningTask + ") - Is already removed" );
								return;

							case TaskEntry.Statuses.Delayed: {
								// Remove item from 'DelayedTasks'
								LOG( "Remove('" + entry + "', " + launchChecksAtEnd + ", " + abortRunningTask + ") - Is delayed" );
								var executionDate = entry.ExecutionDate;
								var list = DelayedTasks[ executionDate ];
								var rc = list.Remove( id );
								System.Diagnostics.Debug.Assert( rc, "Task was not in 'DelayedTasks'" );
								if( list.Count == 0 )
								{
									// No more task for this DateTime
									LOG( "Remove('" + entry + "', " + launchChecksAtEnd + ", " + abortRunningTask + ") - No more task for " + executionDate );
									DelayedTasks.Remove( executionDate );
								}

								// The list has changed
								checkDelayedTasks = true;
								break; }

							case TaskEntry.Statuses.Queued: {
								LOG( "Remove('" + entry + "', " + launchChecksAtEnd + ", " + abortRunningTask + ") - Is Queued" );
								// Recreate 'QueuedTasks' without this task's ID
								QueuedTasks = new Queue<Guid>( QueuedTasks.Where( (itemId)=>(itemId != id) ) );

								// The list has changed
								checkRunningTasks = true;
								break; }

							case TaskEntry.Statuses.Running: {
								LOG( "Remove('" + entry + "', " + launchChecksAtEnd + ", " + abortRunningTask + ") - Is Running" );
								// Abort() the task and remove it from 'RunningTasks'
								if( abortRunningTask )
								{
// TODO: Alain: Beware of recursive Abort() calls id Abort calls Remove() ...
									LOG( "Remove('" + entry + "', " + launchChecksAtEnd + ", " + abortRunningTask + ") - Aborting task" );
									entry.Abort();
								}
								var rc = RunningTasks.Remove( id );
								System.Diagnostics.Debug.Assert( rc, "Task was not in 'RunningTasks'" );

								checkRunningTasks = true;
								break; }

							default:
								throw new NotImplementedException( "Unknown task status '" + status + "'" );
						}

						{
							// Remove the task from 'AllTasks' and set its status to 'Disposed'
							LOG( "Remove('" + entry + "', " + launchChecksAtEnd + ", " + abortRunningTask + ") - Removing from AllTasks" );
							var rc = AllTasks.Remove( id );
							System.Diagnostics.Debug.Assert( rc, "Task was not in 'AllTasks'" );
							entry.SetStatus( TaskEntry.Statuses.Disposed );
						}
					} );

				CheckValidity();
			}
			LOG( "Remove('" + entry + "', " + launchChecksAtEnd + ", " + abortRunningTask + ") - Lock released" );

			if( launchChecksAtEnd )
			{
				if( checkDelayedTasks )
					// The 'DelayedTasks' has changed
					CheckDelayedTasks();

				if( checkRunningTasks )
					// The 'RunningTasks' has changed
					CheckRunningTasks();
			}
			LOG( "Remove('" + entry + "', " + launchChecksAtEnd + ", " + abortRunningTask + ") - Exit" );
		}

		/// <summary>
		/// Check if there are tasks in DelayedTasks that can be executed right now. If yes, put them in QueuedTasks and run CheckRunningTasks()<br/>
		/// Then, set the CurrentTimer if there remain tasks in DelayedTasks.
		/// </summary>
		private void CheckDelayedTasks()
		{
			LOG( "CheckDelayedTasks() - Start" );

			bool queuedTasksHasChanged = false;
			lock( this )
			{
				LOG( "CheckDelayedTasks() - Lock acquired" );
				CheckValidity();

				var now = DateTime.UtcNow;
				KeyValuePair<DateTime,List<Guid>>? topItem;
				do
				{
					topItem = (DelayedTasks.Count > 0) ? DelayedTasks.FirstOrDefault() : (KeyValuePair<DateTime,List<Guid>>?)null;
					if( topItem == null )
						// No more delayed task available => Exit loop
						break;

					if( topItem.Value.Key > now )
					{
						// No more executable task available right now
						break;
					}

					// Remove the topItem
					LOG( "CheckDelayedTasks() - Removing from DelayedTasks tasks for " + topItem.Value.Key );
					DelayedTasks.RemoveAt( 0 );

					// Add the tasks in RunningTasks
					foreach( var entryID in topItem.Value.Value )
					{
						LOG( "CheckDelayedTasks() - Adding to QueuedTasks task '" + entryID + "'" );
						var entry = AllTasks[ entryID ];
						QueuedTasks.Enqueue( entryID );
						entry.SetStatus( TaskEntry.Statuses.Queued );
					}

					queuedTasksHasChanged = true;
				}
				while( true );

				if( topItem == null )
				{
					LOG( "CheckDelayedTasks() - No more tasks in DelayedTasks" );

					System.Diagnostics.Debug.Assert( DelayedTasks.Count == 0, "Logic error: topItem is supposed to be null only when 'DelayedTasks' is empty" );
					if( CurrentTimer != null )
					{
						// There was a timer running => Remove it
						LOG( "CheckDelayedTasks() - Removing CurrentTimer" );
						CurrentTimer.Dispose();
						CurrentTimer = null;
						CurrentTimerDateTime = null;
					}
				}
				else  // topItem != null
				{
					LOG( "CheckDelayedTasks() - There is still at least 1 DelayedTask" );

					System.Diagnostics.Debug.Assert( DelayedTasks.Count > 0, "Logic error: 'topItem' is supposed to be available only when 'DelayedTasks' contains something" );
					System.Diagnostics.Debug.Assert( topItem.Value.Key >= now, "Logic error: 'topItem' is supposed point to the next task to execute, but it has its DateTime in the past" );
					bool createTimer;
					if( CurrentTimer != null )
					{
						if( ((DateTime)CurrentTimerDateTime) == topItem.Value.Key )
						{
							// There is already a CurrentTimer running and it is already managing the top DelayedTask => Do nothing
							LOG( "CheckDelayedTasks() - CurrentTimer already running for " + topItem.Value.Key );
							createTimer = false;
						}
						else
						{
							// There is a CurrentTimer running but it does not manage the top DelayedTask => Replace it
							LOG( "CheckDelayedTasks() - Removing CurrentTimer to replace it" );
							CurrentTimer.Dispose();
							CurrentTimer = null;
							CurrentTimerDateTime = null;

							createTimer = true;
						}
					}
					else  // CurrentTimer == null
					{
						// There is no running timer but there are tasks in DelayedTasks => Create it
						createTimer = true;
					}

					if( createTimer )
					{
						var timeSpan = (topItem.Value.Key - now);
						LOG( "CheckDelayedTasks() - Creating timer for " + timeSpan );
						var timer = new Timer( (state)=>{CheckDelayedTasks();}, null, timeSpan, new TimeSpan(-1) );
						CurrentTimer = timer;
						CurrentTimerDateTime = topItem.Value.Key;
					}
				}

				CheckValidity();
			}
			LOG( "CheckDelayedTasks() - Lock released" );

			if( queuedTasksHasChanged )
				// Run the queued tasks if there remain available slots
				CheckRunningTasks();
			LOG( "CheckDelayedTasks() - Exit" );
		}

		/// <summary>
		/// Check if there are tasks in QueuedTasks and the entry count in RunningTasks is less than MaximumConcurrentTasks. If yes, put those tasks in RunningTasks and execute them.
		/// </summary>
		private void CheckRunningTasks()
		{
			LOG( "CheckRunningTasks() - Start" );
			lock( this )
			{
				LOG( "CheckRunningTasks() - Lock acquired" );
				CheckValidity();

				while( true )
				{
					if( RunningTasks.Count >= MaximumConcurrentTasks )
					{
						LOG( "CheckRunningTasks() - No more task slot available" );
						break;
					}

					if( QueuedTasks.Count == 0 )
					{
						LOG( "CheckRunningTasks() - No task queued currently" );
						break;
					}

					var topId = QueuedTasks.Dequeue();
					var entry = AllTasks[ topId ];
					var thread = new Thread( ()=>{ TaskThread(entry); } );

					entry.Thread = thread;
					entry.SetStatus( TaskEntry.Statuses.Running );
					RunningTasks.Add( topId );

					LOG( "CheckRunningTasks() - Starting task " + topId );
					thread.Start();
				}

				CheckValidity();
			}
			LOG( "CheckRunningTasks() - Exit" );
		}

		/// <summary>
		/// Method used as a thread starting point for tasks
		/// </summary>
		private void TaskThread(TaskEntry entry)
		{
			LOG( "TaskThread('" + entry + "') - Start" );
			System.Diagnostics.Debug.Assert( entry != null, "Missing parameter 'entry'" );

			System.Exception callbackException = null;
			try
			{
				entry.Callback( entry );
			}
			catch( System.Exception ex )
			{
				callbackException = ex;
			}

			// The task's thread is terminated. Remove it from this queue.
			// No need to Abort() it => /*abortRunningTask=*/false
			// but 'launchChecksAtEnd=true' will launch CheckRunningTasks() after removing it from 'RunningTasks' (if its status is 'Running' which it still should be...)
			LOG( "TaskThread('" + entry + "') - Removing TaskEntry" );
			Remove( entry, /*launchChecksAtEnd=*/true, /*abortRunningTask=*/false );
			// entry.Status should now be 'Disposed'

			if( callbackException != null )
			{
				// The task threw an exception. Notify it
				LOG( "TaskThread('" + entry + "') - The task threw an exception: " + callbackException );
				entry.SetCallbackException( callbackException );
			}
			LOG( "TaskThread('" + entry + "') - Exit" );
		}

		/// <summary>
		/// Check the validity of the lists and properties managed by this object.<br/>
		/// Call this method only inside a lock(this) statement.
		/// </summary>
		/// <remarks>This method is compiled only in release mode</remarks>
		[System.Diagnostics.Conditional("DEBUG")]
		private void CheckValidity()
		{
			System.Diagnostics.Debug.Assert( (CurrentTimer == null) == (CurrentTimerDateTime == null), "If CurrentTimer is null then CurrentTimerDateTime should be null too ; and if one is not null, the other should be not null too" );
			if( CurrentTimerDateTime != null )
			{
				var currentTimerDateTime = (DateTime)CurrentTimerDateTime;
				System.Diagnostics.Debug.Assert( currentTimerDateTime.Kind == DateTimeKind.Utc, "CurrentTimerDateTime is not defined as UTC" );
			}

			var count = QueuedTasks.Count;
			count += RunningTasks.Count;
			foreach( var entryListItem in DelayedTasks )
			{
				if( entryListItem.Value == null )
				{
					System.Diagnostics.Debug.Fail( "DelayedTasks contains a NULL entry" );
					continue;
				}
				count += entryListItem.Value.Count;
			}
			System.Diagnostics.Debug.Assert( count == AllTasks.Count, "The entry count in AllTasks does not match the count in all the other lists" );

			foreach( var entryItem in AllTasks )
			{
				if( entryItem.Value == null )
				{
					System.Diagnostics.Debug.Fail( "AllTasks contains a NULL entry" );
					continue;
				}

				System.Diagnostics.Debug.Assert( entryItem.Value.ExecutionDate.Kind == DateTimeKind.Utc, "AllTasks contains an entry with an ExecutionDate that is not defined as UTC" );

				if( entryItem.Key != entryItem.Value.ID )
					System.Diagnostics.Debug.Fail( "The ID declared in AllTasks and by the entry's ID do not match" );

				var entryStatus = TaskEntry.Statuses.Undefined; entryItem.Value.GetStatus( (status)=>{ entryStatus = status; } );
				switch( entryStatus )
				{
					case TaskEntry.Statuses.Delayed:
						List<Guid> entryList;
						if(! DelayedTasks.TryGetValue(entryItem.Value.ExecutionDate, out entryList) )
						{
							System.Diagnostics.Debug.Fail( "The entry is declared as Delayed but could not be found in DelayedTasks list" );
							break;
						}
						if( entryList == null )
						{
							System.Diagnostics.Debug.Fail( "DelayedTasks contains a null list" );
							break;
						}
						if( entryList.Count == 0 )
						{
							System.Diagnostics.Debug.Fail( "DelayedTasks contains an empty list" );
							break;
						}
						System.Diagnostics.Debug.Assert( entryList.Contains(entryItem.Key), "The entry is declared as Delayed but could not be found in DelayedTasks list" );
						break;

					case TaskEntry.Statuses.Queued:
						System.Diagnostics.Debug.Assert( QueuedTasks.Contains(entryItem.Key), "The entry is declared as Queued but could not be found in QueuedTasks list" );
						break;

					case TaskEntry.Statuses.Running:
						System.Diagnostics.Debug.Assert( RunningTasks.Contains(entryItem.Key), "The entry is declared as Running but could not be found in RunningTasks list" );
						break;

					default:
						System.Diagnostics.Debug.Fail( "The entry is not supposed to be in state '" + entryStatus + "'" );
						break;
				}
			}
		}
	}
}
