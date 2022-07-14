//
// CommonLibs/WPF/SetupGui/Actions/ActionEntryControl.cs
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

namespace CommonLibs.WPF.SetupGui.Actions
{
	public partial class ActionEntryControl : UserControl
	{
		public ActionEntry		ActionEntry		{ get { return actionEntry; } set { SetActionEntry(value); } }
		private ActionEntry		actionEntry		= null;

		/// <summary>
		/// Callback to use to create instances of CommonLibs.ExceptionManager.Manager
		/// </summary>
		public CommonLibs.ExceptionManager.CreateManagerDelegate	ExceptionManagerFactory		{	get { return imgActionStatus.ExceptionManagerFactory; }
																									set { imgActionStatus.ExceptionManagerFactory = value;} }

		public ActionEntryControl()
		{
			InitializeComponent();

			ActionEntry = null;  // Init controls
			imgActionStatus.DisableClick = true;
		}

		public static void ShowActionEntry(string windowTitle, ActionEntry entry)
		{
			ShowActionEntry( windowTitle, entry, null );
		}

		public static void ShowActionEntry(string windowTitle, ActionEntry entry, CommonLibs.ExceptionManager.CreateManagerDelegate exceptionManagerFactory)
		{
			var window = new Window() { Title=windowTitle };
			window.Width = 640;
			window.Height = 480;
			var control = new ActionEntryControl();
			if( exceptionManagerFactory != null )
				control.ExceptionManagerFactory = exceptionManagerFactory;
			control.ActionEntry = entry;
			window.Content = control;
			var rc = window.ShowDialog();
		}

		private void SetActionEntry(ActionEntry entry)
		{
			if( actionEntry != null )
			{
				// Unlink event handlers
				actionEntry.StatusHelper.ValueChanged -= Update;
				actionEntry.ProgressHelper.ValueChanged -= Update;
			}

			actionEntry = entry;
			imgActionStatus.ActionEntry = entry;

			if( actionEntry != null )
			{
				// Link event handlers
				actionEntry.StatusHelper.ValueChanged += Update;
				actionEntry.ProgressHelper.ValueChanged += Update;
			}

			Update();
		}

		private void Update()
		{
			var entry = ActionEntry;

			if( entry == null )
			{
				// Clear control & exit
				txtCreator.Text = null;
				txtName.Text = null;
				txtStatus.Text = null;
				txtLogs.Text = null;
				rowProgress.Height = new GridLength(0);
				rowExceptions.Height = new GridLength(0);
				return;
			}

			txtCreator.Text = entry.Creator;
			txtName.Text = entry.Name;
			txtStatus.Text = entry.Status.ToString();
			txtLogs.Text = entry.GetLogs();

			var progress = entry.Progress;
			if( (progress != 0) && (progress != 100) )
			{
				// Show & update progress bar
				prgProgress.Value = progress;
				rowProgress.Height = GridLength.Auto;
			}
			else
			{
				// Hide progress bar
				rowProgress.Height = new GridLength(0);
			}

			(new TextRange(docExceptions.ContentStart, docExceptions.ContentEnd)).Text = "";  // Clear exceptions text
			if(! entry.HasExceptions )
			{
				// Hide exceptions Grid row
				rowExceptions.Height = new GridLength(0);
			}
			else
			{
				// Show exceptions Grid row
				rowExceptions.Height = GridLength.Auto;

				var exceptions = entry.GetExceptions();
				for( int i=0; i<exceptions.Length; ++i )
				{
					var exception = exceptions[i];

					int startPosition = docExceptions.ContentStart.GetOffsetToPosition( docExceptions.ContentEnd );

					if( i != 0 )
						docExceptions.ContentEnd.InsertTextInRun( "\n" );

					docExceptions.ContentEnd.InsertTextInRun( exception.Message + " (" + exception.GetType().Name + ")" );
					var link = new Hyperlink( docExceptions.ContentStart.GetPositionAtOffset(startPosition), docExceptions.ContentEnd );
					link.Click += (sender,e)=>
						{
							var manager = ExceptionManagerFactory( exception );
							CommonLibs.WPF.ExceptionManager.ManagerControl.ShowException( "Exception Details", manager );
						};
				}
			}
		}
	}
}
