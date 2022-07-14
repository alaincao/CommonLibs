//
// CommonLibs/Web/LongPolling/JSClient/LongPollingFileUploader.ts
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2013 Alain CAO
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

import { MessageHandler, Message, EventsHandler, createEventHandler } from './Client';

/** List of parameters used to identify the server's PageFile while uploading the file (i.e. the POST url) */
export interface UrlParameters
{
	ConnectionID	: string;  // The current MessageHandler's connection ID
	Type			: string;  // Will be given as value for parameter 'handlerType' of method 'SyncedHttpHandler.GetHandler()'
	ResponseHandler	: string;  // Will be used by the server as 'type' for messages it sends to the client (i.e. the client-side message handler assigned to the current instance)
	FileID			: string;  // Used to identify the uploader to use if more than 1 is present on the same page (i.e. on the same ConnectionID)
}

export interface Parameters
{
	// Required, manually defined
	messageHandler	: MessageHandler,
	$uploadControl	: JQuery,

	// Required, defined by C#'s 'GenericPageFile.GetQueryParameters()'
	urlParameters					: UrlParameters,

	iframeID?						: string,
	fileInputName?					: string,
	serverSideMsgHandlerType?		: string,
	serverSideRequestHandlerType?	: string,
	clientSideMsgHandlerType?		: string,
	serverSideCustomObjectName?		: string,
}

/** Prototype of messages exchanged between the client's FileUploader and server's PageFile instances */
interface UploaderMessage extends Message
{
	FileID					: string;
	FileUploadMessageType	: UploaderMessageType;

	// When comming from server:
	FileName?				: string;
	Exception?				: string;
	Current?				: number;
	Total?					: number;
	Success?				: boolean;
}
export const UploaderMessageTypes = strEnum([
		// Client message types:

		// When the 'abort()' method has been invoked
		'Abort',

		// Server message types:

		// When an upload has started
		// Parameter 'FileName': The name of the file uploading
		'Start',

		// Upload progress
		// Parameter 'FileName': The name of the file uploading
		// Parameter 'Current': The current size uploaded
		// Parameter 'Total': The total size of the file uploading
		'Progress',

		// When the upload has terminated
		// Parameter 'Success': 'true' if the upload suceeded, 'false' if not
		// Parameter 'FileName': The name of the file uploading
		'Finish',
	]);
type UploaderMessageType = keyof typeof UploaderMessageTypes;

var lastID : number;

export class FileUploader
{
	private		messageHandler		: MessageHandler;

	private		id								: number;
	private		iframeID						: string;
	private		fileInputName					: string;
	private		serverSideMsgHandlerType		: string;  // String that identifies the server-side message handler for custom messages
	private		serverSideRequestHandlerType	: string;  // String that identifies the synced handler (c.f. CommonLibs.Web.LongPolling.SyncedHttpHandler)
	private		clientSideMsgHandlerType		: string;
	private		serverSideCustomObjectName		: string;  // String that identifies the uploader in server-side
	public		urlParameters					: UrlParameters;  // Values sent as query parameters to the posting URL

	protected	$uploadControl			: JQuery;
	private		$buttonDiv				: JQuery;
	private		isDisabled				: boolean;
	private		isTrackingMouse			: boolean;
	private		isUploading				: boolean;
	private		trackTop				: number;
	private		trackBottom				: number;
	private		trackLeft				: number;
	private		trackRight				: number;
	private		buttonDivXMouseOffset	: number;
	private		buttonDivYMouseOffset	: number;
	private		$iframe					: JQuery;
	private		$form					: JQuery;
	protected	$fileInput				: JQuery;

	/** Override this method to validate the selected file before the upload */
	public validateSelectedFile : (fileName:string)=>boolean;
	/** Send a message to the server-side uploader PageFile instance */
	public sendMessage : (messageType:UploaderMessageType, messageDictionary?:UploaderMessage)=>void;
	/** Send an 'abort' message to the server-side uploader PageFile instance */
	public abort : ()=>void;
	/** Enable the upload HTML control ; override to implement the display behaviour */
	public enable : ()=>void;
	/** Disable the upload HTML control ; override to implement the display behaviour */
	public disable : ()=>void;

