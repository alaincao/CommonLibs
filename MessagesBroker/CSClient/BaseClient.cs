using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CommonLibs.Utils;
using CommonLibs.Utils.Event;

namespace CommonLibs.MessagesBroker.CSClient
{
	using TMessage = IDictionary<string,object>;
	using TRootMessage = IDictionary<string,object>;

	public abstract class BaseClient
	{
		[System.Diagnostics.Conditional("DEBUG")] protected void LOG(string message)				{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected void FAIL(string message)				{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		public enum ConnectionStatus
		{
			Disconnected	= 0,  // Default value
			Connecting,
			Connected,
			Sending,
			Closing
		}

		private readonly object					LockObject			= new object();
		public ConnectionStatus					Status				{ get; private set; }
		public string							ConnectionID		{ get; private set; }
		private List<TMessage>					PendingMessages		= new List<TMessage>();

		public readonly IMessageReceiver		MessageReceiver;

		/// <summary>p1: The new status</summary>
		public readonly CallbackListAsync<ConnectionStatus>		OnStatusChanged				= new CallbackListAsync<ConnectionStatus>();
		/// <summary>p1: The ConnectionID received by the server</summary>
		public readonly CallbackListAsync<string>				OnConnectionIdReceived		= new CallbackListAsync<string>();
		/// <summary>
		/// p1: The error message (not null)<br/>
		/// p2: The exception that triggered the error if any (ie. can be null)
		/// </summary>
		public readonly CallbackListAsync<string,Exception>		OnInternalError				= new CallbackListAsync<string,Exception>();
		/// <summary>
		/// p1: The received message that caused the exception<br/>
		/// p2: The exception thrown
		/// </summary>
		public readonly CallbackListAsync<TMessage,Exception>	OnHandlerException			= new CallbackListAsync<TMessage,Exception>();

		/// <returns>The 'ConnectionID' given by the server</returns>
		internal protected abstract Task<string> SendInitMessage();
		internal protected abstract Task MainLoop();
		internal protected abstract Task CloseConnection(string connectionID);
		internal protected abstract Task SendRootMessage(TRootMessage rootMessage);

		internal protected BaseClient(IMessageReceiver messageReceiver=null)
		{
			MessageReceiver = messageReceiver ?? new SimpleMessageReceiver();

			OnStatusChanged.Add( (status)=>
				{
					if( status == ConnectionStatus.Connected )
						CheckPendingMessages().FireAndForget();
					return Task.CompletedTask;
				} );
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
			await OnStatusChanged.Invoke( ConnectionStatus.Connecting );

			ASSERT( Status == ConnectionStatus.Connecting, "The 'Status' is supposed to be 'Connecting' here" );

			bool initSent = false;
			try
			{
				ConnectionID = await SendInitMessage();
				await OnConnectionIdReceived.Invoke( ConnectionID );  // NB: Is NOT supposed to send an exception (try{}catch{} enclosed)
				initSent = true;
			}
			finally
			{
				// Update from 'Connecting' status
				if(! initSent )
				{
					// An exception has occured
					Status = ConnectionStatus.Disconnected;
					await OnStatusChanged.Invoke( ConnectionStatus.Disconnected );
				}
				else  // The last lines of the try/catch block has been reached (no error occured)
				{
					// Continue with execution & invoke MainLoop()
				}
			}
			ASSERT( Status == ConnectionStatus.Connecting, "Property 'Status' is STILL supposed to be 'Connecting' here" );

			Status = ConnectionStatus.Connected;
			await OnStatusChanged.Invoke( ConnectionStatus.Connected );

			CheckPendingMessages().FireAndForget();

			// Send/receive messages loop
			var dummy = MainLoop();  // NB: No await => Run on background tasks threads starting from here
			return true;
		}

		public async Task Stop()
		{
			LOG( "Stop()" );
			bool launchCloseConnection;
			string connectionID;
			string internalError;
			{
				ConnectionStatus newStatus;
				lock( LockObject )
				{
					switch( Status )
					{
						case ConnectionStatus.Disconnected:
							// Already stopped
							return;
						case ConnectionStatus.Connecting:
							internalError = "Cannot stop the messaging client while in state 'Connecting'";
							goto ERROR;  // nb: Cannot "await" inside lock{} => using "goto"
						case ConnectionStatus.Connected:
							// OK
							newStatus = Status = ConnectionStatus.Closing;
							launchCloseConnection = true;
							break;
						case ConnectionStatus.Sending:
							// Output socket in use => 'StopAsync()' will be invoked later by 'SendPendingMessages()'
							newStatus = Status = ConnectionStatus.Closing;
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
				await OnStatusChanged.Invoke( newStatus );
			}

			if( launchCloseConnection )
			{
				try
				{
					await CloseConnection( connectionID );
				}
				catch( System.Exception ex )
				{
					FAIL( $"'{this.GetType().Name}.CloseConnection()' threw an exception ({ex.GetType().FullName}): {ex.Message}" );
				}

				lock( LockObject )
				{
					ASSERT( Status == ConnectionStatus.Closing, "Property 'Status' has changed and should not have" );
					Status = ConnectionStatus.Disconnected;
				}
				await OnStatusChanged.Invoke( ConnectionStatus.Disconnected );
			}

			return;
		ERROR:
			await OnInternalError.Invoke( internalError, null );
			return;
		}

		public void SendMessage(TMessage message)
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
						throw new ApplicationException( "Cannot send message: Connection status is '"+Status+"'" );
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

			TMessage[] messages;
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
			await OnStatusChanged.Invoke( ConnectionStatus.Sending );

			try
			{
				var rootMessage = CreateRootMessage_MessagesList( messages );
				await SendRootMessage( rootMessage );
			}
			catch( System.Exception ex )
			{
				await OnInternalError.Invoke( "Could not send messages", ex );
			}

			bool launchCloseConnection;
			string connectionID = null;
			{
				ConnectionStatus? newStatus = null;
				lock( LockObject )
				{
					switch( Status )
					{
						case ConnectionStatus.Sending:
							// Switch back to 'Connected'
							newStatus = Status = ConnectionStatus.Connected;
							launchCloseConnection = false;
							break;
						case ConnectionStatus.Closing:
							// Stop has been requested while sending
							launchCloseConnection = true;
							connectionID = ConnectionID;
							ConnectionID = null;
							break;
						default:
							// Should not happen
							FAIL( "The property 'Status' is not supposed to have value '"+Status+"' here" );
							newStatus = Status = ConnectionStatus.Closing;
							launchCloseConnection = true;
							connectionID = ConnectionID;
							ConnectionID = null;
							break;
					}
				}
				if( newStatus != null )
					await OnStatusChanged.Invoke( newStatus.Value );
			}

			if( launchCloseConnection )
			{
				var dummy = CloseConnection( connectionID );  // NB: No await so can be performed in background
			}
		}

		protected async Task ReceiveMessages(TRootMessage rootMessage)
		{
			ASSERT( (rootMessage != null) && (rootMessage.Count > 0), "Missing parameter 'rootMessage'" );
			ASSERT( (rootMessage[RootMessageKeys.KeyType] as string) == RootMessageKeys.TypeMessages, $"Invalid root message type '{rootMessage[RootMessageKeys.KeyType]}' ; expected '{RootMessageKeys.TypeMessages}'" );
			ASSERT( (rootMessage[RootMessageKeys.TypeMessages] as IEnumerable) != null, "Invalid content of root message" );

			TMessage[] messages;
			try
			{
				var enumerable = (IEnumerable)rootMessage[ RootMessageKeys.TypeMessages ];
				messages = enumerable.Cast<TMessage>().ToArray();
			}
			catch( System.Exception ex )
			{
				await OnInternalError.Invoke( "Could not parse messages list received from server", ex );
				return;
			}

			foreach( var message in messages )
			{
				try
				{
					await MessageReceiver.ReceiveMessage( message );
				}
				catch( System.Exception ex )
				{
					await OnHandlerException.Invoke( message, ex );
				}
			}
		}

		protected virtual TRootMessage CreateRootMessage_Init()
		{
			return new Dictionary<string,object> {
								{ RootMessageKeys.KeyType, RootMessageKeys.TypeInit } };
		}
		protected virtual TRootMessage CreateRootMessage_Poll(string connectionID)
		{
			return new Dictionary<string,object> {
								{ RootMessageKeys.KeyType, RootMessageKeys.TypePoll },
								{ RootMessageKeys.KeySenderID, connectionID } };
		}
		protected virtual TRootMessage CreateRootMessage_MessagesList(IEnumerable<TMessage> messages)
		{
			return new Dictionary<string,object> {
								{ RootMessageKeys.KeyType, RootMessageKeys.TypeMessages },
								{ RootMessageKeys.TypeMessages, messages } };
		}
	}
}
