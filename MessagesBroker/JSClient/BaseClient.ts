//
// CommonLibs/MessagesBroker/JSClient/BaseClient.ts
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

export abstract class BaseClient implements client.MessageHandler
{
	private readonly	events			: client.EventsHandler	= client.createEventHandler();
	public readonly		debug			: boolean;
	private				status			: client.ClientStatus	= client.ClientStatus.DISCONNECTED;
	private				connectionId	: string		= null;
	private				sendMessageUid	: number		= 0;

	public				authorizationHeader	: string;

	private				pendingMessages		: { message:client.Message, callback?:(evt?:any,message?:client.Message)=>void }[]	= [];

	public abstract		start()	: Promise<void>;
	public abstract		stop()	: void;
	protected abstract	send(messages:client.Message[]) : Promise<void>;

	constructor(p:{	debug?:boolean })
	{
		const self = this;
		this.debug					= (p.debug == true);
		self.authorizationHeader	= null;

		if( (typeof(window) != undefined) && (typeof($) != 'undefined') )
		{
			// Hack: When leaving the page, explicitly abort any connections because IE keeps them alive!!!
			$(window).on('unload', function()
				{
					self.stop();
				} );
		}
	}

	// For EventsHandler: Redirect methods to 'self.events'
	public bind(name:string, callback:(evt?:any,message?:client.Message)=>void) : this
	{
		const self = this;
		self.events.bind.apply( self.events, arguments )
		return self;
	}
	public unbind(name:string, callback?:(evt?:any)=>void) : this
	{
		const self = this;
		self.events.unbind.apply( self.events, arguments )
		return self;
	}
	public trigger(name:string, p?:any) : this
	{
		const self = this;
		self.events.trigger.apply( self.events, arguments )
		return self;
	}

	public sendMessage(message:client.Message, callback?:(evt?:any,message?:client.Message)=>void) : this
	{
		const self = this;

		self.pendingMessages.push({ message:message, callback:callback });
		/*await*/ self.checkPendingMessages();
		return self;
	};

	public sendRequest<T extends client.Message>(request:client.Message) : Promise<T>
	{
		const self = this;
		return new Promise<T>( (resolve)=>
			{
				self.sendMessage( request, (e,response)=>
					{
						resolve( <T>response );
					} );
			} );
	}

	protected receiveMessages(messages:client.Message[]) : void
	{
		const self = this;

		if( messages.length == 0 )  // e.g. POLL request returned
			return;

		self.log( 'message recv', messages );
		for( let i=0; i<messages.length; ++i )
		{
			try
			{
				const messageContent = messages[ i ];
				const type = messageContent[ 'type' ];

				self.trigger( type, messageContent );
			}
			catch( err )
			{
				// The assigned message handler threw an exception
				self.triggerMessageHandlerFailed( err );
			}
		}
	}

	protected hasPendingMessages() : boolean
	{
		return ( this.pendingMessages.length > 0 );
	}

	protected async checkPendingMessages() : Promise<void>
	{
		const self = this;
		try
		{
			if(! self.hasPendingMessages() )
				// NOOP
				return;

			switch( self.getStatus() )
			{
				case client.ClientStatus.DISCONNECTED:  // Connection not ready
					return;
				case client.ClientStatus.SENDING:  // Connection busy
					return;
				case client.ClientStatus.CONNECTED:  // Connection ready to send => Continue
					break;
				default:
					self.triggerInternalError( `Unknown status '${self.getStatus()}'` );
					return;
			}
			// here: status == CONNECTED

			// Switch to SENDING state
			self.triggerStatusChanged( client.ClientStatus.SENDING );

			const messages : client.Message[] = [];
			const items = self.pendingMessages;
			self.pendingMessages = [];
			for( let i=0; i<items.length; ++i )
			{
				const message = items[i].message;
				const callback = items[i].callback;

				if( callback != null )
				{
					// Attach the callback to a new one-shot message handler
					const replyMessageHandler = 'message_handler_autoreply_' + (++ self.sendMessageUid);
					message.reply_to_type = replyMessageHandler;

					self.bind( replyMessageHandler, function(evt, message)
						{
							// The message has returned => unbind the one-shot message handler
							self.unbind( replyMessageHandler );

							// Forward the message to the callback
							callback( evt, message );
						} );
				}

				messages.push( message );
			}
			if( messages.length == 0 )
				return;

			try
			{
				self.log( 'message send', messages );
				await self.send( messages );
			}
			finally
			{
				// Revert to CONNECTED state
				self.triggerStatusChanged( client.ClientStatus.CONNECTED );
			}

			/*await*/ self.checkPendingMessages();
		}
		catch( err )
		{
			self.triggerInternalError( `Error while sending messages: ${err}` );
		}
	}

