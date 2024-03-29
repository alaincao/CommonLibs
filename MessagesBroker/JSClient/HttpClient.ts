﻿//
// CommonLibs/MessagesBroker/JSClient/HttpClient.ts
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2019 Alain CAO
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

export interface RootMessage extends client.Message
{
	messages? : client.Message[];
}

export class HttpClient extends BaseClient
{
	private	readonly handlerUrl		: string;
	private readonly errorRetryMax	: number;
	public readonly  initMessage	: {[key:string]:any};

	private pollingRequest	: XMLHttpRequest	= null;
	private messageRequest	: XMLHttpRequest	= null;

	constructor(p:{	debug?				: boolean,
					handlerUrl			: string,	// Required: The URL of the server's WebSocket listener
					errorRetryMax?		: number,	// Optional: The number of connection try before stopping the pollings
					initMessageTemplate?: {[key:string]:any},
				})
	{
		super({ debug:p.debug });
		const self = this;

		this.handlerUrl		= (p.handlerUrl == null) ? 'HANDLER_URL_UNDEFINED' : p.handlerUrl;
		this.errorRetryMax	= (p.errorRetryMax == null) ? 5 : p.errorRetryMax;

		this.initMessage = { type: 'init' };
		if( p.initMessageTemplate != null )
			Object.keys( p.initMessageTemplate ).forEach( (key)=>self.initMessage[key] = p.initMessageTemplate[key] );
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

		const response = await self.sendHttpRequest( ()=>{}, self.initMessage );
		if( response == null )
			throw 'Init request failed';
		switch( response.type )
		{
			case 'init':
				break;
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
					var cont = self.processResponseMessage( responseMessage );
					if(! cont )
						// Stop polling
						break;

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

	private processResponseMessage(rootMessage:client.Message) : boolean
	{
		const self = this;

		switch( rootMessage.type )
		{
			case 'reset':
				// Comming from the polling request => Just ignore to restart another request
				return true;
			case 'messages':
				self.receiveMessages( (<client.MessageDict>rootMessage).messages );
				return true;
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