	private	documentMouseMoved	: (ui:any)=>void;
	private	fileSelected		: (fileName:string)=>void;
	private	resetIFrame			: ()=>void;

	private	events	: EventsHandler;
	/** Bind to this event to receive notification when the mouse cursor enters the $uploadControl */
	public	onMouseIn		: (callback:()=>void)=>FileUploader;
	private	triggerMouseIn	: ()=>void;
	/** Bind to this event to receive notification when the mouse cursor leaves the $uploadControl */
	public	onMouseOut		: (callback:()=>void)=>FileUploader;
	private	triggerMouseOut	: ()=>void;
	/** Bind to this event to be notified when the internal iframe has been created */
	public	onIframeCreated			: (callback:()=>void)=>FileUploader;
	private	triggerIframeCreated	: ()=>void;
	/** Bind to this event to be notified when the internal iframe has been refreshed */
	public	onIframeLoaded		: (callback:()=>void)=>FileUploader;
	private	triggerIframeLoaded	: ()=>void;
	/** Bind to this event to receive the file name as soon as the user has chosen it
	 * Parameter: the file name */
	public	onFileChanged		: (callback:(fileName:string)=>void)=>FileUploader;
	private	triggerFileChanged	: (fileName:string)=>void;
	/** Bind to this event to receive internal errors
	 * Parameter: The error description */
	public	onInternalError			: (callback:(message:string)=>void)=>FileUploader;
	private	triggerInternalError	: (message:string)=>void;
	/** Bind to this event to receive messages sent by the server */
	public	onServerMessageReceived			: (callback:(message:UploaderMessage)=>void)=>FileUploader;
	private	triggerServerMessageReceived	: (message:UploaderMessage)=>void;

	constructor(p:Parameters)
	{
		var self = this;
		lastID = (lastID == null) ? 1 : (lastID + 1);
		self.id = lastID;

		self.messageHandler = p.messageHandler;
		self.$uploadControl = p.$uploadControl;
	
		self.iframeID						= p.iframeID != null ? p.iframeID											: 'LongPollingFileUploader_IFrame_' + self.id;
		self.fileInputName					= p.fileInputName != null ? p.fileInputName									: 'LongPollingFileUploader_Input_' + self.id;
		self.serverSideMsgHandlerType		= p.serverSideMsgHandlerType != null ? p.serverSideMsgHandlerType			: 'FileUpload';
		self.serverSideRequestHandlerType	= p.serverSideRequestHandlerType != null ? p.serverSideRequestHandlerType	: 'FileUpload';
		self.clientSideMsgHandlerType		= p.clientSideMsgHandlerType != null ? p.clientSideMsgHandlerType			: 'FileUpload_' + self.id;
		self.serverSideCustomObjectName		= p.serverSideCustomObjectName != null ? p.serverSideCustomObjectName		: self.serverSideRequestHandlerType;
		self.urlParameters					= p.urlParameters;

		self.isDisabled				= false;
		self.isTrackingMouse		= false;
		self.isUploading			= false;
		self.trackTop				= 0;
		self.trackBottom			= 0;
		self.trackLeft				= 0;
		self.trackRight				= 0;
		self.buttonDivXMouseOffset	= 0;
		self.buttonDivYMouseOffset	= 0;

		// Create posting URL parameters
		if( self.urlParameters.Type != null )
			// Use the 'Type' from 'urlParameters'
			self.serverSideRequestHandlerType = self.urlParameters.Type;
		else
			// Use the 'Type' from 'serverSideRequestHandlerType'
			self.urlParameters.Type = self.serverSideRequestHandlerType;
		self.urlParameters.ConnectionID = null;  // NB: Will be set by the message handler's 'onConnectionIdReceived' event
		self.urlParameters.ResponseHandler = self.clientSideMsgHandlerType;
		self.urlParameters.FileID = self.serverSideCustomObjectName;

		self.events = createEventHandler();


		///////// Methods


		self.validateSelectedFile = function(fileName:string)
		{
			// Default implemention: NOOP
			return true;
		};

		self.sendMessage = function(messageType, messageDictionary)
		{
			if( messageDictionary == null )
				messageDictionary = <UploaderMessage>{};

			messageDictionary.type = self.serverSideMsgHandlerType;
			messageDictionary.FileID = self.serverSideCustomObjectName;
			messageDictionary.FileUploadMessageType = messageType;
			self.messageHandler.sendMessage( messageDictionary );
		}

		self.abort = function()
		{
			self.sendMessage( UploaderMessageTypes.Abort );
		}

		self.enable = function()
		{
			self.isDisabled = false;
		}

		self.disable = function()
		{
			self.isDisabled = true;
		}

		self.documentMouseMoved = function( ui:any )
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
				return;
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
				self.triggerMouseOut();
				return;
			}

