//
// CommonLibs/Utils/ExceptionShield.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2018 Alain CAO
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

namespace CommonLibs.Utils
{
	/// <summary>
	/// Utility class to handle unexpected exceptions.
	/// </summary>
	public static class ExceptionShield
	{
		public static Action<Exception>		ShowException		{ get; set; }

		static ExceptionShield()
		{
			ShowException = ShowExceptionDefault;
		}

		/// <summary>
		/// Exception shield around and Action.<br/>
		/// If an exception is thrown inside the Action, it is catched and ShowException() is called to prevent the whole application from crashing.
		/// </summary>
		public static void E(Action action)
		{
			try
			{
				action();
			}
			catch( System.Exception exception )
			{
				ShowException( exception );
			}
		}

		private static void ShowExceptionDefault(System.Exception exception)
		{
			if( exception == null )
				System.Diagnostics.Debug.Assert( exception != null, "Missing parameter 'exception'" );
			else
				System.Diagnostics.Debug.Fail( "An exception of type '" + exception.GetType().FullName + "' occured: " + exception.Message );
		}
	}
}
