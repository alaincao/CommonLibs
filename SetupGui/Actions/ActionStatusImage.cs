using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CommonLibs.WPF.SetupGui.Actions
{
	public class ActionStatusImage : Image
	{
		public ActionEntry		ActionEntry			{ get { return actionEntry; } set { SetActionEntry(value); } }
		private ActionEntry		actionEntry			= null;
		public bool				DisableClick		{ get; set; }

		private bool			MouseDownWasInside	= false;

		public CommonLibs.ExceptionManager.CreateManagerDelegate	ExceptionManagerFactory;

		public ActionStatusImage()
		{
			ActionEntry = null;
			DisableClick = false;

			// Set default ExceptionManagerFactory
			ExceptionManagerFactory = CommonLibs.ExceptionManager.Manager.DefaultCreateManagerDelegate;
		}

		protected override void OnMouseDown( System.Windows.Input.MouseButtonEventArgs e )
		{
			base.OnMouseDown( e );
			MouseDownWasInside = true;
		}

		protected override void OnMouseLeave( System.Windows.Input.MouseEventArgs e )
		{
			base.OnMouseLeave( e );
			MouseDownWasInside = false;
		}

		protected override void OnMouseUp( System.Windows.Input.MouseButtonEventArgs e )
		{ExceptionShield.E( ()=> {
			base.OnMouseUp( e );

			if(! MouseDownWasInside )
				// This is not a regular click
				return;
			// This is a click

			if( DisableClick )
				return;
			if( ActionEntry == null )
				return;
			if( ActionEntry.Success )
				return;

			var exceptions = ActionEntry.GetExceptions();
			if( ActionEntry.HasExceptions || ActionEntry.HasErrors || ActionEntry.HasWarnings )
			{
				// Show the whole entry since it contains more informations
				ActionEntryControl.ShowActionEntry( "Action details", ActionEntry, ExceptionManagerFactory );
			}
			else
			{
				System.Diagnostics.Debug.Fail( "Should not happen" );
			}
		} ); }

		private void SetActionEntry(ActionEntry entry)
		{
			if( actionEntry != null )
			{
				// Unlink event handlers
				actionEntry.StatusHelper.ValueChanged -= Update;
				actionEntry.ProgressHelper.ValueChanged -= Update;
			}

			actionEntry = entry;

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
				Source = null;
			else
				Source = GetBitmapImage( entry );
		}

		public static BitmapImage GetBitmapImage(ActionEntry actionEntry)
		{
			BitmapImage image;
			switch( actionEntry.Status )
			{
				case ActionEntry.Statuses.Pending:
					image = CommonLibs.Resources.WPF_SetupGui_Actions.ImagePending;
					break;
				case ActionEntry.Statuses.Running:
					image = CommonLibs.Resources.WPF_SetupGui_Actions.ImageRunning;
					break;
				case ActionEntry.Statuses.Finished:
					if( actionEntry.Success )
					{
						if( actionEntry.HasWarnings )
							image = CommonLibs.Resources.WPF_SetupGui_Actions.ImageWarnings;
						else
							image = CommonLibs.Resources.WPF_SetupGui_Actions.ImageSuccess;
					}
					else
					{
						image = CommonLibs.Resources.WPF_SetupGui_Actions.ImageError;
					}
					break;
				default:
					System.Diagnostics.Debug.Fail( "Unsupported entry status '" + actionEntry.Status.ToString() + "'" );
					image = null;
					break;
			}
			return image;
		}
	}
}
