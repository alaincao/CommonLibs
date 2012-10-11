using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace CommonLibs.WPF
{
	public static class ExceptionShield
	{
		public static void E(Action action)
		{
			try
			{
				action();
			}
			catch( System.Exception exception )
			{
				CommonLibs.Utils.ExceptionShield.ShowException( exception );
			}
		}

		public static Action Action(Action action)
		{
			return new Action( ()=>
				{
					try
					{
						action();
					}
					catch( System.Exception exception )
					{
						CommonLibs.Utils.ExceptionShield.ShowException( exception );
					}
				} );
		}

		public static RoutedEventHandler Routed(Action action)
		{
			return new RoutedEventHandler( (sender,e)=>
				{
					try
					{
						action();
					}
					catch( System.Exception exception )
					{
						CommonLibs.Utils.ExceptionShield.ShowException( exception );
					}
				} );
		}

		public static TextChangedEventHandler TextChanged(Action action)
		{
			return new TextChangedEventHandler( (sender,e)=>
				{
					try
					{
						action();
					}
					catch( System.Exception exception )
					{
						CommonLibs.Utils.ExceptionShield.ShowException( exception );
					}
				} );
		}

		public static SelectionChangedEventHandler SelectionChanged(Action action)
		{
			return new SelectionChangedEventHandler( (sender,e)=>
				{
					try
					{
						action();
					}
					catch( System.Exception exception )
					{
						CommonLibs.Utils.ExceptionShield.ShowException( exception );
					}
				} );
		}
	}
}
