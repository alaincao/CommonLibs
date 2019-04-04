//
// CommonLibs/Utils/Event/JS/EventsHandler.ts
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
