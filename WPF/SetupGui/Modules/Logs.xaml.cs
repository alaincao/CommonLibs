//
// CommonLibs/WPF/SetupGui/Modules/Logs.cs
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

			ActionsManager = new ActionsManager( "" )  { Dispatcher = this.Dispatcher };
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
