//
// CommonLibs/Web/LongPolling/JSClient/LongPollingClient.js
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2013 Alain CAO
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

function LongPollingClient(handlerUrl, syncedHandlerUrl, logoutUrl)
{
	var $this = $(this);
	this.$this = $this;
	var thisDOM = this;

	///////////////////////////
	// Reserved event names: //
	///////////////////////////

	// Bind to this event to be warned of changes in the connection status
	// Event parameter:	'CONNECTED' when there is a polling request currently connected to the server.
	//					'DISCONNECTED' when the connection to the server is lost (NB: Try run the 'verifyConnections()' method to try to reconnect to the server).
	//					'RUNNING' when the message request is currently sending messages to the server.
	//					'PENDING' when the message request is currently sending messages to the server and there are messages pending in the queue
	$this.statusChangedEvent = "long_polling_client_status_changed";

	// Bind to this event to receive errors when an assigned message handler threw an exception.
	// Event parameter: The error object threw by the assigned message handler as receivd by the catch().
	$this.messageHandlerFailedEvent = 'message_handler_failed';

	// Bind to this event to get error messages when an internal error occures inside the LongPollingClient object
	// Event parameter: A description string of the error
	$this.internalErrorEvent = 'long_polling_client_error';

	// Bind to this event to receive the ConnectionID that has been assigned to this LongPollingClient as soon as it has been received
	// Event parameter: The ConnectionID (string)
	$this.connectionIDReceivedEvent = 'long_polling_client_connection_id';

	///////////////////////
	// Member variables: //
	///////////////////////

	this.__status					= 'DISCONNECTED';

	// URL of the server-side message handler
	this.__handlerUrl				= handlerUrl;
	// URL of the server-side synced handler
	this.__syncedHandlerUrl			= syncedHandlerUrl;
	// URL of the logout page
	this.__logoutUrl				= logoutUrl;

	// The only 2 HTTP requests that can be running at the same time:
	this.__pollingRequest			= null;
	this.__messageRequest			= null;

	// This client's SenderID
	this.__connectionID				= null;

	// The list of messages that are waiting for the __messageRequest to be available
	this.__pendingMessages			= null;

	//////////////
	// Methods: //
	//////////////

	this.getSyncedHandlerUrl = function()
	{
		return this.__syncedHandlerUrl;
	}

	this.getStatus = function()
	{
		var newStatus;
		if( this.__pollingRequest == null )
		{
			newStatus = 'DISCONNECTED';
		}
		else if( this.__messageRequest != null )
		{
			if( this.__pendingMessages != null )
				newStatus = 'PENDING';
			else
				newStatus = 'RUNNING';
		}
		else
		{
			newStatus = 'CONNECTED';
		}

		if( this.__status != newStatus )
		{
			this.__status = newStatus;
			try { this.$this.trigger( this.$this.statusChangedEvent, newStatus ); } catch(err) {}
		}
		return newStatus;
	}

	this.onConnectionIDReceived = function(callback)
	{
		if( this.__connectionID != null )
		{
			// There is already a ConnectionID => Invoke callback right now
			try
			{
				callback( this.__connectionID );
			} catch( err ) {
				// The assigned message handler threw an exception
				try { this.$this.trigger( this.messageHandlerFailedEvent, err ); } catch(err) {}
			}
		}
		else
		{
			// The ConnectionID is not received yet => Bind the callback to the 'connectionIDReceivedEvent' trigger
			this.$this.bind( this.$this.connectionIDReceivedEvent, (function(cb)
						{
							return function(evt,connectionID)
							{
								cb( connectionID );
							}
						})(callback) );
		}
	}

	this.verifyConnections = function()
	{
		if( this.__pollingRequest == null )
		{
			// The polling request must be (re)started => Send a simple poll request
//console.log( 'starting poll' );
			var message = {	'type': 'poll',
							'sender': this.__connectionID };
			this.__pollingRequest = new XMLHttpRequest();
			this.__send( this.__pollingRequest, message );
		}
		if( this.__messageRequest == null )
		{
			// There is no request currently running

			if( this.__pendingMessages == null )
				// No pending message
				return;
			if( this.__connectionID == null )
				// No ConnectionID available yet (wait for init() to terminate...)
				return;

			// Create message list with all the pending messages
			var messageContents = [];
			for( var i=0; i<this.__pendingMessages.length; ++i )
			{
				var messageItem = this.__pendingMessages[ i ];

				// Add message content to send
				var message = messageItem[ 'content' ];
				messageContents.push( message );
			}

			// Send message
//console.log( 'starting message' );
			var message = {	'type': 'messages',
							'sender': this.__connectionID,
							'messages': messageContents };
			this.__messageRequest = new XMLHttpRequest();
			this.__send( this.__messageRequest, message );
			this.__pendingMessages = null;  // No more pending messages
		}
	}

	this.sendMessages = function(messages)
	{
		if( this.__pendingMessages == null )
			this.__pendingMessages = [];

		for( var i=0; i<messages.length; ++i )
		{
			var messageItem = { 'content': messages[i] };
			this.__pendingMessages.push( messageItem );
		}

		// Send pending messages if the request is available
		this.verifyConnections();
		this.getStatus();
	}

	this.start = function()
	{
		var self = this;

		// Start the __pollingRequest
		var message = { 'type': 'init' };
		var pendingQuery = new XMLHttpRequest();
		self.__pollingRequest = pendingQuery;  // Assign this member immediately so that 'verifyConnections()' doesn't create its own query
		var sendRequestFunction = function()
			{
				self.__send( pendingQuery, message );
			};

//		if( $.browser.safari || $.browser.opera )
//		{
//			// Opera & Safari thinks the page is still loading until all the initial requests are terminated (which never happens in case of a long-polling...)
//			// => Those browsers shows the 'turning wait icon' indefinately (Safari) or even worse never show the page! (Opera)

//			// Add a delay before sending the initial long-polling query
//			self.$this.delay( 300 ).queue( function(){ sendRequestFunction(); } );
//		}
//		else
//		{
			// Other browsers => sending the initial long-polling query immediately
			sendRequestFunction();
//		}

		$(window).unload( function()
			{
				// When leaving the page, explicitly abort the polling requests because IE keeps them alive!!!
				try { self.__pollingRequest.abort(); }
				catch( err ) {}

				// Kill also the "request" request if any
				try { self.__messageRequest.abort(); }
				catch( err ) {}
			} );
	}

	this.__send = function(requestObject, messageObject)
	{
		var strMessageObject = JSON.stringify( messageObject );
		requestObject.open( "POST", this.__handlerUrl, true );
		requestObject.setRequestHeader( "Content-Type", "application/x-www-form-urlencoded" );

		var callback = (function(self,req) {
				return function() { self.__onRequestStateChange(req); };
			})( this, requestObject );
		requestObject.onreadystatechange = callback;

		requestObject.send( strMessageObject );
	};

	this.__onRequestStateChange = function(request)
	{
		try
		{
			if( request.readyState == 4 )
			{
				if( request == this.__pollingRequest )
				{
					// The __pollingRequest ended
					this.__pollingRequest = null;
				}
				else if( request == this.__messageRequest )
				{
					// The __messageRequest ended
					this.__messageRequest = null;
				}
				else
				{
					try { this.$this.trigger( this.$this.internalErrorEvent, 'Received a response from an unknown request' ); } catch(err) {}
				}

				if( request.status == 200 )  // HTTP 200 OK
				{
					var strResponse = request.responseText;
					var response;
					try
					{
						response = JSON.parse( strResponse );
						var responseType = response[ 'type' ];
						if( responseType == 'init' )
						{
							this.__connectionID = response[ 'sender' ];

							try
							{
								this.$this.trigger( this.$this.connectionIDReceivedEvent, this.__connectionID );
							} catch( err ) {
								// The assigned message handler threw an exception
								try { this.$this.trigger( this.$this.messageHandlerFailedEvent, err ); } catch(err) {}
							}

							// Initiate initial connection
							this.verifyConnections();
						}
						else if( responseType == 'reset' )
						{
							// Restart the __pollingRequest
							this.verifyConnections();
						}
						else if( responseType == 'logout' )
						{
//console.log( '__onRequestStateChange - logout' );
							window.location = this.__logoutUrl;
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
									this.$this.trigger( type, messageContent );
								} catch( err ) {
									// The assigned message handler threw an exception
									try { this.$this.trigger( this.$this.messageHandlerFailedEvent, err ); } catch(err) {}
								}
							}
							this.verifyConnections();
						}
						else
						{
							throw 'Unknown response type \'' + responseType + '\'';
						}
					} catch( err ) {
						try { this.$this.trigger( this.$this.internalErrorEvent, 'JSON Parse Error: ' + err ); } catch(err) {}
						this.verifyConnections();
					}
				}
				else if( (request.status == 0/*All browsers*/) || (request.status == 12031/*Only IE*/) )
				{
					// Request aborted (e.g. redirecting to another page)
					// this.verifyConnections();  <= Don't try to reconnect
				}
				else if( request.status == 12002/*Only IE*/ )
				{
					// Request timeout
//console.log( '__onRequestStateChange - request.status=12002 (timeout)' );
// TODO: Alain: Warning (?)
					this.verifyConnections();  // Try reconnect
				}
				else
				{
					try { this.$this.trigger( this.$this.internalErrorEvent, 'Server error (status="' + request.status + '")' ); } catch(err) {}
// TODO: Alain: Maximum number of retry then definately disconnect
					//window.location = this.__logoutUrl;  // Redirect to logout page
					this.verifyConnections();  // Try reconnect
				}
			}
			else
			{
				// "readyState != 4" == still running
				// NOOP
			}
		}
		finally
		{
			this.getStatus();
		}
	};

	////////////////////////////////////////////////////////////////
	// Redirected functions 'jquery object' => 'original object': //
	////////////////////////////////////////////////////////////////

	$this.start						= function() { thisDOM.start(); };
	$this.getSyncedHandlerUrl		= function() { return thisDOM.getSyncedHandlerUrl(); };
	$this.getStatus					= function() { return thisDOM.getStatus(); };
	$this.sendMessage				= function(message) { thisDOM.sendMessages( [message] ); };
	$this.sendMessages				= function(messages) { thisDOM.sendMessages( messages ); };
	$this.verifyConnections			= function() { thisDOM.verifyConnections(); };
	$this.onConnectionIDReceived	= function(callback) { thisDOM.onConnectionIDReceived(callback); };

	/////////////////////
	// Initialization: //
	/////////////////////

	// Return the JQuery object
	return $this;
}
