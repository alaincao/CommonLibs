//
// CommonLibs/Web/LongPolling/JSClient/WebSocketClient.ts
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2016 SigmaConso
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

import { MessageHandler, EventsHandler, ClientStatus, Message, createEventHandler } from "./Client";

declare var $: any;

export function error(message:string)
{
	alert( message );
	throw message;
}

export class WebSocketClient implements MessageHandler
{
	private	events				: EventsHandler;
	public	debug				: boolean;

	private	handlerUrl			: string;
	private	logoutUrl			: string;
	private	keepAliveUrl		: string;
	private	keepAliveTimeout	: number;
	private	keepAliveTimer		: number;
	private	syncedHandlerUrl	: string;
	private	status				: ClientStatus;
	private	connectionId		: string;
	private	webSocket			: WebSocket;
	private	sendMessageUid		: number;
	private	pendingMessages		: { message:Message, callback?:(evt?:any,message?:Message)=>void }[];

	public	start						: ()=>void;
	public	sendMessage					: (message:Message, callback?:(evt?:any,message?:Message)=>void)=>WebSocketClient;
	private sendPendingMessages			: ()=>void;
	public	getConnectionId				: ()=>string;
	public	getStatus					: ()=>ClientStatus;
	private	checkKeepAliveTimeout		: ()=>void;
	public	getSyncedHandlerUrl			: ()=>string;

	// For EventsHandler:
	public	bind						: (name:string, callback:(evt?:any,message?:Message)=>void)=>EventsHandler;
	public	unbind						: (name:string, callback?:(evt?:any)=>void)=>EventsHandler;
	public	trigger						: (name:string, p?:any)=>EventsHandler;

	// Events:
	private	triggerStatusChanged		: (status:ClientStatus)=>void;
	public	onStatusChanged				: (callback:(status:ClientStatus)=>void)=>WebSocketClient;
	private	triggerInternalError		: (message:string)=>void;
	public	onInternalError				: (callback:(message:string)=>void)=>WebSocketClient;
	private	triggerMessageHandlerFailed	: (error:any)=>void;
	public	onMessageHandlerFailed		: (callback:(error:any)=>void)=>WebSocketClient;
	private	triggerConnectionIdReceived : (connectionId:string)=>void;
	public	onConnectionIdReceived		: (callback:(connectionId:string)=>void)=>WebSocketClient;

