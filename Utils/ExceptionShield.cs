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
		public static Action<Exception>		ShowException;

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
