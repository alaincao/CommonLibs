//
// CommonLibs/Web/LongPolling/JSClient/HttpClient.ts
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2019 Alain CAO
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

import * as client from "./Client";
import BaseClient from "./BaseClient";

export interface RootMessage extends client.Message
{
	messages? : client.Message[];
}

export class HttpClient extends BaseClient
{
	private	readonly handlerUrl		: string;
	private readonly errorRetryMax	: number;

	private pollingRequest	: XMLHttpRequest	= null;
	private messageRequest	: XMLHttpRequest	= null;

	constructor(p:{	debug?				: boolean,
					handlerUrl			: string,	// Required: The URL of the server's WebSocket listener
					syncedHandlerUrl?	: string,	// Optional: The URL to the Synced HTTP handler if used (e.g. file uploads)
					logoutUrl?			: string,	// Optional: The URL to redirect to when the server asks to logout
					errorRetryMax?		: number,	// Optional: The number of connection try before stopping the pollings
				})
	{
		super({ debug:p.debug, syncedHandlerUrl:p.syncedHandlerUrl, logoutUrl:p.logoutUrl });

		this.handlerUrl		= (p.handlerUrl == null) ? 'HANDLER_URL_UNDEFINED' : p.handlerUrl;
		this.errorRetryMax	= (p.errorRetryMax == null) ? 10 : p.errorRetryMax;
	}

	public /*override*/ async start() : Promise<void>
	{
		const self = this;

		if( self.getStatus() != client.ClientStatus.DISCONNECTED )
		{
			self.logWarning( `'start()' invoked, but status is not 'DISCONNECTED' ; Ignoring` );
			return;
		}

		// Send the init request
		let connectionID : string;
		try
		{
			connectionID = await self.sendInitRequest();
		}
		catch( err )
		{
			self.triggerInternalError( `Error while sending the 'init' request: ${err}` );
			self.triggerStatusChanged( client.ClientStatus.DISCONNECTED );
			throw err;
		}
		self.triggerConnectionIdReceived( connectionID );
		self.triggerStatusChanged( client.ClientStatus.CONNECTED );

		// Initial messages check
		/*await*/ self.checkPendingMessages();

		// Launch poll requests
		/*await*/ self.runPollLoop();
	}

	public /*override*/ stop() : void
	{
		const self = this;

		if( self.getStatus() == client.ClientStatus.DISCONNECTED )
		{
			// Already disconnected => NOOP
			self.logWarning( `'stop()' invoked, but state is already 'DISCONNECTED'` );
			return;
		}

		// Change status
		// nb: do that before aborting reuqests so that they are not retried
		self.triggerStatusChanged( client.ClientStatus.DISCONNECTED );

		// Abort requests if any
		if( self.pollingRequest != null )
			try { self.pollingRequest.abort(); }
			catch( err ) { self.logWarning( `'pollingRequest.abort()' threw an exception: ${err}` ); }
		if( self.messageRequest != null )
			try { self.messageRequest.abort(); }
			catch( err ) { self.logWarning( `'messageRequest.abort()' threw an exception: ${err}` ); }
	}

	/** Returns: the received ConnectionID */
	private async sendInitRequest() : Promise<string>
	{
		const self = this;

		const message : RootMessage = { type: 'init' };
		const response = await self.sendHttpRequest( ()=>{}, message );
		if( response == null )
			throw 'Init request failed';
		switch( response.type )
		{
			case 'init':
				break;
			case 'logout':
				self.triggerLogoutReceived();
				throw 'Init request rejected by server';
			default:
				throw `Unknown response type '${response.type}'`;
		}

		const connectionId = response.sender;
		if( connectionId == null )
			throw 'Server did not responded with ConnectionID';
		return connectionId;
	}

