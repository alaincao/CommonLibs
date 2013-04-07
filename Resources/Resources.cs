//
// CommonLibs/Resources/Resources.cs
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
using System.Windows.Media.Imaging;

namespace CommonLibs.Resources
{
	public static class Common
	{
		public static BitmapImage		ImagePause;
		public static BitmapImage		ImagePlay;
		public static BitmapImage		ImageCheckMark;
		public static BitmapImage		ImageInfo;
		public static BitmapImage		ImageError;

		static Common()
		{
			System.Diagnostics.Debug.Assert( UriParser.IsKnownScheme("pack"), "This method must be called after the initialization of any WPF UserControls to initialize URI stuffs..." );

			var assemblyName = typeof(Common).Assembly.FullName;
			string uriPrefix = "pack://application:,,,/" + assemblyName + ";component/Resources/";
			ImagePause =		new BitmapImage( new Uri(uriPrefix + "127166-simple-black-square-icon-media-a-media27-pause-sign.png") );
			ImagePlay =			new BitmapImage( new Uri(uriPrefix + "126552-simple-black-square-icon-arrows-triangle-solid-right.png") );
			ImageCheckMark =	new BitmapImage( new Uri(uriPrefix + "019227-green-jelly-icon-symbols-shapes-check-mark5-ps.png") );
			ImageInfo =			new BitmapImage( new Uri(uriPrefix + "070241-firey-orange-jelly-icon-alphanumeric-information2-ps.png") );
			ImageError =		new BitmapImage( new Uri(uriPrefix + "074367-simple-red-glossy-icon-alphanumeric-circled-x.png") );
		}
	}

	public static class WPF_SetupGui_Actions
	{
		public static BitmapImage		ImagePending		{ get { return Common.ImagePause; } }
		public static BitmapImage		ImageRunning		{ get { return Common.ImagePlay; } }
		public static BitmapImage		ImageError			{ get { return Common.ImageError; } }
		public static BitmapImage		ImageWarnings		{ get { return Common.ImageInfo; } }
		public static BitmapImage		ImageSuccess		{ get { return Common.ImageCheckMark; } }
	}

	public static class WPF_SetupGui_UserControls_IISWebApplication
	{
		public static BitmapImage		ImageRunning		{ get { return Common.ImagePlay; } }
		public static BitmapImage		ImageSuccess		{ get { return Common.ImageCheckMark; } }
		public static BitmapImage		ImageError			{ get { return Common.ImageError; } }
	}
}
