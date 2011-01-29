using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Threading;

namespace CommonLibs.WPF.SetupGui.Actions
{
	public delegate void ActionDelegate(ActionEntry entry);

	public class ActionsManager
	{
		protected string							DefaultCreator				{ get; private set; }
		protected bool								HasPendingActions			{ get { return CurrentPosition < ActionEntries.Count; } }

		public ObservableCollection<ActionEntry>	ActionEntries				{ get; private set; }
		private int									CurrentPosition				= 0;

		internal event Action<ActionEntry>			ActionStatusChanged;

		/// <summary>
		/// Callback to use to create instances of ActionEntry
		/// </summary>
		public Func<string,string,ActionEntry>		ActionEntryFactory;

		public Dispatcher							Dispatcher					{ get { return dispatcher; } set { if(dispatcher != null)  throw new ArgumentException("Dispatcher can only be assigned once"); dispatcher = value; } }
		private Dispatcher							dispatcher					= null;

		public ActionsManager(string defaultCreator)
		{
			DefaultCreator = defaultCreator;
			ActionEntries = new ObservableCollection<ActionEntry>();

			ActionEntryFactory = CreateActionEntryDefault;  // Set default callback
		}

		private ActionEntry CreateActionEntryDefault(string creator, string name)
		{
			return new ActionEntry( this, creator, name );
		}

		public ActionEntry AppendAction(string name, ActionDelegate action)						{ return AppendAction( DefaultCreator, name, action, false ); }
		public ActionEntry AppendAction(string name, ActionDelegate action, bool isBackground)	{ return AppendAction( DefaultCreator, name, action, isBackground ); }
		public ActionEntry AppendAction(string creator, string name, ActionDelegate action)		{ return AppendAction( creator, name, action, false ); }
		public ActionEntry AppendAction(string creator, string name, ActionDelegate action, bool isBackground)
		{
			var entry = ActionEntryFactory( creator, name );
			entry.Init( action, isBackground );
			return AppendAction( entry );
		}
		public ActionEntry AppendActionExternal(string creator, string name, System.Reflection.MethodInfo methodInfo)
		{
			var entry = ActionEntryFactory( creator, name );
			entry.Init( methodInfo );
			return AppendAction( entry );
		}
		private ActionEntry AppendAction(ActionEntry entry)
		{
			ActionEntries.Add( entry );

			entry.StatusHelper.ValueChanged += ()=>
				{
					if( ActionStatusChanged != null )
						ActionStatusChanged( entry );
				};
			entry.ProgressHelper.ValueChanged += ()=>
				{
					if( ActionStatusChanged != null )
						ActionStatusChanged( entry );
				};
			if( ActionStatusChanged != null )
				ActionStatusChanged( entry );

			CheckPendingActions();
			return entry;
		}

		public ActionEntry PushAction(string name, ActionDelegate action)						{ return PushAction( DefaultCreator, name, action, false ); }
		public ActionEntry PushAction(string name, ActionDelegate action, bool isBackground)	{ return PushAction( DefaultCreator, name, action, isBackground ); }
		public ActionEntry PushAction(string creator, string name, ActionDelegate action)		{ return PushAction( creator, name, action, false ); }
		public ActionEntry PushAction(string creator, string name, ActionDelegate action, bool isBackground)
		{
			var entry = ActionEntryFactory( creator, name );
			entry.Init( action, isBackground );
			ActionEntries.Insert( CurrentPosition, entry );

			entry.StatusHelper.ValueChanged += ()=>
				{
					if( ActionStatusChanged != null )
						ActionStatusChanged( entry );
				};
			entry.ProgressHelper.ValueChanged += ()=>
				{
					if( ActionStatusChanged != null )
						ActionStatusChanged( entry );
				};
			if( ActionStatusChanged != null )
				ActionStatusChanged( entry );

			CheckPendingActions();
			return entry;
		}

		protected void Run()
		{
			while( RunOne() );
		}

		/// <returns>False if there was no pending action</returns>
		protected bool RunOne()
		{
			if( !HasPendingActions )
				return false;

			var entry = ActionEntries[ CurrentPosition ];
			++ CurrentPosition;

			System.Diagnostics.Debug.Assert( entry.Status == ActionEntry.Statuses.Pending , "The action that is going to be run should be 'Pending'" );

			entry.Run();

			return true;
		}

		private void CheckPendingActions()
		{
			if( HasPendingActions )
// TODO: Alain: This is WPF-specific => find a way to generalize this then move ActionsManager & ActionEntry to namespace "CommonLibs.Actions"
// (? use a callback to put the job in background ?)
				Dispatcher.CurrentDispatcher.BeginInvoke( new Action(Run), DispatcherPriority.ApplicationIdle, null );
		}
	}
}
