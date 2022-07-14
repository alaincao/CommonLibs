//
// CommonLibs/WPF/ExceptionShield.cs
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
