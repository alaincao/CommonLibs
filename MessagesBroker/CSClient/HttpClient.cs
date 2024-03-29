﻿//
// CommonLibs/MessagesBroker/CSClient/HttpClient.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2010 - 2022 Alain CAO
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
using System.Text;
using System.Threading.Tasks;

using CommonLibs.Utils;
using CommonLibs.MessagesBroker.Utils;

namespace CommonLibs.MessagesBroker.CSClient
{
	using TMessage = IDictionary<string,object>;
	using TRootMessage = IDictionary<string,object>;

	public class HttpClient : BaseClient
	{
		private const string	RootMessageType_Logout		= "logout";  // Was used by legacy versions of the server (ie. the LongPolling one) ; Keep it here for compatibility

		public readonly string						HandlerUrl;
		public TMessage								InitMessage			{ get; }
		public readonly System.Net.CookieContainer	Cookies;
		private System.Net.Http.HttpClient			PollClient			= null;

		/// <param name="cookies">Contains the ASP.NET session cookie</param>
		private HttpClient(string handlerUrl, System.Net.CookieContainer cookies, IMessageReceiver messageReceiver=null, TMessage initMessageTemplate=null) : base(messageReceiver)
		{
			ASSERT( !string.IsNullOrWhiteSpace(handlerUrl), $"Missing parameter '{nameof(handlerUrl)}'" );
			ASSERT( cookies != null, $"Missing parameter '{nameof(cookies)}'" );

			HandlerUrl = handlerUrl;
			Cookies = cookies;

			InitMessage = CreateRootMessage_Init();
			if( initMessageTemplate != null )
				foreach( var pair in initMessageTemplate )
					InitMessage[ pair.Key ] = pair.Value;
		}

		public static Task<HttpClient> New(string handlerUrl, System.Net.CookieContainer cookies, IMessageReceiver messageReceiver=null, TMessage initMessageTemplate=null)
		{
			return Task.FromResult( new HttpClient(handlerUrl, cookies, messageReceiver, initMessageTemplate) );
		}

		protected async internal override Task<string> SendInitMessage()
		{
			var response = await SendHttpRequest( InitMessage, isPollConnection:false );

			var connectionID = response.TryGetString( RootMessageKeys.KeySenderID );
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
					var request = CreateRootMessage_Poll( ConnectionID );
					var response = await SendHttpRequest( request, isPollConnection:true );

					var responseType = response[RootMessageKeys.KeyType] as string;
					LOG( "Root message received: "+responseType );
					switch( responseType )
					{
						case RootMessageKeys.TypeReset:
							// TCP connection refresh asked (just send another request)
							continue;

						case RootMessageType_Logout:
							// Terminate MainLoop
							goto EXIT_LOOP;

						case RootMessageKeys.TypeMessages:
							ReceiveMessages( response ).FireAndForget();
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
						await OnInternalError.Invoke( "Error while reading message from the HTTP request", ex );
						break;
				}
			}

			await Stop();
		}

		private async Task<IDictionary<string,object>> SendHttpRequest(TRootMessage rootMessage, bool isPollConnection)
		{
			ASSERT( (rootMessage != null) && (rootMessage.Count > 0), "Missing parameter 'rootMessage'" );
			ASSERT( ! string.IsNullOrWhiteSpace(HandlerUrl), "Property 'HandlerUrl' is supposed to be set here" );

			string strResponse;
			try
			{
				using( var handler = new System.Net.Http.HttpClientHandler(){ CookieContainer = Cookies } )
				using( var client = new System.Net.Http.HttpClient(handler){ Timeout = System.Threading.Timeout.InfiniteTimeSpan } )
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
					var content = new System.Net.Http.StringContent( strMessage, Encoding.UTF8, "application/json" );
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
			var responseMessage = strResponse.FromJSONDictionary();
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

		protected internal override async Task SendRootMessage(IDictionary<string, object> rootMessage)
		{
			ASSERT( (rootMessage != null) && (rootMessage.Count > 0), $"Missing parameter '{nameof(rootMessage)}'" );

			rootMessage[RootMessageKeys.KeySenderID] = ConnectionID;
			var response = await SendHttpRequest( rootMessage, isPollConnection:false );
			var responseType = response[RootMessageKeys.KeyType] as string;
			LOG( "Root message received: "+responseType );
			switch( responseType )
			{
				case RootMessageType_Logout:
					throw new CommonException( "Logged out" );

				case RootMessageKeys.TypeMessages:
					// NB: Should be empty
					ReceiveMessages( response ).FireAndForget();
					break;

				default:
					throw new NotImplementedException( "Unsupported response message type '" + responseType + "'" );
			}
		}
	}
}
