using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

using CommonLibs.Utils.Event;

namespace CommonLibs.WPF.SetupGui.Actions
{
	using ES=CommonLibs.WPF.ExceptionShield;

	public class ActionEntry
	{
		public enum Statuses
		{
			Undefined,		// Default
			Pending,
			Running,
			Finished
		}

		internal enum ActionTypes
		{
			Undefined,		// Default
			Regular,
			Background,
			External,
			ExternalChild
		}
		
		private ActionDelegate					Action;
		public ActionsManager					ActionsManager			{ get; private set; }
		public string							Creator					{ get; private set; }
		public string							Name					{ get; private set; }
		internal ActionTypes					ActionType				{ get; private set; }

		public Statuses							Status					{ get { return StatusHelper.Value; } private set { StatusHelper.Value = value; } }
		public ValueHelper<Statuses>			StatusHelper			{ get; private set; }
		/// <summary>Value from 0 to 100</summary>
		public int								Progress				{ get { return ProgressHelper.Value; } private set { ProgressHelper.Value = CheckProgress(value); } }
		public ValueHelper<int>					ProgressHelper			{ get; private set; }
		public bool								Success					{ get { return (Status==Statuses.Finished) && (Errors.Count==0) && (Exceptions.Count==0); } }  // NB: No need to ThreadDispatch() since if action has Finished then only the main thread will access this entry
		public bool								HasWarnings				{ get { return ThreadDispatch( ()=>(Warnings.Count > 0) ); } }
		public bool								HasErrors				{ get { return ThreadDispatch( ()=>(Errors.Count > 0) ); } }
		public bool								HasExceptions			{ get { return ThreadDispatch( ()=>(Exceptions.Count > 0) ); } }

		private StringBuilder					Logs					= new StringBuilder();
		private List<string>					Errors					= new List<string>();
		private List<string>					Warnings				= new List<string>();
		private List<Exception>					Exceptions				= new List<Exception>();

		#region Used when this is a Background ActionEntry
		private BackgroundWorker				WorkerThread			= null;
		#endregion

		#region Used when this is a External or ExternalChild ActionEntry
		private System.Reflection.MethodInfo	ExternalMethodInfo		= null;
		private ActionEntryExternalHelper		ExternalHelper			= null;
		#endregion

		public ActionEntry(ActionsManager actionsManager, string creator, string name)
		{
			ActionsManager = actionsManager;
			Creator = creator;
			Name = name;
			StatusHelper = new ValueHelper<Statuses>();
			Status = Statuses.Pending;
			ProgressHelper = new ValueHelper<int>();
			Progress = 0;
		}

		internal void Init(ActionDelegate action, bool isBackground)
		{
			System.Diagnostics.Debug.Assert( action != null, "Missing parameter 'action'" );
			ActionType = isBackground ? ActionTypes.Background : ActionTypes.Regular;
			Action = action;

			PostInit();
		}

		internal void Init(System.Reflection.MethodInfo externalMethodInfo)
		{
			System.Diagnostics.Debug.Assert( externalMethodInfo != null, "Missing parameter 'externalMethodInfo'" );
			ActionType = ActionTypes.External;
			ExternalMethodInfo = externalMethodInfo;

			PostInit();
		}

		/// <summary>
		/// Used only by ActionEntryExternalHelper when running from a child process
		/// </summary>
		internal void Init(ActionEntryExternalHelper externalHelper)
		{
			System.Diagnostics.Debug.Assert( externalHelper != null, "Missing parameter 'externalHelper'" );
			ActionType = ActionTypes.ExternalChild;
			ExternalHelper = externalHelper;

			PostInit();
		}

		private void PostInit()
		{
			switch( ActionType )
			{
				case ActionTypes.Background:
				case ActionTypes.External:
					if( ActionsManager.Dispatcher == null )
						throw new ArgumentException( "Cration of Background or External is prohibited when the ActionManager does not have a Dispatcher set" );
					break;
			}
		}

		public void LaunchSubAction(ActionDelegate action)
		{
			try
			{
				action( this );
			}
			catch( System.Exception ex )
			{
				AddException( ex );
			}
		}

		public void LogLine(string line, int progressPercentage)
		{
			LogLine( line );
			UpdateProgress( progressPercentage );
		}

		public void LogLine(string line)
		{
			switch( ActionType )
			{
				case ActionTypes.Regular:
				case ActionTypes.Background:
				case ActionTypes.External:
					ThreadDispatch( ()=>{ Logs.AppendLine(line); } );
					break;

				case ActionTypes.ExternalChild:
					System.Diagnostics.Debug.Assert( ExternalHelper != null, "The ExternalHelper property should have been set in Init(ActionEntryExternalHelper)" );
					ExternalHelper.SendLogLine( line );
					break;

				default:
					System.Diagnostics.Debug.Fail( "Unknown ActionType '" + ActionType + "'" );
					break;
			}
		}

