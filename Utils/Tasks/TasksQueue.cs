//
// CommonLibs/Utils/Tasks/TasksQueue.cs
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
using System.Threading;
using System.Linq;

using CommonLibs.Utils.Event;

namespace CommonLibs.Utils.Tasks
{
	public class TasksQueue : IDisposable
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		/// <summary>Contains only a reference to 1 object declared as volatile</summary>
		/// <remarks>Remove this class when C# allow local variables to be declared as volatile</remarks>
		private class VolatileContainer<T> where T : class { public volatile T Value; }

		public int										MaximumConcurrentTasks		= 10;
		/// <summary>If this property is set to true, the CultureInfo of the thread that calls CreateTask() is copied to all created Threads</summary>
		public bool										TransferCultureInfo			= true;

		private DateTime								Now							{ get { return DateTime.UtcNow; } }

		internal object									LockObject;

		private Dictionary<Guid,TaskEntry>				AllTasks					= new Dictionary<Guid,TaskEntry>();
		private SortedList<DateTime,List<Guid>>			DelayedTasks				= new SortedList<DateTime,List<Guid>>();
		private Queue<Guid>								QueuedTasks					= new Queue<Guid>();
		private HashSet<Guid>							RunningTasks				= new HashSet<Guid>();

		private volatile Timer							CurrentTimer				= null;
		private bool									Disposed					= false;

		public event Action<TaskEntry>					OnEntryRemoved				{ add { onEntryRemoved.Add(value); } remove { onEntryRemoved.Remove(value); } }
		private CallbackList<TaskEntry>					onEntryRemoved				= new CallbackList<TaskEntry>();

		public TasksQueue()
		{
			LOG( "Constructor" );
			LockObject = AllTasks;
		}

		public void Dispose()
		{
			LOG( "Dispose() - Start" );
			try
			{
				TaskEntry[] allEntries;
				lock( LockObject )
				{
					LOG( "Dispose() - Lock aquired" );

					if( Disposed )
					{
						LOG( "Dispose() - Already disposed" );
						return;
					}
					Disposed = true;  // This will prevent any other TaskEntry to be created

					allEntries = AllTasks.Values.ToArray();
				}

				foreach( var entry in allEntries )
				{
					try { entry.Remove(); }
					catch( System.Exception ex ) { FAIL( "TasksEntry.Remove() threw a '" + ex.GetType().FullName + "' exception: " + ex.Message ); }
				}
			}
			catch( System.Exception ex )
			{
				FAIL( "TasksQueue.Dispose() threw a '" + ex.GetType().FullName + "' exception: " + ex.Message );
			}
			LOG( "Dispose() - Exit" );
		}

		/// <summary>
		/// Create a task for immediate execution only if 'MaximumConcurrentTasks' is not reached
		/// </summary>
		/// <returns>
		/// The created task or null if the there are too many tasks currently running
		/// </returns>
		public TaskEntry CreateTaskIfNotBusy(Action<TaskEntry> callback)
		{
			lock( LockObject )
			{
				if( RunningTasks.Count >= MaximumConcurrentTasks )
				{
					return null;
				}
				else
				{
					var time = DateTime.MinValue.ToUniversalTime();
					return CreateTask( time, callback );
				}
			}
		}

		/// <summary>
		/// Create a task to be executed as soon as possible
		/// </summary>
		public TaskEntry CreateTask(Action<TaskEntry> callback)
		{
			var time = DateTime.MinValue.ToUniversalTime();
			return CreateTask( time, callback );
		}

		public TaskEntry CreateTask(TimeSpan timeOut, Action<TaskEntry> callback)
		{
			var time = Now.Add( timeOut );
			return CreateTask( time, callback );
		}

		/// <summary>
		/// Create a task to be executed in the specified amount of time
		/// </summary>
		public TaskEntry CreateTask(int days, int hours, int minutes, int seconds, int milliseconds, Action<TaskEntry> callback)
		{
			var time = Now.Add( new TimeSpan(days, hours, minutes, seconds, milliseconds) );
			return CreateTask( time, callback );
		}

