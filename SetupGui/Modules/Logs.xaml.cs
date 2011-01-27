using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using CommonLibs.Utils.Event;
using CommonLibs.WPF.SetupGui.Actions;

namespace CommonLibs.WPF.SetupGui.Modules
{
	using ES=CommonLibs.WPF.ExceptionShield;

	public partial class Logs : UserControl
	{
		public ModuleObject						ModuleObject		{ get; private set; }
		public ActionsManager					ActionsManager		{ get; private set; }

		private ValueHelper<bool>				CanSaveHelper		= new ValueHelper<bool>();

		/// <summary>
		/// Callback to use to create instances of CommonLibs.ExceptionManager.Manager
		/// </summary>
		public CommonLibs.ExceptionManager.CreateManagerDelegate	ExceptionManagerFactory		{ set { usrActionEntry.ExceptionManagerFactory = value; } }

		public Logs()
		{
			ModuleObject = new ModuleObject( "Logs", this, CanSaveHelper );

			InitializeComponent();

			ActionsManager = new ActionsManager( "" );
			ActionsManager.ActionEntryFactory = (creator,name)=>
				{
					return new LogEntry(ActionsManager, creator, name);
				};
			lstLogs.Items.Clear();
			lstLogs.ItemsSource = ActionsManager.ActionEntries;
			lstLogs.SelectionChanged += ES.SelectionChanged( lstLogs_SelectionChanged );

			ActionsManager.ActionStatusChanged += (actionEntry)=>
				{
					lstLogs.Items.Refresh();
				};
		}

		private void lstLogs_SelectionChanged()
		{
			var actionEntry = (ActionEntry)lstLogs.SelectedItem;
			usrActionEntry.ActionEntry = actionEntry;
		}
	}
}