		/// <summary>
		/// Update the action progress status (typically for background actions)
		/// </summary>
		/// <param name="percentage">Percentage value from 0 to 100</param>
		public void UpdateProgress(int percentage)
		{
			System.Diagnostics.Debug.Assert( percentage >= 0 && percentage <= 100, "Invalid percentage value" );
			switch( ActionType )
			{
				case ActionTypes.Regular:
				case ActionTypes.External:
					Progress = percentage;
					break;

				case ActionTypes.Background:
					if( WorkerThread == null )
					{
						System.Diagnostics.Debug.Fail( "'WorkerThread' is not available!" );
						return;
					}
					WorkerThread.ReportProgress( percentage );
					break;

				case ActionTypes.ExternalChild:
					System.Diagnostics.Debug.Assert( ExternalHelper != null, "The ExternalHelper property should have been set in Init(ActionEntryExternalHelper)" );
					ExternalHelper.SendUpdateProgress( percentage );
					break;

				default:
					System.Diagnostics.Debug.Fail( "Unknown ActionType '" + ActionType + "'" );
					break;
			}
		}

		public void Abort()
		{
			//System.Diagnostics.Debug.Assert( IsBackground, "Abort() is supposed to be called only on background actions." );
			// TODO: Alain: Abort an ActionEntry (WorkerThread.WorkerSupportsCancellation = true;)
			// NYI
		}

		public void AddError(string message)
		{
			switch( ActionType )
			{
				case ActionTypes.Regular:
				case ActionTypes.External:
					Errors.Add( message );
					LogLine( "*** Error: " + message );
					break;
				case ActionTypes.Background:
					ThreadDispatch( ()=>
						{
							Errors.Add( message );
							LogLine( "*** Error: " + message );
						} );
					break;
				case ActionTypes.ExternalChild:
					System.Diagnostics.Debug.Assert( ExternalHelper != null, "The ExternalHelper property should have been set in Init(ActionEntryExternalHelper)" );
					ExternalHelper.SendAddError( message );
					break;
				default:
					System.Diagnostics.Debug.Fail( "Unknown ActionType '" + ActionType + "'" );
					break;
			}
		}

		public void AddWarning(string message)
		{
			switch( ActionType )
			{
				case ActionTypes.Regular:
				case ActionTypes.External:
					Warnings.Add( message );
					break;
				case ActionTypes.Background:
					ThreadDispatch( ()=>
						{
							Warnings.Add( message );
						} );
					break;
				case ActionTypes.ExternalChild:
					System.Diagnostics.Debug.Assert( ExternalHelper != null, "The ExternalHelper property should have been set in Init(ActionEntryExternalHelper)" );
					ExternalHelper.SendAddWarning( message );
					break;
				default:
					System.Diagnostics.Debug.Fail( "Unknown ActionType '" + ActionType + "'" );
					break;
			}
		}

		public void AddException(Exception ex)
		{
			ThreadDispatch( ()=>
				{
					System.Diagnostics.Debug.Assert( ex != null, "Missing parameter 'ex'" );
					Exceptions.Add( ex );
					LogLine( "*** Exception (" + ex.GetType().FullName + "): " + ex.Message );
				} );
		}

		public string GetLogs()
		{
			return ThreadDispatch( ()=>Logs.ToString() );
		}

		public string[] GetWarnings()
		{
			return ThreadDispatch( ()=>Warnings.ToArray() );
		}

		public string[] GetErrors()
		{
			return ThreadDispatch( ()=>Errors.ToArray() );
		}

		public Exception[] GetExceptions()
		{
			return ThreadDispatch( ()=>Exceptions.ToArray() );
		}

		private int CheckProgress(int progress)
		{
			progress = (progress > 100 ? 100 : progress);
			return (progress < 0 ? 0 : progress);
		}