		/// <summary>
		/// The one and only method to add a task to this queue.
		/// </summary>
		private TaskEntry CreateTask(DateTime executionDate, Action<TaskEntry> callback)
		{
			LOG( "CreateTask('" + executionDate + "') - Start" );
			ASSERT( executionDate.Kind == DateTimeKind.Utc, "CreateTimer() called with a non-UTC DateTime" );

			TaskEntry entry;
			lock( LockObject )
			{
				LOG( "CreateTask('" + executionDate + "') - Lock acquired" );
				if( Disposed )
					// Cannot create tasks anymore
					throw new ApplicationException( "This TasksQueue instance is disposed" );

				entry = new TaskEntry( this, executionDate, TaskEntry.Statuses.Delayed, callback );
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

		/// <param name="start">First parameter number included</param>
		/// <param name="end">Last parameter number included</param>
		public void For(int start, int end, Action<TaskEntry,int> action)
		{
			var count = end - start + 1;
			ASSERT( count > 0, "'start' parameter is not less than 'end'" );
			var arguments = new int[ count ];
			for( int i=0; i<count; ++i )
				arguments[ i ] = start + i;
			ForEach( arguments, action );
		}

		public void ForEach<T>(T[] arguments, Action<TaskEntry,T> action)
		{
			ASSERT( arguments != null, "Missing parameter 'arguments'" );
			ASSERT( action != null, "Missing parameter 'action'" );

			// Create arguments.Length TaskEntries
			int count = arguments.Length;
			var resetEvent = new ManualResetEvent( false );
			var entries = new TaskEntry[ count ];
			var exceptionContainer = new VolatileContainer<Exception>{ Value = null };
			for( int i=0; i<arguments.Length; ++i )
			{
				var argument = arguments[ i ];
				entries[ i ] = CreateTask( (entry)=>
					{
						try
						{
							action( entry, argument );
						}
						catch( System.Exception ex )
						{
							lock( exceptionContainer )
							{
								// If this is the first exception we catch ...
								if( exceptionContainer.Value == null )
									// ... save it
									exceptionContainer.Value = ex;
							}
							// Release the main thread now so it can 'Remove()' all remaining TaskEntries
							resetEvent.Set();
						}
						finally
						{
							var newCount = Interlocked.Decrement( ref count );
							if( newCount == 0 )
								// The last TaskEntry has finished
								resetEvent.Set();
						}
					} );
			}

			// Wait for all TaskEntries to exit
			resetEvent.WaitOne( new TimeSpan(0,0,30) );

			// if the resetEvent exited because of an exception ...
			if( exceptionContainer.Value != null )
			{
				// ... discard all remaining TaskEntries (it is the responsibility of the caller to check 'entry.IsRemoved()') ...
				foreach( var entry in entries )
					entry.Remove();
				// ... and rethrow the caught exception
				throw exceptionContainer.Value;
			}
		}

		/// <summary>
		/// The one and only method that can be used to remove a task from this queue.
		/// </summary>
		/// <remarks>Can only be called from the TastEntry itself</remarks>
		internal void Remove(TaskEntry entry)
		{
			LOG( "Remove('" + entry + ") - Start" );
			ASSERT( entry != null, "Missing parameter 'entry'" );

			bool launchChecksAtEnd;
			bool checkDelayedTasks = false;
			bool checkRunningTasks = false;
			lock( LockObject )
			{
				LOG( "Remove('" + entry + ") - Lock acquired" );
				CheckValidity();

				launchChecksAtEnd = Disposed ? false : true;

				entry.GetStatus( (status)=>
					{
						var id = entry.ID;
						switch( status )
						{
							case TaskEntry.Statuses.Removed:
								// Already removed
								// ASSERT( !AllTasks.ContainsKey(id), "The task is declared as Removed but is still in AllTasks" );	<= This can happen when 2 threads are removing the same TaskEntry at the same time => log but don't assert
								LOG( "Remove('" + entry + ") - Is already removed" );
								return;

							case TaskEntry.Statuses.Delayed: {
								// Remove item from 'DelayedTasks'
								LOG( "Remove('" + entry + ") - Is delayed" );
								var executionDate = entry.ExecutionDate;
								var list = DelayedTasks[ executionDate ];
								var rc = list.Remove( id );
								ASSERT( rc, "Task was not in 'DelayedTasks'" );
								if( list.Count == 0 )
								{
									// No more task for this DateTime
									LOG( "Remove('" + entry + ") - No more task for " + executionDate );
									DelayedTasks.Remove( executionDate );
								}

								// The list has changed
								checkDelayedTasks = true;
								break; }

							case TaskEntry.Statuses.Queued: {
								LOG( "Remove('" + entry + ") - Is Queued" );
								// Recreate 'QueuedTasks' without this task's ID
								QueuedTasks = new Queue<Guid>( QueuedTasks.Where( (itemId)=>(itemId != id) ) );

								// The list has changed
								checkRunningTasks = true;
								break; }

							case TaskEntry.Statuses.Running: {
								LOG( "Remove('" + entry + ") - Is Running" );
								var rc = RunningTasks.Remove( id );
								ASSERT( rc, "Task was not in 'RunningTasks'" );

								checkRunningTasks = true;
								break; }

							default:
								throw new NotImplementedException( "Unknown task status '" + status + "'" );
						}

						{
							// Remove the task from 'AllTasks' and set its status to 'Removed'
							LOG( "Remove('" + entry + ") - Removing from AllTasks" );
							var rc = AllTasks.Remove( id );
							ASSERT( rc, "Task was not in 'AllTasks'" );
							entry.SetStatus( TaskEntry.Statuses.Removed );
						}
					} );

				CheckValidity();
			}
			LOG( "Remove('" + entry + ") - Lock released" );

			if( launchChecksAtEnd )
			{
				if( checkDelayedTasks )
					// The 'DelayedTasks' has changed
					CheckDelayedTasks();

				if( checkRunningTasks )
					// The 'RunningTasks' has changed
					CheckRunningTasks();

				LOG( "Remove('" + entry + ") - Invoking " + onEntryRemoved.Count + " callbacks for 'OnEntryRemoved' event" );
				onEntryRemoved.Invoke( entry );
			}

			LOG( "Remove('" + entry + ") - Exit" );
		}

		/// <summary>
		/// Check if there are tasks in DelayedTasks that can be executed right now. If yes, put them in QueuedTasks and run CheckRunningTasks()<br/>
		/// Then, set the CurrentTimer if there remain tasks in DelayedTasks.
		/// </summary>
		private void CheckDelayedTasks()
		{
			LOG( "CheckDelayedTasks() - Start" );

			bool queuedTasksHasChanged = false;
			lock( LockObject )
			{
				LOG( "CheckDelayedTasks() - Lock acquired" );
				CheckValidity();

				var now = Now;
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

					ASSERT( DelayedTasks.Count == 0, "Logic error: topItem is supposed to be null only when 'DelayedTasks' is empty" );
					if( CurrentTimer != null )
					{
						// There was a timer running => Remove it
						LOG( "CheckDelayedTasks() - Removing CurrentTimer" );
						CurrentTimer.Dispose();
						CurrentTimer = null;
					}
				}
				else  // topItem != null
				{
					LOG( "CheckDelayedTasks() - There is still at least 1 DelayedTask" );

					ASSERT( DelayedTasks.Count > 0, "Logic error: 'topItem' is supposed to be available only when 'DelayedTasks' contains something" );
					ASSERT( topItem.Value.Key >= now, "Logic error: 'topItem' is supposed point to the next task to execute, but it has its DateTime in the past" );
					bool createTimer;
					if( CurrentTimer != null )
					{
						// There is a CurrentTimer running but it does not manage the top DelayedTask => Replace it
						LOG( "CheckDelayedTasks() - Removing CurrentTimer to replace it" );
						CurrentTimer.Dispose();
						CurrentTimer = null;

						createTimer = true;
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
			lock( LockObject )
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
			ASSERT( entry != null, "Missing parameter 'entry'" );

			System.Exception callbackException = null;
			try
			{
				// Copy CultureInfos to this thread
				if( entry.CreatorCultureInfo != null )
					Thread.CurrentThread.CurrentCulture = entry.CreatorCultureInfo;
				if( entry.CreatorUICultureInfo != null )
					Thread.CurrentThread.CurrentUICulture = entry.CreatorUICultureInfo;

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
			entry.Remove();  // NB: If the task has already been manually Remove()d, this won't have any effect
			// entry.Status should now be 'Removed'

			if( callbackException != null )
			{
				// The task threw an exception. Notify it
				LOG( "TaskThread('" + entry + "') - The task threw an exception: " + callbackException );
				entry.SetCallbackException( callbackException );
			}

			LOG( "TaskThread('" + entry + "') - Calling entry.TriggerOnTerminated()" );
			entry.Terminate();

			LOG( "TaskThread('" + entry + "') - Exit" );
		}

		/// <summary>
		/// Check the validity of the lists and properties managed by this object.<br/>
		/// Call this method only inside a lock(this) statement.
		/// </summary>
		/// <remarks>This method is compiled only in debug mode</remarks>
		[System.Diagnostics.Conditional("DEBUG")]
		private void CheckValidity()
		{
			ASSERT( RunningTasks.Count <= MaximumConcurrentTasks, "The MaximumConcurrentTasks has been exceeded" );

			var count = QueuedTasks.Count;
			count += RunningTasks.Count;
			foreach( var entryListItem in DelayedTasks )
			{
				if( entryListItem.Value == null )
				{
					FAIL( "DelayedTasks contains a NULL entry" );
					continue;
				}
				count += entryListItem.Value.Count;
			}
			ASSERT( count == AllTasks.Count, "The entry count in AllTasks does not match the count in all the other lists" );

			foreach( var entryItem in AllTasks )
			{
				if( entryItem.Value == null )
				{
					FAIL( "AllTasks contains a NULL entry" );
					continue;
				}

				ASSERT( entryItem.Value.ExecutionDate.Kind == DateTimeKind.Utc, "AllTasks contains an entry with an ExecutionDate that is not defined as UTC" );

				if( entryItem.Key != entryItem.Value.ID )
					FAIL( "The ID declared in AllTasks and by the entry's ID do not match" );

				var entryStatus = TaskEntry.Statuses.Undefined; entryItem.Value.GetStatus( (status)=>{ entryStatus = status; } );
				switch( entryStatus )
				{
					case TaskEntry.Statuses.Delayed:
						List<Guid> entryList;
						if(! DelayedTasks.TryGetValue(entryItem.Value.ExecutionDate, out entryList) )
						{
							FAIL( "The entry is declared as Delayed but could not be found in DelayedTasks list" );
							break;
						}
						if( entryList == null )
						{
							FAIL( "DelayedTasks contains a null list" );
							break;
						}
						if( entryList.Count == 0 )
						{
							FAIL( "DelayedTasks contains an empty list" );
							break;
						}
						ASSERT( entryList.Contains(entryItem.Key), "The entry is declared as Delayed but could not be found in DelayedTasks list" );
						break;

					case TaskEntry.Statuses.Queued:
						ASSERT( QueuedTasks.Contains(entryItem.Key), "The entry is declared as Queued but could not be found in QueuedTasks list" );
						break;

					case TaskEntry.Statuses.Running:
						ASSERT( RunningTasks.Contains(entryItem.Key), "The entry is declared as Running but could not be found in RunningTasks list" );
						break;

					default:
						FAIL( "The entry is not supposed to be in state '" + entryStatus + "'" );
						break;
				}
			}
		}
	}
}