			// Move the $uploadControl so that it is under the cursor
			self.$buttonDiv	.css( 'top', ''+(ui.pageY-self.buttonDivYMouseOffset)+'px' )
							.css( 'left', ''+(ui.pageX-self.buttonDivXMouseOffset)+'px' );
		}

		self.fileSelected = function(fileName)
		{
			// Extract file name from full path (NB: full path not always provided depending on the browser)
			var startIndex = (fileName.indexOf('\\') >= 0 ? fileName.lastIndexOf('\\') : fileName.lastIndexOf('/'));
			fileName = fileName.substring(startIndex);
			if (fileName.indexOf('\\') === 0 || fileName.indexOf('/') === 0)
				fileName = fileName.substring(1);

			var isValid = self.validateSelectedFile( fileName );
			if( isValid != true )
				// Discard
				return;

			self.triggerFileChanged( fileName );

			// Set the form's posting URL
			var formAction = self.messageHandler.getSyncedHandlerUrl() + '?' + $.param( self.urlParameters );
			self.$form.attr( 'action', formAction );

			// Send file
			(<HTMLFormElement>self.$form[0]).submit();
		};

		self.resetIFrame = function()
		{
			if( self.$iframe != null )
			{
				// Remove the old iframe
				try { self.$iframe.remove(); }
				catch(err) { self.triggerInternalError( '"self.$iframe.remove()" threw an error: ' + err ); }
			}

			// Create the iframe that will receive the file input's postback
			self.$iframe = $('<iframe name="' + self.iframeID + '"/>')	//.attr( 'name', self.iframeID )   <= IE7 cannot set name="" to iframes => must be done literaly when creating the tag !!!
											.attr( 'src', 'about:blank' )
											.css( 'top', '-100px' )
											.css( 'left', '-100px' )
											.css( 'width', '10px' )
											.css( 'height', '10px' )
											.css( 'position', 'absolute' )
											.load( function() { self.triggerIframeLoaded(); } );
			$('body').append( self.$iframe );

			if( self.$form == null )
			{
				// Create the div that will contain the upload button
				self.$fileInput = $('<input/>')	.attr( 'type', 'file' )  // <input type="file" name="testFile" onchange="this.form.submit();" />
												.attr( 'name', self.fileInputName )
												.attr( 'id', self.fileInputName )
												.css( 'width', '100px' )
												.change( function(e:any,p:any){ self.fileSelected(e.currentTarget.value); } );
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
				$(document)	.mousemove( self.documentMouseMoved );

				self.$uploadControl	.mousemove( function(ui:JQueryMouseEventObject)
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

													self.documentMouseMoved( ui );

													self.triggerMouseIn();
												} );
			}

			self.triggerIframeCreated();
		}


		///////// Events


		self.onInternalError = function(callback)
		{
			self.events.bind( 'internalError', function(evt:any,message:string)
				{
					try { callback( message ); }
					catch(err) { console.error( "Error while invoking 'internalError' event: "+err ); }
				} );
			return self;
		};
		self.triggerInternalError = function(message)
		{
			self.events.trigger( 'internalError', message );
		};

		self.onMouseIn = function(callback)
		{
			self.events.bind( 'mouseIn', function(evt:any,p:any)
				{
					try { callback(); }
					catch(err) { self.triggerInternalError( "Error while invoking 'onMouseIn' event: "+err ); }
				} );
			return self;
		};
		self.triggerMouseIn = function()
		{
			self.events.trigger( 'mouseIn', status );
		};

		self.onMouseOut = function(callback)
		{
			self.events.bind( 'mouseOut', function(evt:any,p:any)
				{
					try { callback(); }
					catch(err) { self.triggerInternalError( "Error while invoking 'onMouseOut' event: "+err ); }
				} );
			return self;
		};
		self.triggerMouseOut = function()
		{
			self.events.trigger( 'mouseOut', status );
		};

		self.onIframeCreated = function(callback)
		{
			self.events.bind( 'iframeCreated', function(evt:any,p:any)
				{
					try { callback(); }
					catch(err) { self.triggerInternalError( "Error while invoking 'onIframeCreated' event: "+err ); }
				} );
			return self;
		};
		self.triggerIframeCreated = function()
		{
			self.events.trigger( 'iframeCreated', status );
		};

		self.onIframeLoaded = function(callback)
		{
			self.events.bind( 'iframeLoaded', function(evt:any,p:any)
				{
					try { callback(); }
					catch(err) { self.triggerInternalError( "Error while invoking 'iframeLoaded' event: "+err ); }
				} );
			return self;
		};
		self.triggerIframeLoaded = function()
		{
			self.events.trigger( 'iframeLoaded', status );
		};

		self.onFileChanged = function(callback)
		{
			self.events.bind( 'fileChanged', function(evt:any,fileName:string)
				{
					try { callback(fileName); }
					catch(err) { self.triggerInternalError( "Error while invoking 'fileChanged' event: "+err ); }
				} );
			return self;
		};
		self.triggerFileChanged = function(fileName)
		{
			self.events.trigger( 'fileChanged', fileName );
		};

		self.onServerMessageReceived = function(callback)
		{
			self.events.bind( 'serverMessageReceived', function(evt:any,message:UploaderMessage)
				{
					try { callback(message); }
					catch(err) { self.triggerInternalError( "Error while invoking 'serverMessageReceived' event: "+err ); }
				} );
			return self;
		};
		self.triggerServerMessageReceived = function(message)
		{
			self.events.trigger( 'serverMessageReceived', message );
		};


		///////////////


		// Wait for the MessageHandler to receive a ConnectionID from the server to initialize this Uploader (the posting URL won't be valid without it)
		self.messageHandler.onConnectionIdReceived( function(connectionID:string)
			{
				self.urlParameters['ConnectionID'] = connectionID;
				self.resetIFrame();
			} );

		// Wait for a file selection (must be the first to bind to this event)
		self.onFileChanged( function(fileName)
			{
				// Upload started
				self.isUploading = true;

				// Force move the $fileInput away from above the $uploadControl
				self.isTrackingMouse = true;
				self.documentMouseMoved( /*ui*/null );
			} );

		// Attach to the MessageHandler to receive messages destined to this object
		self.messageHandler.bind( self.clientSideMsgHandlerType, function(e:any,message:UploaderMessage)
			{
				var messageType = message.FileUploadMessageType;
				if( messageType == null )
				{
					self.triggerInternalError( "Received a message that does not contain parameter 'FileUploadMessageType'" );
					return;
				}

				var customObjectName = message.FileID;
				if( customObjectName != null )
				{
					// The server sent its CustomObjectName so we can send messages back to it
					self.serverSideCustomObjectName = customObjectName;
					self.urlParameters.FileID = customObjectName;
				}

				if( (messageType == UploaderMessageTypes.Start) || (messageType == UploaderMessageTypes.Progress) )
				{
					if(! self.isUploading )
					{
						// Firefox sometimes re-send the file 1X if the upload fails => without triggering the 'change' event on the upload control => Witout calling 'self.fileSelected()'
						self.triggerFileChanged( message.FileName );  // Simulate the user's file selection
					}
				}
				else if( messageType == UploaderMessageTypes.Finish )
				{
					self.isUploading = false;
					var exceptionMessage = message.Exception;
					if( exceptionMessage != null )
						self.triggerInternalError( exceptionMessage );
				}

				// Redirect to events triggered by 'self'
				self.triggerServerMessageReceived( message );
			} );
	}  // constructor
}

/** Copied from: https://basarat.gitbooks.io/typescript/docs/types/literal-types.html */
function strEnum<T extends string>(o: Array<T>): {[K in T]: K}
{
	return o.reduce((res, key) => {
		res[key] = key;
		return res;
	}, Object.create(null));
}

export default FileUploader;
