//
// CommonLibs/Web/LongPolling/JSClient/LongPollingFileUploader.js
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

function LongPollingFileUploader($uploadControl, messageHandler, options)
{
	// Static variables
	if( window.LongPollingFileUploader_LastID == null )
		window.LongPollingFileUploader_LastID = 0;
	LongPollingFileUploader_LastID = LongPollingFileUploader_LastID + 1;

	// Member variables
	var self = $({});
	self.id = LongPollingFileUploader_LastID;  // The unique ID (in this page) of this control
	self.iframeID = 'LongPollingFileUploader_IFrame_' + self.id;
	self.fileInputName = 'LongPollingFileUploader_Input_' + self.id;
	self.serverSideMsgHandlerType = 'FileUpload';  // String that identifies the server-side message handler for custom messages
	self.serverSideRequestHandlerType = 'FileUpload';  // String that identifies the synced handler (c.f. CommonLibs.Web.LongPolling.SyncedHttpHandler)
	self.serverSideCustomObjectName = self.serverSideRequestHandlerType;  // String that identifies the uploader in server-side
	if( options && options['serverSideRequestHandlerType'] )
		// Use the same as 'serverSideRequestHandlerType' except when in is overriden
		self.serverSideCustomObjectName = options['serverSideRequestHandlerType'];
	self.clientSideMsgHandlerType = 'FileUpload_' + self.id;
	self.urlParameters = {};  // Values sent as query parameters to the posting URL (NB: Values set below)

	// Bind to this event to receive internal errors.
	// Parameter: The error description
	self.internalErrorEvent = 'file_uploader_internal_error';
	// Bind to this event to be notified when the internal iframe has been created
	// Parameter: null
	self.iframeCreatedEvent = 'file_uploader_iframe_created';
	// Bind to this event to be notified when the internal iframe has been refreshed
	// Parameter: null
	self.iframeLoadedEvent = 'file_uploader_iframe_loaded';
	// Bind to this event to receive notification when the mouse cursor enters the $uploadControl
	// Parameter: null
	self.mouseInEvent = 'file_uploader_mouse_in';
	// Bind to this event to receive notification when the mouse cursor leaves the $uploadControl
	// Parameter: null
	self.mouseOutEvent = 'file_uploader_mouse_out';
	// Bind to this event to receive the file name as soon as the user has chosen it
	// Parameter: (string) The file name
	self.fileChangedEvent = 'file_uploader_changed';

	$.extend( self, options );

	// Following events are had-coded in C# => Not '$.extend()'able

	// Bind to this event to be notified when an upload has started
	// Parameter 'FileName' (string): The name of the file uploading
	self.startEvent = 'Start';
	// Bind to this event to be notified of upload progress
	// Parameter 'Current' (long): The current size uploaded
	// Parameter 'Total' (long): The total size of the file uploading
	// Parameter 'FileName' (string): The name of the file uploading
	self.progressEvent = 'Progress';
	// Bind to this event to be notified the upload has terminated
	// Parameter 'Success' (boolean): 'true' if the upload suceeded, 'false' if not
	// Parameter 'FileName' (string): The name of the file uploading
	self.terminatedEvent = 'Finish';

	self.messageHandler = messageHandler;
	self.$uploadControl = $uploadControl;
	self.$iframe = null;
	self.$buttonDiv = null;
	self.$form = null;
	self.$fileInput = null;
	self.isDisabled = false;
	self.isUploading = false;
	self.isTrackingMouse = false;
	self.mouseMoveCallback = null;
	self.trackTop = 0;
	self.trackBottom = 0;
	self.trackLeft = 0;
	self.trackRight = 0;
	self.buttonDivXMouseOffset = 0;
	self.buttonDivYMouseOffset = 0;

	// Create posting URL parameters
	if( self.urlParameters['Type'] != null )
		// Use the 'Type' from 'urlParameters'
		self.serverSideRequestHandlerType = self.urlParameters['Type'];
	else
		// Use the 'Type' from 'serverSideRequestHandlerType'
		self.urlParameters['Type'] = self.serverSideRequestHandlerType;

	self.urlParameters['ConnectionID'] = null;  // NB: Will be set by the message handler's 'onConnectionIDReceived' event
	self.urlParameters['ResponseHandler'] = self.clientSideMsgHandlerType;
	self.urlParameters['FileID'] = self.serverSideCustomObjectName;

	self.sendMessage = function(messageType, messageDictionary)
	{
		if( messageDictionary == null )
			messageDictionary = {};

		messageDictionary['type'] = self.serverSideMsgHandlerType;
		messageDictionary['FileID'] = self.serverSideCustomObjectName;
		messageDictionary['FileUploadMessageType'] = messageType;
		self.messageHandler.sendMessage( messageDictionary );
	}

	self.abort = function()
	{
		self.sendMessage( 'Abort' );
	}

	self.disable = function()
	{
		self.isDisabled = true;
	}

	self.onDocumentMouseMove = function( ui )
	{
		if( self.isDisabled == true )
			return;

		if(! self.isTrackingMouse )
			// The cursor is outside the $buttonDiv
			return;

		if( self.isUploading )
		{
			// Currently uploading a file => Hide the file button and discard this event
			self.isTrackingMouse = false;
			self.$buttonDiv	.css( 'top', '-100px' )
							.css( 'left', '-100px' );
		}

		if( (ui.pageY < self.trackTop)
			|| (ui.pageY > self.trackBottom)
			|| (ui.pageX < self.trackLeft)
			|| (ui.pageX > self.trackRight) )
		{
			// The cursor left the $buttonDiv => Unregister the callback and hide the $uploadControl
			self.isTrackingMouse = false;
			self.$buttonDiv	.css( 'top', '-100px' )
							.css( 'left', '-100px' );

			try { self.trigger( self.mouseOutEvent, null ); }
			catch(err) { try { self.trigger( self.internalErrorEvent, '"self.trigger( self.mouseOutEvent, null )" threw an error: ' + err ); } catch(err) {}; }
			return;
		}

		// Move the $uploadControl so that it is under the cursor
		self.$buttonDiv	.css( 'top', ''+(ui.pageY-self.buttonDivYMouseOffset)+'px' )
						.css( 'left', ''+(ui.pageX-self.buttonDivXMouseOffset)+'px' );
	}

	self.onFileChange = function(fileName)
	{
		// Extract file name from full path
		var startIndex = (fileName.indexOf('\\') >= 0 ? fileName.lastIndexOf('\\') : fileName.lastIndexOf('/'));
		fileName = fileName.substring(startIndex);
		if (fileName.indexOf('\\') === 0 || fileName.indexOf('/') === 0)
			fileName = fileName.substring(1);

		// Trigger fileChanged event
		try { self.trigger( self.fileChangedEvent, fileName ); }
		catch(err) { try { self.trigger( self.internalErrorEvent, '"self.trigger( self.mouseOutEvent, null )" threw an error: ' + err ); } catch(err) {}; }

		// Set the form's posting URL
		var formAction = self.messageHandler.getSyncedHandlerUrl() + '?' + $.param( self.urlParameters );
		self.$form.attr( 'action', formAction );

		// Send file
		self.isUploading = true;
		self.$form[0].submit();
	};

	self.resetIFrame = function()
	{
		if( self.$iframe != null )
		{
			// Remove the old iframe
			try { self.$iframe.remove(); }
			catch(err) { try { self.trigger( self.internalErrorEvent, '"self.$iframe.remove()" threw an error: ' + err ); } catch(err) {}; }
		}

		// Create the iframe that will receive the file input's postback
		self.$iframe = $('<iframe name="' + self.iframeID + '"/>')	//.attr( 'name', self.iframeID )   <= IE7 cannot set name="" to iframes => must be done literaly when creating the tag !!!
										.attr( 'src', 'about:blank' )
										.css( 'top', '-100px' )
										.css( 'left', '-100px' )
										.css( 'width', '10px' )
										.css( 'height', '10px' )
										.css( 'position', 'absolute' )
										.load( function() { self.trigger(self.iframeLoadedEvent); } );
		$('body').append( self.$iframe );

		if( self.$form == null )
		{
			// Create the div that will contain the upload button
			self.$fileInput = $('<input/>')	.attr( 'type', 'file' )  // <input type="file" name="testFile" onchange="this.form.submit();" />
											.attr( 'name', self.fileInputName )
											.attr( 'id', self.fileInputName )
											.change( function(e,p){ self.onFileChange(e.currentTarget.value); } );
			self.$buttonDiv = $('<div/>')	.css( 'position', 'absolute' )
											.css( 'top', '-100px' )
											.css( 'left', '-100px' )
											.css( 'opacity', '0' )
											.zIndex( self.$uploadControl.zIndex()+1 )  // Place this div above the $uploadControl
											.append( self.$fileInput );
			self.$form = $('<form/>')	.attr( 'action', 'about:blank' )  // Will be set right before posting
										.attr( 'target', self.iframeID )
										.attr( 'method', 'post' )
										.attr( 'enctype', 'multipart/form-data' )
										.append( self.$buttonDiv );
			$('body')	.append( self.$form );
			self.buttonDivXMouseOffset = (self.$buttonDiv.outerWidth() - 5);
			self.buttonDivYMouseOffset = (self.$buttonDiv.outerHeight() - 5);

			// Start tracking mouse when the cursor enters inside $uploadControl
			$(document)	.mousemove( self.onDocumentMouseMove );

			self.$uploadControl	.mousemove( function(ui)
											{
												if( self.isTrackingMouse == true )
													// Already tracking the mouse move
													return;
												// Mouse entered the $uploadControl

												if( self.isUploading == true )
													// Currently downloading a file => Discard the event
													return;

												// Readjusting the tracking borders coordinates
												var position = self.$uploadControl.offset();
												self.trackTop = position.top;
												self.trackBottom = position.top + self.$uploadControl.outerHeight();
												self.trackLeft = position.left;
												self.trackRight = position.left + self.$uploadControl.outerWidth();
												self.isTrackingMouse = true;

												self.onDocumentMouseMove( ui );

												try { self.trigger( self.mouseInEvent, null ); }
												catch(err) { try { self.trigger( self.internalErrorEvent, '"self.trigger( self.mouseInEvent, null )" threw an error: ' + err ); } catch(err) {}; }
											} );
		}

		self.trigger( self.iframeCreatedEvent );
	}

	// Wait for the MessageHandler to receive a ConnectionID from the server to initialize this Uploader (the posting URL won't be valid without it)
	self.messageHandler.onConnectionIDReceived( function(connectionID)
		{
			self.urlParameters['ConnectionID'] = connectionID;
			self.resetIFrame();
		} );

	// Attach to the MessageHandler to receive messages destined to this object
	self.messageHandler.bind( self.clientSideMsgHandlerType, function(e,message)
		{
			var messageType = message['FileUploadMessageType'];
			if( messageType == null )
			{
				self.trigger( self.internalErrorEvent, 'Received message does not contain parameter "FileUploadMessageType' );
				return;
			}

			var customObjectName = message['FileID'];
			if( customObjectName != null )
			{
				// The server sent its CustomObjectName so we can send messages back to it
				self.serverSideCustomObjectName = customObjectName;
				self.urlParameters['FileID'] = customObjectName;
			}

			if( messageType == self.terminatedEvent )
			{
				self.isUploading = false;
				var exceptionMessage = message['Exception'];
				if( exceptionMessage != null )
					self.trigger( self.internalErrorEvent, exceptionMessage );
			}

			// Redirect to events triggered by 'self'
			self.trigger( messageType, message );
		} );

	return self;
}
