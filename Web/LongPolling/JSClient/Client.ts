import * as sock from './WebSocketClient';
import * as http from './HttpClient';

export var HttpClient = http.HttpClient;
export var WebSocketClient = sock.WebSocketClient;

export interface Message
{
	type				: string;
	reply_to_type?		: string;
	sender?				: string;
	chained_messages?	: Message[];
}
// Use this when members are not to be strictly enforced (behaves as a regular dictionary):
export interface MessageDict extends Message
{
	[key:string]:any;
}

export interface EventsHandler
{
	bind	: (name:string, callback:(evt?:any,p?:any)=>void)=>EventsHandler;
	unbind	: (name:string, callback?:(evt?:any,p?:any)=>void)=>EventsHandler;
	trigger	: (name:string, p?:any)=>EventsHandler;
}

export interface MessageHandler
{
	start					: ()=>void;
	getStatus				: ()=>ClientStatus;
	getSyncedHandlerUrl		: ()=>string;
	onConnectionIdReceived	: (callback:(connectionId:string)=>void)=>MessageHandler;
	onStatusChanged			: (callback:(status:ClientStatus)=>void)=>MessageHandler;
	onInternalError			: (callback:(message:string)=>void)=>MessageHandler;
	onMessageHandlerFailed	: (callback:(error:any)=>void)=>MessageHandler;
	bind					: (name:string, callback:(evt?:any,message?:Message)=>void)=>EventsHandler;
	unbind					: (name:string, callback?:(evt?:any)=>void)=>EventsHandler;
	sendMessage				: (message:Message, callback?:(evt?:any,message?:Message)=>void)=>MessageHandler;
}

export enum ClientStatus
{
	DISCONNECTED,	// When the connection to the server is not yet established or is lost
	CONNECTED,		// When there is a polling request currently connected to the server.
	PENDING,		// (NB: Only used by HttpClient) When the message request is currently sending messages to the server and there are messages pending in the queue
	RUNNING,		// (NB: Only used by HttpClient) When the message request is currently sending messages to the server.
};

export function createEventHandler() : EventsHandler
{
	return $({});
}

export function Client(p:{	debug?						: boolean,
							httpHandlerUrl?				: string,
							webSocketHandlerUrl?		: string,
							webSocketKeepAliveUrl?		: string,
							webSocketKeepAliveTimeout?	: number,
							syncedHandlerUrl?			: string,
							logoutUrl					: string
					}) : MessageHandler
{
	var canUseSocket : boolean = (p.webSocketHandlerUrl != null);
	canUseSocket = ( canUseSocket && (typeof(WebSocket) != 'undefined') );
	var canUseHttp : boolean = (p.httpHandlerUrl != null);

	if( p.debug )
	{
		// Check that console is available
		if( (typeof(console) == 'undefined') || (typeof(console.log) == 'undefined') )
			// Debugging not available
			p.debug = false;
	}

	if( canUseSocket )
	{
		return new sock.WebSocketClient({ debug:p.debug, handlerUrl:p.webSocketHandlerUrl, keepAliveUrl:p.webSocketKeepAliveUrl, keepAliveTimeout:p.webSocketKeepAliveTimeout, syncedHandlerUrl:p.syncedHandlerUrl, logoutUrl:p.logoutUrl });
	}
	else if( canUseHttp )
	{
		var client = http.HttpClient( p.httpHandlerUrl, p.syncedHandlerUrl, p.logoutUrl, p.debug );

		// TODO: Alain: Cleanup 'HttpClient' & include 'sendMessage(message, callback)'

		// Override 'client.sendMessage()' to support a second argument: 'callback'
		var sendMessageUid = 0;
		var baseSendMessage = client.sendMessage;
		client.sendMessage = function(message:Message, callback:any)
			{
				if( callback != null )
				{
					// Attach the callback to a new one-shot message handler
					var replyMessageHandler = 'commonlibs_message_handler_autoreply_' + (++ sendMessageUid);
					message[ 'reply_to_type' ] = replyMessageHandler;

					client.bind( replyMessageHandler, function(evt:any, message:Message)
						{
							// The message has returned => unbind the one-shot message handler
							client.unbind( replyMessageHandler );

							// Forward the message to the callback
							callback( evt, message );
						} );
				}

				// Invoke original 'sendMessage()' function
				baseSendMessage( message );
			};

		return client;
	}
	else
	{
		sock.error( 'Unable to find a suitable message handler protocol' );  // NB: Temporarily use the same error handler (until the whole 'utils.*.ts' are merged)
		return null;
	}
}

export default Client;
