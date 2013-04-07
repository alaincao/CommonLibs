//
// CommonLibs/WPF/ExceptionManager/ManagerControl.cs
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
using System.IO;
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

using CommonLibs.ExceptionManager;

namespace CommonLibs.WPF.ExceptionManager
{
	public partial class ManagerControl : UserControl
	{
		public Manager					ExceptionManager		{ get { return exceptionManager; } set { SetManager(value); } }
		private Manager					exceptionManager		= null;

		public ManagerControl()
		{
			InitializeComponent();
		}

		public static void ShowException(string windowTitle, CommonLibs.ExceptionManager.Manager manager)
		{
			var window = new Window() { Title=windowTitle };
			window.Width = 640;
			window.Height = 480;
			var managerControl = new ManagerControl() { ExceptionManager=manager };
			window.Content = managerControl;
			var rc = window.ShowDialog();
		}

		private void SetManager(Manager manager)
		{
			exceptionManager = manager;
			string text;
			if( manager == null )
				text = "";
			else
				text = CommonLibs.ExceptionManager.TextWriter.GetText( manager );
			//var textStream = new MemoryStream( (new UTF8Encoding()).GetBytes(text) );
			//var textRange = new TextRange( txtDocument.ContentStart, txtDocument.ContentEnd );
			//textRange.Load( textStream, System.Windows.DataFormats.Text );
			txtText.Text = text;
		}
	}
}
