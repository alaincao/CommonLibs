using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommonLibs.Utils;

namespace CommonLibs.Web.LongPolling.CSClient
{
	public enum ConnectionStatus
	{
		Disconnected	= 0,  // Default value
		Connecting,
		Connected,
		Sending,
		Closing
	}

	public abstract class BaseClient
	{
		[System.Diagnostics.Conditional("DEBUG")] internal protected void LOG(string message)				{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] internal protected void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] internal protected void FAIL(string message)				{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		internal protected object				LockObject			= new object();
		public ConnectionStatus					Status				{ get; private set; }
		public string							ConnectionID		{ get; private set; }
		private readonly List<Message>			PendingMessages		= new List<Message>();

		public MessageHandler					MessageHandler		{ get; private set; }
		public System.Net.CookieContainer		Cookies				{ get; private set; }
		public RootMessage						InitMessage			{ get; } = RootMessage.CreateClientInit();

		/// <summary>p1: The new status</summary>
		public event Action<ConnectionStatus>	OnStatusChanged;
		/// <summary>
		/// p1: The error message (not null)<br/>
		/// p2: The exception that triggered the error if any (nullable)
		/// </summary>
		public event Action<string,Exception>	OnInternalError;
		/// <summary>p1: The ConnectionID</summary>
		public event Action<string>				OnConnectionIdReceived;

		/// <returns>The 'ConnectionID' given by the server</returns>
		internal protected abstract Task<string> SendInitMessage();
		internal protected abstract Task MainLoop();
		internal protected abstract Task CloseConnection(string connectionID);
		internal protected abstract Task SendRootMessage(RootMessage rootMessage);

		/// <param name="cookies">Contains the ASP.NET session cookie</param>
		private protected BaseClient(MessageHandler messageHandler, System.Net.CookieContainer cookies)
		{
			ASSERT( messageHandler != null, "Missing parameter 'messageHandler'" );
			ASSERT( cookies != null, "Missing parameter 'cookies'" );

			MessageHandler	= messageHandler;
			Cookies			= cookies;
		}

		public static BaseClient CreateLongPollingClient(MessageHandler messageHandler, System.Net.CookieContainer cookies, string handlerUrl)
		{
			var client = new LongPollingClient( messageHandler, cookies, handlerUrl );
			return client;
		}
		public static BaseClient CreateWebSocketClient(MessageHandler messageHandler, System.Net.CookieContainer cookies, string handlerUrl, string keepaliveUrl=null)
		{
			var client = new WebSocketClient( messageHandler, cookies, handlerUrl, keepaliveUrl );
			return client;
		}

		public async Task<bool> Start()
		{
			LOG( "Start()" );

			lock( LockObject )
			{
				switch( Status )
				{
					case ConnectionStatus.Disconnected:
						// OK
						Status = ConnectionStatus.Connecting;
						break;
					case ConnectionStatus.Connecting:
					case ConnectionStatus.Connected:
					case ConnectionStatus.Sending:
						// Already connect{ing|ed}
						return false;
					case ConnectionStatus.Closing:
						// Invalid state
						throw new ArgumentException( "Cannot start the messaging client while in state 'Closing" );
					default:
						throw new NotImplementedException( "Unknown value '"+Status+"' for property 'Status'" );
				}
			}
			TriggerStatusChanged();

			ASSERT( Status == ConnectionStatus.Connecting, "The 'Status' is supposed to be 'Connecting' here" );

			bool initSent = false;
			try
			{
				ConnectionID = await SendInitMessage();
				TriggerConnectionIdReceived();  // NB: Is NOT supposed to send an exception (try{}catch{} enclosed)
				initSent = true;
			}
			finally
			{
				// Update from 'Connecting' status
				if(! initSent )
				{
					// An exception has occured
					Status = ConnectionStatus.Disconnected;
					TriggerStatusChanged();  // NB: Is NOT supposed to send an exception (try{}catch{} enclosed)
				}
				else  // The last lines of the try/catch block has been reached (no error occured)
				{
					// Continue with execution & invoke MainLoop()
				}
			}
			ASSERT( Status == ConnectionStatus.Connecting, "Property 'Status' is STILL supposed to be 'Connecting' here" );

			Status = ConnectionStatus.Connected;
			TriggerStatusChanged();

			CheckPendingMessages().FireAndForget();

			// Send/receive messages loop
			MainLoop().FireAndForget();  // NB: No await => Run on background tasks threads starting from here
			return true;
		}

		public void Stop()
		{
			LOG( "Stop()" );
			bool launchCloseConnection;
			string connectionID;
			lock( LockObject )
			{
				switch( Status )
				{
					case ConnectionStatus.Disconnected:
						// Already stopped
						return;
					case ConnectionStatus.Connecting:
						TriggerInternalError( "Cannot stop the messaging client while in state 'Connecting'" );
						return;
					case ConnectionStatus.Connected:
						// OK
						Status = ConnectionStatus.Closing;
						launchCloseConnection = true;
						break;
					case ConnectionStatus.Sending:
						// Output socket in use => 'StopAsync()' will be invoked later by 'SendPendingMessages()'
						Status = ConnectionStatus.Closing;
						launchCloseConnection = false;
						break;
					case ConnectionStatus.Closing:
						// Already stopping
						return;
					default:
						throw new NotImplementedException( "Unknown value '"+Status+"' for property 'Status'" );
				}
				ASSERT( Status == ConnectionStatus.Closing, "Property 'Status' is supposed to be 'Closing' here" );
				connectionID = ConnectionID;
				ConnectionID = null;
			}
			TriggerStatusChanged();

			if( launchCloseConnection )
			{
				CloseConnection( connectionID );  // NB: No await so can be performed in background

				lock( LockObject )
				{
					ASSERT( Status == ConnectionStatus.Closing, "Property 'Status' has changed and should not have" );
					Status = ConnectionStatus.Disconnected;
				}
				TriggerStatusChanged();
			}
		}

		public void SendMessage(Message message)
		{
			LOG( "SendMessage()" );
			ASSERT( message != null, "Missing parameter 'message'" );

			lock( LockObject )
			{
				switch( Status )
				{
					case ConnectionStatus.Connecting:
					case ConnectionStatus.Connected:
					case ConnectionStatus.Sending:
						break;
					case ConnectionStatus.Closing:
					case ConnectionStatus.Disconnected:
						throw new CommonException( $"Cannot send message: Connection status is '{Status}'" );
					default:
						throw new NotImplementedException( "Unknown status '"+Status+"'" );
				}

				PendingMessages.Add( message );
			}
			CheckPendingMessages().FireAndForget();
		}

		private async Task CheckPendingMessages()
		{
			LOG( "CheckPendingMessages()" );

			Message[] messages;
			lock( LockObject )
			{
				if( Status != ConnectionStatus.Connected )
					// Connection not ready. Leave in queue & send later
					return;
				if( PendingMessages.Count == 0 )
					// Nothing to send
					return;

				messages = PendingMessages.ToArray();
				PendingMessages.Clear();

				Status = ConnectionStatus.Sending;
			}
			TriggerStatusChanged();

			try
			{
				var rootMessage = RootMessage.CreateServer_MessagesList( messages );
				await SendRootMessage( rootMessage );
			}
			catch( System.Exception ex )
			{
				TriggerInternalError( "Could not send messages", ex );
			}

			bool triggerStatusChanged;
			bool launchCloseConnection;
			string connectionID = null;
			lock( LockObject )
			{
				switch( Status )
				{
					case ConnectionStatus.Sending:
						// Switch back to 'Connected'
						Status = ConnectionStatus.Connected;
						triggerStatusChanged = true;
						launchCloseConnection = false;
						break;
					case ConnectionStatus.Closing:
						// Stop has been requested while sending
						triggerStatusChanged = false;
						launchCloseConnection = true;
						connectionID = ConnectionID;
						ConnectionID = null;
						break;
					default:
						// Should not happen
						FAIL( "The property 'Status' is not supposed to have value '"+Status+"' here" );
						Status = ConnectionStatus.Closing;
						triggerStatusChanged = true;
						launchCloseConnection = true;
						connectionID = ConnectionID;
						ConnectionID = null;
						break;
				}
			}

			if( triggerStatusChanged )
				TriggerStatusChanged();

			if( launchCloseConnection )
			{
				CloseConnection( connectionID ).FireAndForget();  // NB: No await so can be performed in background
			}
		}

		internal protected void ReceiveMessages(RootMessage rootMessage)
		{
			ASSERT( (rootMessage != null) && (rootMessage.Count > 0), "Missing parameter 'rootMessage'" );
			ASSERT( (rootMessage[RootMessage.TypeKey] as string) == RootMessage.TypeMessages, "Invalid root message type '"+rootMessage[RootMessage.TypeKey]+"' ; expected '"+RootMessage.TypeMessages+"'" );
			ASSERT( rootMessage[RootMessage.KeyMessageMessagesList] is IEnumerable, "Invalid content of root message" );

			foreach( IDictionary<string,object> messageItem in (IEnumerable)rootMessage[RootMessage.KeyMessageMessagesList] )
			{
				var message = new Message( messageItem );
				try
				{
					MessageHandler.ReceiveMessage( message );
				}
				catch( System.Exception ex )
				{
					// NB: Exceptions thrown by the message handlers themselves should be intercepted by the 'MessageHandler' itself
					// => Exceptions arriving here are internal errors of the 'MessageHandler'
					TriggerInternalError( "Error while giving a message to the 'MessageHandler'", ex );
				}
			}
		}

		internal protected void TriggerStatusChanged()
		{
			try
			{
				LOG( "TriggerStatusChanged()" );

				if( OnStatusChanged != null )
					OnStatusChanged( Status );
			}
			catch( System.Exception ex )
			{
				FAIL( "Event 'OnStatusChanged' failed ("+ex.GetType().FullName+"): "+ex.Message );
				TriggerInternalError( "Event 'OnStatusChanged' failed", ex );
			}

			if( Status == ConnectionStatus.Connected )
				CheckPendingMessages().FireAndForget();
		}

		internal protected void TriggerInternalError(string message, Exception exception=null)
		{
			try
			{
				LOG( "TriggerInternalError()" );
				ASSERT( !string.IsNullOrWhiteSpace(message), "Missing parameter 'message'" );

				if( OnInternalError == null )
					return;
				OnInternalError( message, exception );
			}
			catch( System.Exception ex )
			{
				FAIL( "Event 'OnInternalError' failed ("+ex.GetType().FullName+"): "+ex.Message );
			}
		}

		internal protected void TriggerConnectionIdReceived()
		{
			try
			{
				LOG( "TriggerConnectionIdReceived()" );
				ASSERT( !string.IsNullOrWhiteSpace(ConnectionID), "Property 'ConnectionID' is supposed to be set here" );

				if( OnConnectionIdReceived == null )
					return;
				OnConnectionIdReceived( ConnectionID );
			}
			catch( System.Exception ex )
			{
				FAIL( "Event 'OnConnectionIdReceived' failed ("+ex.GetType().FullName+"): "+ex.Message );
				TriggerInternalError( "Event 'OnConnectionIdReceived' failed", ex );
			}
		}
	}
}
