//
// CommonLibs/Web/LongPolling/JSClient/WebSocketClient.ts
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2019 SigmaConso
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

import * as client from "./Client";
import BaseClient from "./BaseClient";

export class WebSocketClient extends BaseClient
{
	private	readonly	handlerUrl			: string;
	private	readonly	keepAliveUrl		: string;
	private	readonly	keepAliveTimeout	: number;
	private				keepAliveTimer		: any;
	private				webSocket			: WebSocket;

	constructor(p:{	debug?				: boolean,
					handlerUrl			: string,	// Required: The URL of the server's WebSocket listener
					keepAliveUrl?		: string,	// Optional: Set to create a dummy HTTP request to keep the ASP.NET session alive
					keepAliveTimeout?	: number,	// Optional: If 'keepAliveUrl' is set, this will be the interval between the dummy HTTP requests (default: 10*60*1000 miliseconds = 10 minutes)
					syncedHandlerUrl?	: string,	// Optional: The URL to the Synced HTTP handler if used (e.g. file uploads)
					logoutUrl?			: string,	// Optional: The URL to redirect to when the server asks to logout
				})
	{
		super({ debug:p.debug, syncedHandlerUrl:p.syncedHandlerUrl, logoutUrl:p.logoutUrl });
		const self = this;

		const handlerUrl		= (p.handlerUrl == null) ? 'HANDLER_URL_UNDEFINED' : p.handlerUrl;
		this.keepAliveUrl		= (p.keepAliveUrl == null) ? null : p.keepAliveUrl;
		this.keepAliveTimeout	= (p.keepAliveTimeout == null) ? 600000 : p.keepAliveTimeout;
		self.keepAliveTimer		= null;
		self.webSocket			= null;

		// Check URL's protocol
		const url = document.createElement( 'a' );  // nb: 'would have used 'new URL()' to be "DOM-independent", but it is not supported by IE !!! >:-(
		url.href = handlerUrl;
		if( (url.protocol != 'wss:') && (url.protocol != 'ws:') )
		{
			if( url.protocol == 'https:' )
				url.protocol = 'wss:';
			else
				url.protocol = 'ws:';
		}
		this.handlerUrl = url.href;
	}

	public /*override*/ start() : Promise<void>
	{
		const self = this;

		return new Promise<void>( (resolve,reject)=>
			{
				try
				{
					const initMessage = { 'type' : 'init' };
					const strInitMessage = JSON.stringify( initMessage );

					self.log( 'WebSocket connecting "'+self.handlerUrl+'"' );
					self.webSocket = new WebSocket( self.handlerUrl );
					if( self.authorizationHeader != null )
						throw `'authorizationHeader' has been specified, but is not (yet?) supported using WebSockets`;
					self.webSocket.onerror = (e)=>self.webSocket_onError( e );
					self.webSocket.onclose = (e)=>self.webSocket_onClose( e );
					self.webSocket.onmessage = (e)=>self.webSocket_onMessage_init( e, resolve, reject );  // nb: one-shot message handler to receive the initial message
					self.webSocket.onopen = function()
						{
							try
							{
								// Send init message
								self.log( 'message send', initMessage );
								self.webSocket.send( strInitMessage );
							}
							catch( err )
							{
								reject( err );
							}
						};
				}
				catch( err )
				{
					reject( err );
				}
			} );
	}

	public /*override*/ stop() : void
	{
		const self = this;

		if( self.webSocket != null )
		{
			self.webSocket.close();
			self.webSocket = null;
		}
		self.triggerStatusChanged( client.ClientStatus.DISCONNECTED );
	}

	protected /*override*/ async send(messages:client.Message[]) : Promise<void>
	{
		const self = this;
		const rootMessage = {	'type'		: 'messages',
								'messages'	: messages };
		const strMessage = JSON.stringify( rootMessage );
		self.webSocket.send( strMessage );  // nb: not async but non-blocking (uses a buffer) ; cf. https://stackoverflow.com/questions/18246708/does-javascript-websocket-send-method-block
	}

	private webSocket_onError(e:Event) : void
	{
		const self = this;
		self.log( 'WebSocket error', e );
		self.triggerInternalError( 'WebSocket error: '+JSON.stringify(e) );
	}

	private webSocket_onClose(e:CloseEvent) : void
	{
		const self = this;

		self.log( 'WebSocket closed', e );

		self.resetConnectionId();
		self.webSocket = null;
	}

	private webSocket_onMessage_init(e:MessageEvent, resolve:()=>void, reject:(reason:any)=>void) : void
	{
		const self = this;
		let errorPrefix : string;
		try
		{
			errorPrefix = 'JSON Parse Error: ';
			const message = JSON.parse( e.data );
			errorPrefix = '';

			const type = message['type'];
			if( type != 'init' )
				throw 'Unexpected message type "'+type+'" ; Expected "init"';

			const connectionId = message['sender'];
			if( connectionId == null )
				throw 'Server did not responded with ConnectionID';

			self.log( 'message recv', message );

			// Ready to receive & subsequent messages must go to the 'onMessage' handler
			self.webSocket.onmessage = (e)=>self.webSocket_onMessage( e );

			// Invoke events
			self.triggerConnectionIdReceived( connectionId );
			self.triggerStatusChanged( client.ClientStatus.CONNECTED );

			// Start keepalive timer
			errorPrefix = 'Could not start keepalive timer: ';
			self.checkKeepAliveTimeout();
		}
		catch( err )
		{
			try { self.triggerInternalError( errorPrefix+err ); } catch(err) {}
			reject( err );
			return;  // Fatal ; Stop here ...
		}

		// Startup terminated
		resolve();

		// Check for any pending messages
		/*await*/ self.checkPendingMessages();
	}

	private webSocket_onMessage(e:MessageEvent) : void
	{
		const self = this;
		let errorPrefix : string;
		try
		{
			errorPrefix = 'JSON Parse Error: ';
			const response = JSON.parse( e.data );
			errorPrefix = '';

			const responseType = response[ 'type' ];
			if( responseType == 'reset' )
			{
				// Ignore (should not happen?)
			}
			else if( responseType == 'logout' )
			{
				self.triggerLogoutReceived();
			}
			else if( responseType == 'messages' )
			{
				const messagesList = response[ 'messages' ];
				self.receiveMessages( messagesList );
			}
			else
			{
				throw 'Unknown response type "' + responseType + '"';
			}
		}
		catch( err )
		{
			self.triggerInternalError( errorPrefix+err );
		}
	}

	private checkKeepAliveTimeout() : void
	{
		const self = this;

		if( self.keepAliveUrl == null )
			// Nothing to do
			return;

		if( self.getStatus() != client.ClientStatus.DISCONNECTED )
		{
			if( self.keepAliveTimer != null )
				// A timer is already running => NOOP
				return;

			// Start the timer
			self.keepAliveTimer = setTimeout( function()
				{
					// Deactivate current timer
					self.keepAliveTimer = null;

					// Send keepalive request
					const req = new XMLHttpRequest();
					req.open( 'GET', self.keepAliveUrl );
					req.send();

					// Reactivate timer
					self.checkKeepAliveTimeout();
				}, self.keepAliveTimeout );
		}
		else
		{
			if( self.keepAliveTimer != null )
			{
				// A timer is running => Stop it
				const tm = self.keepAliveTimer;
				self.keepAliveTimer = null;
				clearTimeout( tm );
			}
		}
	}
}  // class WebSocketClient

export default WebSocketClient;