	private async runPollLoop() : Promise<void>
	{
		const self = this;
		self.assert( self.pollingRequest == null, `'runPollLoop()' invoked, but 'pollingRequest' is already set` );
		try
		{
			const requestMessage : client.Message = {	type	: 'poll',
														sender	: self.getConnectionId() };
			let retryCount = 0;
			while( true )
			{
				let responseMessage : client.Message;
				let triggerError = true;
				try
				{
					// Send request
					self.log( 'message poll' );
					responseMessage = await self.sendHttpRequest( req=>self.pollingRequest = req, requestMessage );
					if( responseMessage == null )
					{
						// Request aborted probably because we are being redirected to another page
						triggerError = false;  // => Don't show the error to the user
						throw 'Request aborted';
					}

					// Process response
					self.processResponseMessage( responseMessage );

					// Polling request went Ok => Reset retry count if any
					retryCount = 0;
				}
				catch( err )
				{
					if( self.getStatus() == client.ClientStatus.DISCONNECTED )
					{
						// Stopped
						self.logWarning( 'Polling stopped' );
						return;
					}

					if( (++retryCount) < self.errorRetryMax )
					{
						// Failed, but need to retry
						if( triggerError )
							self.triggerInternalError( `Polling request failed: ${err}` );
					}
					else
					{
						// Too many fails
						self.triggerInternalError( `Polling request failed: ${err}` );  // nb: Show the error to the user even if (triggerError == false)
						return;
					}
				}
			}
		}
		catch( err )
		{
			self.triggerInternalError( `Poll loop threw an exception: ${err}` );
			throw err;
		}
		finally
		{
			// Stopped
			self.triggerStatusChanged( client.ClientStatus.DISCONNECTED );
		}
	}

	protected /*override*/ async send(messages:client.Message[]) : Promise<void>
	{
		const self = this;
		self.assert( self.messageRequest == null, `'send()' invoked, but messageRequest is already set` );

		// Send request
		const request : RootMessage = {	type		: 'messages',
										sender		: self.getConnectionId(),
										messages	: messages };
		const response = await self.sendHttpRequest( req=>self.messageRequest=req, request );
		if( response == null )
			throw 'Message request failed';

		// Process response
		self.processResponseMessage( response );
	}

	private processResponseMessage(rootMessage:client.Message) : void
	{
		const self = this;

		switch( rootMessage.type )
		{
			case 'reset':
				// Comming from the polling request => Just ignore to restart another request
				break;
			case 'logout':
				// Leave the handling of this to BaseClient
				self.triggerLogoutReceived();
				break;
			case 'messages':
				self.receiveMessages( (<client.MessageDict>rootMessage).messages );
				break;
			default:
				throw `Unknown response type ${rootMessage.type}' for root message`;
		}
	}

	/** NB: can return 'null' for aborted requests (e.g. redirecting to another page) */
	private sendHttpRequest(assignRequest:(req:XMLHttpRequest)=>void, rootMessage:client.Message) : Promise<RootMessage>
	{
		const self = this;

		const strMessageObject = JSON.stringify( rootMessage );
		const request = new XMLHttpRequest();
		request.open( "POST", self.handlerUrl, true );
		request.setRequestHeader( "Content-Type", "application/x-www-form-urlencoded" );
		if( self.authorizationHeader != null )
			request.setRequestHeader( 'Authorization', self.authorizationHeader );
		assignRequest( request );
		const rv = new Promise<client.Message>( (resolve,reject)=>
			{
				request.onreadystatechange = (evt)=>
					{
						if( request.readyState != 4 )
							// "readyState != 4" == still running => NOOP
							return;
						// here: request terminated => Must invoke either 'resolve()' or 'reject()'
						assignRequest( null );

						if( (request.status == 0/*All browsers*/) || (request.status == 12031/*Only IE*/) )
						{
							// Request aborted (e.g. redirecting to another page)
							self.logWarning( 'Request aborted' );
							resolve( null );
							return;
						}

						if( request.status == 12002/*Only IE*/ )
						{
							// Request timeout
							reject( 'Request timeout' );
							return;
						}

						if( request.status != 200 )  // HTTP 200 OK
						{
							reject( 'Server error (status="' + request.status + '")' );
							return;
						}
						// here: HTTP 200 Ok

						let response : RootMessage;
						try
						{
							response = JSON.parse( request.responseText );
						}
						catch( err )
						{
							reject( `JSON parse error: ${err}` );
							return;
						}

						// All OK !
						resolve( response );
					};
			} );
		request.send( strMessageObject );

		return rv;
	}
}

export default HttpClient;
