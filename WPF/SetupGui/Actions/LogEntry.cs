//
// CommonLibs/WPF/SetupGui/Actions/LogEntry.cs
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
using System.Windows.Media.Imaging;

namespace CommonLibs.WPF.SetupGui.Actions
{
	/// <summary>
	/// Class used in the Visual Studio Designer as content example of LogEntries
	/// </summary>
	public class LogEntrySample
	{
		public string					Creator				{ get { return "<Creator>"; } }
		public string					Name				{ get { return "<Name>"; } }
		public string					LogsText			{ get { return "<LogsTest>\nTest line 1\nTest line 2\nTest line 3"; } }
		public int						Progress			{ get { return 33; } set {} }
		public bool						HasWarnings			{ get { return true; } }
		public bool						HasErrors			{ get { return true; } }
		public bool						HasExceptions		{ get { return true; } }
		public ActionEntry.Statuses		Status				{ get { return ActionEntry.Statuses.Running; } }
		public BitmapImage				StatusImage			{ get { return CommonLibs.Resources.WPF_SetupGui_Actions.ImageRunning; } }
	}

	public class LogEntry : ActionEntry
	{
		public new int			Progress			{ get { return base.Progress; } set {} }
		public Visibility		ProgressVisibility	{ get { return (Progress == 0) || (Progress == 100) ?  Visibility.Collapsed : Visibility.Visible; } }
		public string			LogsText			{ get; private set; }
		public BitmapImage		StatusImage			{ get; private set; }

		internal LogEntry(ActionsManager actionsManager, string creator, string name)
			: base(actionsManager, creator, name)
		{
			StatusHelper.ValueChanged += UpdateStatus;
			ProgressHelper.ValueChanged += UpdateStatus;
			//UpdateStatus();
		}

		private void UpdateStatus()
		{
			LogsText = GetLogs();

			StatusImage = ActionStatusImage.GetBitmapImage( this );
		}
	}
}
