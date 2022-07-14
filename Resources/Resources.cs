//
// CommonLibs/Resources/Resources.cs
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