		internal void Run()
		{
			System.Diagnostics.Debug.Assert( Status == Statuses.Pending, "Running an ActionEntry that has already been run" );

			Status = Statuses.Running;
			Progress = 0;
			switch( ActionType )
			{
				case ActionTypes.Regular:
					try
					{
						Action(this);
					}
					catch( System.Exception ex )
					{
						AddException( ex );
					}
					finally
					{
						try { Progress = 100; } catch ( System.Exception ex ) { System.Diagnostics.Debug.Fail( "Setting of 'Progress' property failed: " + ex.Message ); }
						try { Status = Statuses.Finished; } catch( System.Exception ex ) { System.Diagnostics.Debug.Fail( "Setting of 'Status' property failed: " + ex.Message ); }
					}
					break;

				case ActionTypes.Background:
					System.Diagnostics.Debug.Assert( WorkerThread == null, "'WorkerThread' is not null" );
					WorkerThread = new BackgroundWorker();
					WorkerThread.DoWork += (sender,e)=>
						{
							Action( this );
						};
					WorkerThread.WorkerReportsProgress = true;
					WorkerThread.ProgressChanged += (sender,e)=>
						{
							System.Diagnostics.Debug.Assert( e.ProgressPercentage >= 0 && e.ProgressPercentage <= 100, "Invalid percentage value (should be 0-100)" );
							Progress = e.ProgressPercentage;
						};
					WorkerThread.RunWorkerCompleted += (sender,e)=>
						{
							if( e.Error != null )
								AddException( e.Error );
							try { Progress = 100; } catch ( System.Exception ex ) { System.Diagnostics.Debug.Fail( "Setting of 'Progress' property failed: " + ex.Message ); }
							try { Status = Statuses.Finished; } catch( System.Exception ex ) { System.Diagnostics.Debug.Fail( "Setting of 'Status' property failed: " + ex.Message ); }
							WorkerThread.Dispose();
							WorkerThread = null;
						};
					WorkerThread.RunWorkerAsync();
					break;

				case ActionTypes.External:
					System.Diagnostics.Debug.Assert( ExternalMethodInfo != null, "Parameter 'ExternalMethodInfo' should have been set in Init()" );
					try
					{
						ExternalHelper = new ActionEntryExternalHelper( this, ExternalMethodInfo );
					}
					catch( System.Exception exception )
					{
						try { AddException( exception ); } catch ( System.Exception ex ) { System.Diagnostics.Debug.Fail( "AddException() failed: " + ex.Message ); }
						try { Progress = 100; } catch ( System.Exception ex ) { System.Diagnostics.Debug.Fail( "Setting of 'Progress' property failed: " + ex.Message ); }
						try { Status = Statuses.Finished; } catch( System.Exception ex ) { System.Diagnostics.Debug.Fail( "Setting of 'Status' property failed: " + ex.Message ); }
					}
					break;

				default:
					throw new NotImplementedException( "Unsupported ActionType '" + ActionType + "'"  );
			}
		}

		/// <summary>
		/// Called by the ExternalHelper to declare that the child process terminated
		/// </summary>
		internal void ExternalProcessEnded(int exitCode)
		{
			try { LogLine( "*** Child process terminated with exit code " + exitCode ); } catch ( System.Exception ex ) { System.Diagnostics.Debug.Fail( "LogLine() failed: " + ex.Message ); }
			try { Progress = 100; } catch ( System.Exception ex ) { System.Diagnostics.Debug.Fail( "Setting of 'Progress' property failed: " + ex.Message ); }
			try { Status = Statuses.Finished; } catch( System.Exception ex ) { System.Diagnostics.Debug.Fail( "Setting of 'Status' property failed: " + ex.Message ); }
		}

		/// <summary>
		/// Executes an Action (asynchroneously) inside the main thread
		/// </summary>
		public void ThreadDispatch(Action action)
		{
			switch( ActionType )
			{
				case ActionTypes.Regular:
				case ActionTypes.ExternalChild:  // ActionManager is null
					System.Diagnostics.Debug.Assert( (ActionsManager == null) || (ActionsManager.Dispatcher.CheckAccess()), "Here, we are supposed to be in the main thread..." );
					action();
					return;
				case ActionTypes.External:
				case ActionTypes.Background:
					System.Diagnostics.Debug.Assert( (ActionsManager != null) && (ActionsManager.Dispatcher != null), "Creating ActionEntries without Dispatcher should have been prohibited" );
					if( ActionsManager.Dispatcher.CheckAccess() )
						action();
					else
						ActionsManager.Dispatcher.BeginInvoke( new Action( ()=>ES.E(action) ), null );
					return;
				default:
					throw new NotImplementedException( "Unsupported ActionType '" + ActionType.ToString() + "'" );
			}
		}

		/// <summary>
		/// Executes a Func (synchroneously) inside the main thread
		/// </summary>
		public T ThreadDispatch<T>(Func<T> func)
		{
			switch( ActionType )
			{
				case ActionTypes.Regular:
				case ActionTypes.ExternalChild:  // ActionManager is null
					System.Diagnostics.Debug.Assert( (ActionsManager == null) || (ActionsManager.Dispatcher.CheckAccess()), "Here, we are supposed to be in the main thread..." );
					return func();
				case ActionTypes.External:
				case ActionTypes.Background:
					System.Diagnostics.Debug.Assert( (ActionsManager != null) && (ActionsManager.Dispatcher != null), "Creating ActionEntries without Dispatcher should have been prohibited" );
					if( ActionsManager.Dispatcher.CheckAccess() )
						return func();
					else
// TODO: Alain: try/catch? Essayer de lancer une exception dans la func pour vérifier qu'elle est propagée dans le thread appelant
						return (T)ActionsManager.Dispatcher.Invoke( func, null );
				default:
					throw new NotImplementedException( "Unsupported ActionType '" + ActionType.ToString() + "'" );
			}
		}
	}
}
