//
// CommonLibs/Utils/ExceptionShield.cs
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
