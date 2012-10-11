using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace CommonLibs.Utils
{
	public static class Security
	{
		[DllImport("user32")]
		public static extern UInt32 SendMessage(IntPtr hWnd, UInt32 msg, UInt32 wParam, UInt32 lParam);

		public static bool IsCurrentUserAdministrator()
		{
			var windowsIdentity = WindowsIdentity.GetCurrent();
			var windowsPrincipal = new WindowsPrincipal( windowsIdentity );
			return windowsPrincipal.IsInRole( WindowsBuiltInRole.Administrator );
		}

		public static void AddShieldToButton(System.Windows.Forms.Button b)
		{
			const int BCM_FIRST = 0x1600; //Normal button
			const int BCM_SETSHIELD = (BCM_FIRST + 0x000C); //Elevated button
			b.FlatStyle = System.Windows.Forms.FlatStyle.System;
			SendMessage( b.Handle, BCM_SETSHIELD, 0, 0xFFFFFFFF );
		}
	}
}
