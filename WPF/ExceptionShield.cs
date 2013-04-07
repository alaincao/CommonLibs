//
// CommonLibs/WPF/ExceptionShield.cs
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
