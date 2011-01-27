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
