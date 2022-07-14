//
// CommonLibs/Utils/Event/JS/EventsHandler.ts
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

export interface IEventsHandler
{
	bind	: (name:string, callback:(evt:any,p?:any)=>void)=>this;
	unbind	: (name:string, callback?:(evt:any,p?:any)=>void)=>this;
	trigger	: (name:string, p?:any)=>this;
}

/** Drop-in replacement for JQuery events */
export class EventsHandler implements IEventsHandler
{
	private readonly bindings	: { [key:string] : ((evt:any,p?:any)=>void)[] }	= {};

	public bind(name:string, callback:(evt:any,p?:any)=>void) : this
	{
		const self = this;

		let callbacksList = self.bindings[ name ];
		if( callbacksList == null )
		{
			callbacksList = [];
			self.bindings[ name ] = callbacksList;
		}

		callbacksList.push( callback );

		return self;
	}

	public unbind(name:string, callback?:(evt:any,p?:any)=>void) : this
	{
		const self = this;

		if( callback == null )
		{
			// Delete the whole list
			delete self.bindings[ name ];
			return;
		}

		const callbacks = self.bindings[ name ];
		if( callbacks == null )
			// Not found
			return;
		const idx = callbacks.indexOf( callback );
		if( idx < 0 )
			// Not found
			return;
		if( callbacks.length == 1 )
			// List with this element only => delete the whole list
			delete self.bindings[ name ];
		else
			// Remove single element
			callbacks.splice( idx, 1 );

		return self;
	}

	public trigger(name:string, p?:any) : this
	{
		const self = this;

		// Get callbacks list linked to this 'name'
		let callbacks = self.bindings[ name ];
		if( callbacks == null )
			return;

		// Invoke all callbacks
		for( let i=0; i<callbacks.length; ++i )
			(callbacks[i])( /*event*/'DUMMY', p );

		return self;
	}
}
