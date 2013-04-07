//
// CommonLibs/Utils/Security.cs
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
