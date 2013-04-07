//
// CommonLibs/WPF/SetupGui/Modules/Logs.cs
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