	constructor(p:{	debug?				: boolean,
					handlerUrl			: string,	// Required: The URL of the server's WebSocket listener
					keepAliveUrl?		: string,	// Optional: Set to create a dummy HTTP request to keep the ASP.NET session alive
					keepAliveTimeout?	: number,	// Optional: If 'keepAliveUrl' is set, this will be the interval between the dummy HTTP requests (default: 10*60*1000 miliseconds = 10 minutes)
					logoutUrl			: string,	// Required: The URL to redirect to when the server asks to logout
					syncedHandlerUrl?	: string })	// Optional: The URL to the Synced HTTP handler if used (e.g. file uploads)
	{
		p = $.extend( {	debug				: false,
						handlerUrl			: 'HANDLER_URL_UNDEFINED',
						keepAliveUrl		: null,
						keepAliveTimeout	: 600000,
						logoutUrl			: 'LOGOUT_URL_UNDEFINED',
						syncedHandlerUrl	: null,
					}, p );
		var self = this;

		self.events				= createEventHandler();
		self.debug				= (p.debug == true);
		self.handlerUrl			= p.handlerUrl;
		self.keepAliveUrl		= p.keepAliveUrl;
		self.keepAliveTimeout	= p.keepAliveTimeout;
		self.keepAliveTimer		= null;
		self.logoutUrl			= p.logoutUrl;
		self.syncedHandlerUrl	= p.syncedHandlerUrl;
		self.status				= ClientStatus.DISCONNECTED;
		self.connectionId		= null;
		self.webSocket			= null;
		self.sendMessageUid		= 0;
		self.pendingMessages	= [];

		// Redirect jQuery's methods to 'self.events'
		self.bind						= function(){ return self.events.bind.apply( self.events, arguments ); };
		self.unbind						= function(){ return self.events.unbind.apply( self.events, arguments ); };
		self.trigger					= function(){ return self.events.trigger.apply( self.events, arguments ); };

		// Misc
		self.getSyncedHandlerUrl = function(){ return self.syncedHandlerUrl; };

		// Init event handlers

		self.triggerInternalError = function(message:string)
			{
				self.events.trigger( 'long_polling_client_error', message );
			};
		self.onInternalError = function(callback:(message:string)=>void)
			{
				self.events.bind( 'long_polling_client_error', function(e,message:string)
					{
						try
						{
							callback(message);
						}
						catch(err)
						{
							// NB: Really not much we can do here ...
							if( (console != null) && (console.error != null) )
								console.error( 'Error while invoking long_polling_client_error event', err );
						}
					} );
				return self;
			};

		self.triggerStatusChanged = function(status:ClientStatus)
			{
				self.events.trigger( 'long_polling_client_status_changed', status );
			}
		self.onStatusChanged = function(callback:(status:ClientStatus)=>void)
			{
				self.events.bind( 'long_polling_client_status_changed', function(evt,status:ClientStatus)
					{
						try { callback(status); }
						catch(err) { self.triggerInternalError( 'Error while invoking long_polling_client_status_changed event: '+err ); }
					} );
				return self;
			};

		self.triggerMessageHandlerFailed = function(error:any)
			{
				self.events.trigger( 'message_handler_failed', error );
			}
		self.onMessageHandlerFailed = function(callback:(message:string)=>void)
			{
				self.events.bind( 'message_handler_failed', function(evt,error:any)
					{
						try { callback(error); }
						catch(err) { self.triggerInternalError( 'Error while invoking message_handler_failed event: '+err ); }
					} );
				return self;
			};

		self.triggerConnectionIdReceived = function(connectionId:string)
			{
				self.events.trigger( 'long_polling_client_connection_id', connectionId );
			};
		self.onConnectionIdReceived = function(callback:(connectionId:string)=>void)
			{
				if( self.connectionId != null )
				{
					// Already received
					callback( self.connectionId );
					return;
				}
				else // Defer
				{
					self.events.bind( 'long_polling_client_connection_id', function(evt,connectionId:string)
						{
							try { callback(connectionId); }
							catch(err) { self.triggerInternalError( 'Error while invoking long_polling_client_connection_id event: '+err ); }
						} );
				}
				return self;
			};

		self.getConnectionId			= function(){ return self.connectionId; };
		self.getStatus					= function(){ return self.status; };

		// Init WebSocket communications

		var onSocketError = function(e:any)
			{
				if( self.debug )
					console.log( 'WebSocket error', e );

				self.triggerInternalError( 'WebSocket error: '+e.message );
			};

		var onSocketClose = function()
			{
				if( self.debug )
					console.log( 'WebSocket closed' );

				self.connectionId = null;
				self.webSocket = null;

				self.status = ClientStatus.DISCONNECTED;
				self.triggerStatusChanged( self.status );
			};

		var onSocketMessageInit = function(e:any)
			{
				try
				{
					var errorPrefix = 'JSON Parse Error: ';
					var message = JSON.parse( e.data );
					var errorPrefix = '';

					var type = message['type'];
					if( type != 'init' )
						throw 'Unexpected message type "'+type+'" ; Expected "init"';

					var connectionId = message['sender'];
					if( connectionId == null )
						throw 'Server did not responded with ConnectionID';
					self.connectionId = connectionId;

					if( self.debug )
						console.log( 'message recv', message );

					// Ready to receive & subsequent messages must go to the 'onMessage' handler
					self.status = ClientStatus.CONNECTED;
					self.webSocket.onmessage = onSocketMessage;

					// Start keepalive timer
					var errorPrefix = 'Could not start keepalive timer: ';
					self.checkKeepAliveTimeout();
				}
				catch( err )
				{
					try { self.triggerInternalError( errorPrefix+err ); } catch(err) {}
					return;  // Fatal ; Stop here ...
				}

				// Invoke events
				self.triggerStatusChanged( self.status );
				self.triggerConnectionIdReceived( self.connectionId );

				// Check for any pending messages
				self.sendPendingMessages();
			};

		var onSocketMessage = function(e:any)
			{
				try
				{
					var errorPrefix = 'JSON Parse Error: ';
					var response = JSON.parse( e.data );
					errorPrefix = '';

					var responseType = response[ 'type' ];
					if( responseType == 'reset' )
					{
						// Ignore (should not happen?)
					}
					else if( responseType == 'logout' )
					{
						window.location.href = self.logoutUrl;
					}
					else if( responseType == 'messages' )
					{
						var messagesList = response[ 'messages' ];
						for( var i=0; i<messagesList.length; ++i )
						{
							try
							{
								var messageContent = messagesList[ i ];
								var type = messageContent[ 'type' ];

								if( self.debug )
									console.log( 'message recv', messageContent );

								self.trigger( type, messageContent );
							}
							catch( err )
							{
								// The assigned message handler threw an exception
								self.triggerMessageHandlerFailed( err );
							}
						}
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
			};

		self.start = function()
			{
				if( self.debug )
					console.log( 'WebSocket connecting "'+self.handlerUrl+'"' );
				self.webSocket = new WebSocket( self.handlerUrl );
				self.webSocket.onerror = onSocketError;
				self.webSocket.onclose = onSocketClose;
				self.webSocket.onmessage = onSocketMessageInit;
				self.webSocket.onopen = function()
					{
						var message = { 'type' : 'init' };
						if( self.debug )
							console.log( 'message send', message );
						// Send init message
						self.webSocket.send( JSON.stringify(message) );
					};
			};

		self.checkKeepAliveTimeout = function()
			{
				if( self.keepAliveUrl == null )
					// Nothing to do
					return;

				if( self.getStatus() == ClientStatus.CONNECTED )
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
							var req = new XMLHttpRequest();
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
						var tm = self.keepAliveTimer;
						self.keepAliveTimer = null;
						clearTimeout( tm );
					}
				}
			};

		self.sendMessage = function(message:Message, callback?:(evt?:any,message?:Message)=>void)
			{
				self.pendingMessages.push({ message:message, callback:callback });
				self.sendPendingMessages();
				return self;
			};

		self.sendPendingMessages = function() : void
			{
				if( self.getStatus() != ClientStatus.CONNECTED )
					// Connection not ready
					return;

				var items = self.pendingMessages;
				self.pendingMessages = [];

				for( var i=0; i<items.length; ++i )
				{
					try
					{
						var message		= items[i].message;
						var callback	= items[i].callback;

						if( callback != null )
						{
							// Attach the callback to a new one-shot message handler
							var replyMessageHandler = 'message_handler_autoreply_' + (++ self.sendMessageUid);
							message[ 'reply_to_type' ] = replyMessageHandler;

							self.bind( replyMessageHandler, function(evt, message)
								{
									// The message has returned => unbind the one-shot message handler
									self.unbind( replyMessageHandler );

									// Forward the message to the callback
									callback( evt, message );
								} );
						}

						var rootMessage = {	'type'		: 'messages',
											'messages'	: [ message ] };

						if( self.debug )
							console.log( 'message send', message );

						var strMessage = JSON.stringify( rootMessage );
						self.webSocket.send( strMessage );
					}
					catch( err )
					{
						try { self.triggerInternalError( 'Unable to send message: '+err ); } catch(err) {}
					}
				}
			};

		/////////////////////

		// Check URL's protocol
		var url = document.createElement( 'a' );
		url.href = self.handlerUrl;
		if( (url.protocol != 'wss:') && (url.protocol != 'ws:') )
		{
			if( url.protocol == 'https:' )
				url.protocol = 'wss:';
			else
				url.protocol = 'ws:';
		}
		self.handlerUrl = url.href;
	}
}  // class WebSocketClient

export default WebSocketClient;
