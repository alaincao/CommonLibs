using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace CommonLibs.WPF.SetupGui.Actions
{
	using E=CommonLibs.Utils.ExceptionShield;

	public class ActionEntryExternalHelper
	{
		private const string						ARGUMENT					= "/LaunchExternalAction";

		private const string						MSG_METHODTYPE				= "MethodType";
		private const string						MSG_METHODNAME				= "MethodName";
		private const string						MSG_TYPE					= "MessageType";
		private const string						MSG_TYPE_LOG				= "Log";
		private const string						MSG_TYPE_PROGRESS			= "Progress";
		private const string						MSG_VALUE					= "Value";

		private ActionEntry							Entry;

		internal Process							CallerProcess				{ get; private set; }
		private Process								Process;
		// TODO: Alain: Implement child->parent messages
		//private TextReader						StreamIn					= null;
		private TextWriter							StreamOut;

		internal ActionEntryExternalHelper(ActionEntry entry, MethodInfo methodInfo)
		{
			System.Diagnostics.Debug.Assert( entry != null, "Missing parameter 'entry'" );
			System.Diagnostics.Debug.Assert( methodInfo != null, "Missing parameter 'methodInfo'" );
			System.Diagnostics.Debug.Assert( entry != null, "Missing parameter 'entry'" );

			Entry = entry;
			CallerProcess = Process.GetCurrentProcess();

			var procInfo = new System.Diagnostics.ProcessStartInfo();
			procInfo.UseShellExecute = false;
			//procInfo.RedirectStandardError = true;
			procInfo.RedirectStandardOutput = true;
			procInfo.RedirectStandardInput = true;
			procInfo.WorkingDirectory = Environment.CurrentDirectory;
			procInfo.FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName.Replace( ".vshost"/*c.f. VisualStudio debugger*/, "" );
			procInfo.Arguments = ARGUMENT;
			procInfo.Verb = "runas";

			Process = new System.Diagnostics.Process();
			Process.StartInfo = procInfo;
			Process.OutputDataReceived += Process_OutputDataReceived;
			var rc = Process.Start();
			System.Diagnostics.Debug.Assert( rc == true, "Process.Start() returned false" );
			StreamOut = Process.StandardInput;
			//StreamIn = Process.StandardOutput;  <= Using async reads with BeginOutputReadLine() instead
			Process.BeginOutputReadLine();

			// Send the method to execute
			var msg = new Dictionary<string,object>();
			msg[ MSG_METHODTYPE ] = methodInfo.DeclaringType.AssemblyQualifiedName;
			msg[ MSG_METHODNAME ] = methodInfo.Name;
			SendMessage( msg );
		}

		/// <summary>
		/// Used only by ParseArguments()
		/// </summary>
		private ActionEntryExternalHelper()
		{
			Entry = new ActionEntry( null, null, null );
			Entry.Init( this );
			StreamOut = Console.Out;
		}

		/// <summary>
		/// This is the method called by main() to check if this process must launch external actions
		/// </summary>
		/// <returns>True if external actions were processed</returns>
		public static bool ParseCommandLineArguments(string[] arguments)
		{
			if( arguments == null || arguments.Length != 1 )
				return false;
			if( arguments[0] != ARGUMENT )
				return false;

			var helper = new ActionEntryExternalHelper();

			try
			{
				helper.Entry.LogLine( "Retreiving the method to execute" );

				var msg = ParseMessage( Console.In.ReadLine() );
				var methodType = (string)msg[ MSG_METHODTYPE ];
				var methodName = (string)msg[ MSG_METHODNAME ];
				helper.Entry.LogLine( "Method name: " + methodName );
				helper.Entry.LogLine( "From type: " + methodType );
				var type = Type.GetType( methodType );
				var methodInfo = type.GetMethod( methodName );

				helper.Entry.LogLine( "Invoking method...\n\n" );
				methodInfo.Invoke( null, new object[]{ null } );
			}
			catch( System.Exception exception )
			{
				E.E( ()=>helper.Entry.AddException(exception) );
			}
			return true;
		}

		/// <summary>
		/// Process lines sent by the child process through its stdout
		/// </summary>
		private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
		{
			try
			{
				System.Diagnostics.Debug.Assert( Entry != null, "Entry property should have been set in the constructor" );

				string line = e.Data;
				if( line == null )
				{
					// Child process ended
					Entry.ExternalProcessEnded( Process.ExitCode );
					return;
				}

				var msg = ParseMessage( line );
				switch( (string)msg[MSG_TYPE] )
				{
					case MSG_TYPE_LOG:
						Entry.LogLine( (string)msg[MSG_VALUE] );
						break;
					case MSG_TYPE_PROGRESS:
						Entry.UpdateProgress( int.Parse((string)msg[MSG_VALUE]) );
						break;
					case null:
					case "":
						throw new ArgumentException( "Missing message type" );
					default:
						throw new NotImplementedException( "Unknown message type '" + msg[MSG_TYPE] + "'" );
				}
			}
			catch( System.Exception exception )
			{
				E.E( ()=>
					{
						exception = new CommonLibs.ExceptionManager.BaseException( "Error parsing message coming from the child process", exception )
									.AddData( "Message line", e.Data );
						Entry.AddException(exception);
					} );
			}
		}

		internal void SendLogLine(string line)
		{
			var msg = new Dictionary<string,object>();
			msg[ MSG_TYPE ] = MSG_TYPE_LOG;
			msg[ MSG_VALUE ] = line;
			SendMessage( msg );
		}

		internal void SendUpdateProgress(int percentage)
		{
			var msg = new Dictionary<string,object>();
			msg[ MSG_TYPE ] = MSG_TYPE_PROGRESS;
			msg[ MSG_VALUE ] = percentage;
			SendMessage( msg );
		}

		private void SendMessage(IDictionary<string,object> message)
		{
			System.Diagnostics.Debug.Assert( StreamOut != null, "SendMessage() called but no 'StreamOut' defined" );
			System.Diagnostics.Debug.Assert( message != null && message.Count > 0, "Missing parameter 'message'" );

			var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
			var line = serializer.Serialize( message );
			System.Diagnostics.Debug.Assert( !line.Contains('\n'), @"JSon serialized messages are not supposed to contain \n" );
			StreamOut.WriteLine( line );

			StreamOut.Flush();
		}

		private static IDictionary<string,object> ParseMessage(string line)
		{
			System.Diagnostics.Debug.Assert( !string.IsNullOrEmpty(line), "Missing parameter 'line'" );
			System.Diagnostics.Debug.Assert( !line.Contains('\n'), "JSon serialized messages are not supposed to contain \\n" );

			var deserializer = new System.Web.Script.Serialization.JavaScriptSerializer();
			var msg = (IDictionary<string,object>)deserializer.DeserializeObject( line );
			return msg;
		}
	}
}
