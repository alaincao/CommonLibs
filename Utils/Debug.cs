//
// CommonLibs/Utils/Debug.cs
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
	public static class Debug
	{
		public static Action<string,string>			LogHandler					{ get; set; }
		public static Action<string,string>			AssertionFailureHandler		{ get; set; }

		static Debug()
		{
			// Set the default log handler
			#if DEBUG
				LogHandler = (sender,message)=>
					{
						var linePrefix = string.Format( "{0} {1:00} ({2}): ", DateTime.UtcNow.ToString("HH:mm:ss.fffffff"), System.Threading.Thread.CurrentThread.ManagedThreadId, sender );
						foreach( var line in (message ?? "<NULL>").Replace("\r", "").Split(new char[]{'\n'}) )
							System.Diagnostics.Debug.WriteLine( linePrefix + line );
					};

				AssertionFailureHandler = (sender,message)=>
					{
						System.Diagnostics.Debug.WriteLine( "*** ASSERTION FAILED ("+sender+"): "+message );
						System.Diagnostics.Debug.Fail( "" + sender + "\n" + message );
						//System.Diagnostics.Debugger.Break()
					};
			#else
				// In release mode, the LogHandler() and the AssertionFailureHandler() should never be called.
				// But if this happens anyway, discard the calls. NB: The LogHandler and AssertionFailureHandler should NOT be left null anyway!!!
				LogHandler = (sender,message)=>{};
				AssertionFailureHandler = (sender,message)=>{};
			#endif
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void LOG(object sender, string message)
		{
			System.Diagnostics.Debug.Assert( sender != null, "Missing parameter 'sender'" );
			LOG( sender.GetType(), message );
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void LOG(Type type, string message)
		{
			System.Diagnostics.Debug.Assert( type != null, "Missing parameter 'type'" );
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(message), "Missing parameter 'message'" );
			LogHandler( type.FullName, message );
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void LOG(System.Reflection.MethodInfo method, string message)
		{
			System.Diagnostics.Debug.Assert( method != null, "Missing parameter 'method'" );
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(message), "Missing parameter 'message'" );

			string type = "";
			if( method != null )
				type = "" + method.Name;

			LogHandler( type, message );
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void ASSERT(bool test, object sender, string message)
		{
			System.Diagnostics.Debug.Assert( sender != null, "Missing parameter 'sender'" );
			ASSERT( test, sender.GetType(), message );
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void ASSERT(bool test, Type type, string message)
		{
			System.Diagnostics.Debug.Assert( type != null, "Missing parameter 'type'" );
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(message), "Missing parameter 'message'" );
			if( test )
				return;

			var sender = (type == null) ? "" : type.FullName;
			AssertionFailureHandler( sender, message );
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public static void ASSERT(bool test, System.Reflection.MethodInfo method, string message)
		{
			System.Diagnostics.Debug.Assert( message != null, "Missing parameter 'message'" );
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(message), "Missing parameter 'message'" );
			if( test )
				return;

			var sender = (method == null) ? "" : method.Name;
			AssertionFailureHandler( sender, message );
		}

		public static void DefaultFatalExceptionHandler(object sender, string description, Exception exception)
		{
			System.Diagnostics.Debug.Assert( sender != null, "Missing parameter 'sender'" );
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(description), "Missing parameter 'description'" );
			System.Diagnostics.Debug.Assert( exception != null, "Missing parameter 'exception'" );

			try
			{
				description = (!string.IsNullOrEmpty(description)) ? description : "<EMPTY>";
				string exceptionType;
				string exceptionMessage;
				string stackTrace;
				if( exception != null )
				{
					exceptionType = exception.GetType().FullName;
					exceptionMessage = exception.Message;
					stackTrace = "\n" + exception.StackTrace;
				}
				else
				{
					exceptionType = "<NULL>";
					exceptionMessage = "<NULL>";
					stackTrace = "";
				}
				string message = "*** Exception (" + exceptionType + "): " + description + ": " + exceptionMessage + stackTrace;
				System.Diagnostics.Debug.Fail( message );
				LOG( sender, message );
			}
			catch( System.Exception ex )
			{
				System.Diagnostics.Debug.Fail( "FATAL ERROR: FATAL EXCEPTION HANDLER IS NOT SUPPOSED TO THROW AN EXCEPTION!!!: " + ex.Message );
			}
		}
	}
}
