using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;

namespace CommonLibs.Resources
{
	public static class Resources
	{
		public static BitmapImage		ImagePause;
		public static BitmapImage		ImagePlay;
		public static BitmapImage		ImageCheckMark;
		public static BitmapImage		ImageInfo;
		public static BitmapImage		ImageError;

		static Resources()
		{
			System.Diagnostics.Debug.Assert( UriParser.IsKnownScheme("pack"), "This method must be called after the initialization of any WPF UserControls to initialize URI stuffs..." );

			var assemblyName = typeof(Resources).Assembly.FullName;
			string uriPrefix = "pack://application:,,,/" + assemblyName + ";component/Resources/";
			ImagePause =		new BitmapImage( new Uri(uriPrefix + "127166-simple-black-square-icon-media-a-media27-pause-sign.png") );
			ImagePlay =			new BitmapImage( new Uri(uriPrefix + "126552-simple-black-square-icon-arrows-triangle-solid-right.png") );
			ImageCheckMark =	new BitmapImage( new Uri(uriPrefix + "019227-green-jelly-icon-symbols-shapes-check-mark5-ps.png") );
			ImageInfo =			new BitmapImage( new Uri(uriPrefix + "070241-firey-orange-jelly-icon-alphanumeric-information2-ps.png") );
			ImageError =		new BitmapImage( new Uri(uriPrefix + "074367-simple-red-glossy-icon-alphanumeric-circled-x.png") );
		}
	}
}
