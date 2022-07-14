//
// CommonLibs/Utils/Security.cs
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