	public clearPendingMessages() : void
	{
		this.pendingMessages = [];
	}

	public log(...optionalParams: any[]) : void
	{
		const self = this;
		if(! self.debug )
			return;

		if( (typeof(console) == 'undefined') || (typeof(console.log) == 'undefined') )
			// Console not available ; i.e. old IE
			return;
		console.log.apply( console, arguments );
	}

	protected assert(test:boolean, message:string) : void
	{
		const self = this;
		if(! self.debug )
			return;

		if(! test )
			self.logWarning( 'Assertion failed', message );
	}

	public logWarning(...optionalParams: any[]) : void
	{
		const self = this;

		if(! self.debug )
			return;
		if( (typeof(console) == 'undefined') || (typeof(console.warn) == 'undefined') )
			// Console not available ; i.e. old IE
			return;
		console.warn.apply( console, arguments );
	}

	public getStatus() : client.ClientStatus
	{
		return this.status;
	}
	protected triggerStatusChanged(status:client.ClientStatus) : this
	{
		const self = this;

		const oldStatus = self.status;
		if( oldStatus == status )
			// NOOP
			return self;
		self.status = status;

		if( status == client.ClientStatus.CONNECTED )
		{
			self.assert( self.connectionId != null, 'Switching to "CONNECTED" status, but "connectionID" is not set' );
		}
		else if( status == client.ClientStatus.DISCONNECTED )
		{
			self.connectionId = null;
		}

		try
		{
			self.events.trigger( 'commonlibs_message_handler_status_changed', status );
		}
		catch( err )
		{
			self.triggerInternalError( `Event handler for statusChanged threw an exception: ${err}` );
		}
		return self;
	}
	public onStatusChanged(callback:(status:client.ClientStatus)=>void) : this
	{
		const self = this;
		self.events.bind( 'commonlibs_message_handler_status_changed', function(evt,status:client.ClientStatus)
			{
				try { callback(status); }
				catch(err) { self.triggerInternalError( 'Error while invoking commonlibs_message_handler_status_changed event: '+err ); }
			} );
		return self;
	}

	public getConnectionId() : string
	{
		return this.connectionId;
	}
	protected resetConnectionId() : void
	{
		this.connectionId = null;
	}
	protected triggerConnectionIdReceived(connectionId:string) : this
	{
		const self = this;
		self.log( 'message init', connectionId );
		self.connectionId = connectionId;
		self.events.trigger( 'commonlibs_message_handler_connection_id_received', connectionId );
		return self;
	}
	public onConnectionIdReceived(callback:(connectionId:string)=>void) : this
	{
		const self = this;

		// Bind to event
		self.events.bind( 'commonlibs_message_handler_connection_id_received', function(evt,connectionId:string)
			{
				try { callback(connectionId); }
				catch(err) { self.triggerInternalError( 'Error while invoking commonlibs_message_handler_connection_id_received event: '+err ); }
			} );

		if( self.connectionId != null )
		{
			// Already received => Also invoke immediately
			callback( self.connectionId );
		}

		return self;
	}

	protected triggerMessageHandlerFailed(error:any) : this
	{
		const self = this;
		self.events.trigger( 'message_handler_failed', error );
		return self;
	}
	public onMessageHandlerFailed(callback:(error:any)=>void) : this
	{
		const self = this;
		self.events.bind( 'message_handler_failed', function(evt,error:any)
			{
				try { callback(error); }
				catch(err) { self.triggerInternalError( 'Error while invoking message_handler_failed event: '+err ); }
			} );
		return self;
	}

	protected triggerInternalError(message:string) : this
	{
		const self = this;
		try
		{
			self.events.trigger( 'commonlibs_message_handler_error', message );
		}
		catch( err )
		{
			client.fatalError( 'Internal error handler threw an error: '+err );
		}
		return self;
	}
	public onInternalError(callback:(message:string)=>void) : this
	{
		const self = this;
		self.events.bind( 'commonlibs_message_handler_error', function(e,message:string)
			{
				try
				{
					callback( message );
				}
				catch(err)
				{
					client.fatalError( 'Error while invoking commonlibs_message_handler_error event: '+err );
				}
			} );
		return self;
	}
}

export default BaseClient;
