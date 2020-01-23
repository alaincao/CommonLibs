//
// CommonLibs/MessagesBroker/JSClient/Client.ts
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2019 SigmaConso
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
		client = new http.HttpClient({ debug:p.debug, handlerUrl:p.httpHandlerUrl });
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
