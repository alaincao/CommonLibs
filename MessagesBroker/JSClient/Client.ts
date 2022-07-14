//
// CommonLibs/MessagesBroker/JSClient/Client.ts
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

import * as evnt from '../../Utils/Event/JS/EventsHandler';
import * as http from './HttpClient';
import BaseClient from './BaseClient';

export { IEventsHandler as EventsHandler } from '../../Utils/Event/JS/EventsHandler';

export var HttpClient = http.HttpClient;

export interface Message
{
	type?				: string;
	reply_to_type?		: string;
	sender?				: string;
	receiver?			: string;
}
// Use this when members are not to be strictly enforced (behaves as a regular dictionary):
export interface MessageDict extends Message
{
	[key:string]:any;
}

export interface MessageHandler extends evnt.IEventsHandler
{
	getStatus				: ()=>ClientStatus;
	onConnectionIdReceived	: (callback:(connectionId:string)=>void)=>this;
	onStatusChanged			: (callback:(status:ClientStatus)=>void)=>this;
	sendMessage				: (message:Message, callback?:(evt:any,message?:Message)=>void)=>this;
	sendRequest				: <T extends Message>(request:Message)=>Promise<T>;
}

export enum ClientStatus
{
	DISCONNECTED,	// When the connection to the server is not yet established or is lost.
	CONNECTED,		// When there is a polling request currently connected to the server.
	SENDING,		// When a message request is currently sending messages to the server.
};

/** Last resort error handler => Should never been called ... */
export function fatalError(message:string)
{
	if( (console != null) && (console.error != null) )
	{
		console.error( 'Fatal error:', message );
	}
	else
	{
		// NB: Really not much more we can do here ...
		try { alert( message ); }
		catch( err ) {}
	}
}

export function createEventHandler() : evnt.IEventsHandler
{
	return new evnt.EventsHandler;
}

export function Client(p:{	debug?						: boolean,
							httpHandlerUrl?				: string,
							initMessageTemplate?		: {[key:string]:any},
					}) : BaseClient
{
	const canUseHttp : boolean = (p.httpHandlerUrl != null);

	if( p.debug )
	{
		// Check that console is available
		if( (typeof(console) == 'undefined') || (typeof(console.log) == 'undefined') )
			// Debugging not available
			p.debug = false;
	}

	let client : BaseClient;
	if( canUseHttp )
	{
		client = new http.HttpClient({ debug:p.debug, handlerUrl:p.httpHandlerUrl, initMessageTemplate:p.initMessageTemplate });
	}
	else
	{
		throw 'Unable to find a suitable message handler protocol';
	}

	client.onInternalError( (message)=>
		{
			if( (console != null) && (console.error != null) )
				console.error( message );
		} );
	return client;
}

export default Client;
