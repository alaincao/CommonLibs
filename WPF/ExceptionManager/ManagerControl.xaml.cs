//
// CommonLibs/WPF/ExceptionManager/ManagerControl.cs
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
