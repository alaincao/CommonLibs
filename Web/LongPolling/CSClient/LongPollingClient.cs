using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

using CommonLibs.Utils;
using CommonLibs.MessagesBroker.Utils;

namespace CommonLibs.Web.LongPolling.CSClient
{
	internal class LongPollingClient : BaseClient
	{
		public string			HandlerUrl			{ get; private set; }
		private HttpClient		PollClient			= null;

		internal LongPollingClient(MessageHandler messageHandler, System.Net.CookieContainer cookies, string handlerUrl) : base(messageHandler, cookies)
		{
			ASSERT( !string.IsNullOrWhiteSpace(handlerUrl), "Missing parameter 'handlerUrl'" );

			HandlerUrl = handlerUrl;
		}

		protected async internal override Task<string> SendInitMessage()
		{
			var response = await SendHttpRequest( InitMessage, isPollConnection:false );

			var connectionID = response.TryGetString( RootMessage.KeySenderID );
			LOG( "ConnectionID: "+connectionID );
			if( string.IsNullOrWhiteSpace(connectionID) )
				throw new ArgumentException( "The server did not return the ConnectionID" );

			return connectionID;
		}

		protected internal async override Task MainLoop()
		{
			LOG( "MainLoop()" );

			try
			{
				while( true )
				{
					ASSERT( ! string.IsNullOrWhiteSpace(HandlerUrl), "Property 'HandlerUrl' is supposed to be set here" );
					ASSERT( ! string.IsNullOrWhiteSpace(ConnectionID), "Property 'ConnectionID' is supposed to be set here" );

					LOG( "Send POLL request" );
					var request = CommonLibs.Web.LongPolling.RootMessage.CreateClient_Poll( ConnectionID );
					var response = await SendHttpRequest( request, isPollConnection:true );

					var responseType = response[RootMessage.TypeKey] as string;
					LOG( "Root message received: "+responseType );
					switch( responseType )
					{
						case RootMessage.TypeReset:
							// TCP connection refresh asked (just send another request)
							continue;

						case RootMessage.TypeLogout:
							// Terminate MainLoop
							goto EXIT_LOOP;

						case RootMessage.TypeMessages:
							ReceiveMessages( response );
							break;

						default:
							throw new NotImplementedException( "Unsupported response message type '" + responseType + "'" );
					}
				}
			EXIT_LOOP:;
			}
			catch( System.Exception ex )
			{
				switch( Status )
				{
					case ConnectionStatus.Closing:
					case ConnectionStatus.Disconnected:
						// Error received while closing the connections => No need to report
						break;
					default:
						TriggerInternalError( "Error while reading message from the HTTP request", ex );
						break;
				}
			}

			Stop();
		}

		private async Task<RootMessage> SendHttpRequest(RootMessage rootMessage, bool isPollConnection)
		{
			ASSERT( (rootMessage != null) && (rootMessage.Count > 0), "Missing parameter 'rootMessage'" );
			ASSERT( ! string.IsNullOrWhiteSpace(HandlerUrl), "Property 'HandlerUrl' is supposed to be set here" );

			string strResponse;
			try
			{
				using( var handler = new HttpClientHandler(){ CookieContainer = Cookies } )
				using( var client = new HttpClient(handler){ Timeout = System.Threading.Timeout.InfiniteTimeSpan } )
				{
					if( isPollConnection )
					{
						ASSERT( PollClient == null, "Property 'PollClient' is not supposed to be set here" );
						PollClient = client;

						var status = Status;
						switch( status )
						{
							case ConnectionStatus.Closing:
							case ConnectionStatus.Disconnected:
								throw new ArgumentException( "Cannot create new connection while status is '"+status+"'" );
						}
					}

					var strMessage = rootMessage.ToJSON();
					var content = new StringContent( strMessage, Encoding.UTF8, "application/json" );
					var response = await client.PostAsync( HandlerUrl, content );

					LOG( "Receive response content" );
					strResponse = await response.Content.ReadAsStringAsync();
				}
			}
			finally
			{
				if( isPollConnection )
				{
					ASSERT( PollClient != null, "Property 'PollClient' is supposed to be set here" );
					PollClient = null;
				}

			}
			var responseMessage = CommonLibs.Web.LongPolling.RootMessage.CreateClient_ServerResponse( strResponse.FromJSONDictionary() );
			return responseMessage;
		}

		protected internal async override Task CloseConnection(string connectionID)
		{
			LOG( "CloseConnection()" );
			ASSERT( Status == ConnectionStatus.Closing, "Property 'Status' is supposed to be 'Closing' here" );

			var client = PollClient;
			if( client != null )
				client.CancelPendingRequests();

			await Task.FromResult(0);  // NB: Nothing to 'await' here
		}

		protected internal async override Task SendRootMessage(RootMessage rootMessage)
		{
			ASSERT( (rootMessage != null) && (rootMessage.Count > 0), $"Missing parameter '{nameof(rootMessage)}'" );

			rootMessage[RootMessage.KeySenderID] = ConnectionID;
			var response = await SendHttpRequest( rootMessage, isPollConnection:false );
			var responseType = response[RootMessage.TypeKey] as string;
			LOG( "Root message received: "+responseType );
			switch( responseType )
			{
				case RootMessage.TypeLogout:
					throw new CommonException( "Logged out" );

				case RootMessage.TypeMessages:
					// NB: Should be empty
					ReceiveMessages( response );
					break;

				default:
					throw new NotImplementedException( "Unsupported response message type '" + responseType + "'" );
			}
		}
	}
}
