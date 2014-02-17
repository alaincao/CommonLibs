//
// CommonLibs/Web/LongPolling/Utils/PageFile.cs
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

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace CommonLibs.Web.LongPolling.Utils
{
	public abstract class PageFile : CommonLibs.Web.LongPolling.SyncedHttpHandler.ISyncedRequestHandler, IDisposable
	{
		[System.Diagnostics.Conditional("DEBUG")] protected void LOG(string message)				{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected void FAIL(string message)				{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		public class OperationAbortedException : ApplicationException
		{
			public OperationAbortedException(string message) : base(message)  {}
		}

		public class MaximumUploadSizeException : ApplicationException
		{
			public MaximumUploadSizeException(string message) : base(message)  {}
		}

		// Parameters for messages sent to peer :

		/// <summary>The handler name that is used to register against the MessageHandler</summary>
		/// <remarks>Can be changed, but only once and at application start</remarks>
		public static string						InMsgHandlerType			= "FileUpload";
		public const string							InMsgTypeKey				= "FileUploadMessageType";
		public const string							InMsgTypeAbort				= "Abort";
		public const string							InMsgResponseHandler		= RqstResponseHandler;

		// Parameters for messages coming from the peer :

		public string								OutMsgHandlerType			= null;
		public const string							OutMsgTypeKey				= "FileUploadMessageType";
		public const string							OutMsgTypeStart				= "Start";
		public const string							OutMsgTypeProgress			= "Progress";
		public const string							OutMsgTypeFinish			= "Finish";
		public const string							OutMsgParmFileName			= "FileName";
		public const string							OutMsgParmFileSize			= "Total";
		public const string							OutMsgParmCurrent			= "Current";
		public const string							OutMsgParmTotal				= "Total";
		public const string							OutMsgParmSuccess			= "Success";
		public const string							OutMsgParmException			= "Exception";

		/// <summary>The name of the HTTP parameter for the optional string that identifies this file in the page</summary>
		public const string							RqstFileID					= "FileID";
		/// <summary>The name of the HTTP parameter that names the handler type that must be used to send message to the peer</summary>
		public const string							RqstResponseHandler			= "ResponseHandler";

		public MessageHandler						MessageHandler				{ get; private set; }
		public TimeSpan								NotificationInterval		= new TimeSpan( hours:0, minutes:0, seconds:1 );

		protected object							Locker						= new object();
		public string								ConnectionID				{ get; private set; }
		public bool									IsDoingSomething			{ get { return (CurrentOperationObject != null); } }
		private volatile object						CurrentOperationObject		= null;
		public bool									Disposed					{ get { return disposed; } private set { disposed = value; } }
		private volatile bool						disposed					= false;
		public long?								MaximumUploadSize			= null;
		private List<string>						FilesToDeleteOnDispose		= new List<string>();
		/// <summary>When upload ContentLenght > 10M, use the 'HtmlPostedMultipartStreamParser' to upload the file</summary>
		public abstract int							UploadLengthForStreamParser	{ get; }

		/// <param name="uploadFileName">The file name as it is uploaded</param>
		/// <param name="fileName">Must be set to the name that will be used by this object</param>
		/// <param name="onUploadTerminated">
		/// The callback to invoke when the upload is terminated. Can be null.<br/>
		/// Parameter 1: True if the upload was successfully completed. False if not<br/>
		/// Parameter 2: The uploaded file size 
		/// </param>
		protected abstract Stream GetUploadStream(HttpRequest request, string uploadFileName, out string fileName, out Action<bool,long> onUploadTerminated);
		protected abstract Stream GetDownloadStream(HttpRequest request, out string fileName);

		public event Action<Message> OnSendingNewFileMessage;

		public Action<Exception,Message> OnUploadException = null;

		public PageFile(MessageHandler messageHandler, string connectionID)
		{
			MessageHandler = messageHandler;
			ConnectionID = connectionID;
		}

		public void Dispose()
		{
			lock( Locker )
			{
				// Abort any current operation on this file
				try { SetConcurrentOperationsObject( null ); }
				catch( System.Exception ex ) { FAIL( "'SetConcurrentOperationsObject()' threw exception (" + ex.GetType().FullName + "): " + ex.Message ); }

				if( Disposed )
					return;
				Disposed = true;

				try
				{
					// Delete any temporary files
					if( FilesToDeleteOnDispose != null )
					{
						foreach( var filePath in FilesToDeleteOnDispose )
						{
							try { if( File.Exists(filePath) )  File.Delete(filePath); }
							catch( System.Exception ex ) { FAIL( "'File.Delete('" + filePath + "')' threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
						}
					}
					FilesToDeleteOnDispose = null;
				}
				catch( System.Exception ex )
				{
					FAIL( "Foreach FilesToDeleteOnDispose threw an exception (" + ex.GetType().FullName + "): " + ex.Message );
				}
			}

			try { OnDispose(); }
			catch( System.Exception ex ) { FAIL( "'File.OnClose()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
		}

		protected virtual void OnCustomMessageReceived(Message message, string messageType)
		{
			return;  // Virtual => NOOP
		}

		protected virtual void OnDispose()
		{
			return;  // Virtual => NOOP
		}

		/// <param name="persistantObjectName">Will be used as CustomObject name to get the object instance</param>
		public static PageFile GetInstance(ConnectionList connectionList, string connectionID, string persistantObjectName=null, HttpRequest request=null, Func<PageFile> createCallback=null)
		{
			if( persistantObjectName == null )
			{
				if( request == null )
					throw new ArgumentException( "Either 'fileID' parameter or 'request' must be specified" );
				persistantObjectName = request.QueryString[ RqstFileID ];
			}
			if( string.IsNullOrEmpty(persistantObjectName) )
				throw new ArgumentException( "The connection's custom object name could not be determined. Either the 'fileID' parameter must be set or the 'request' must contain the HTTP parameter '" + RqstFileID + "'" );

			PageFile instance;
			if( createCallback == null )
			{
				instance = (PageFile)connectionList.GetConnectionCustomObject( connectionID, persistantObjectName );
			}
			else
			{
 				Func<object> createPageFileCallback = ()=>{ return createCallback(); };
				instance = (PageFile)connectionList.GetConnectionCustomObject( connectionID, persistantObjectName, createPageFileCallback );
			}
			return instance;
		}

		public void AddFileToDeleteOnDispose(string filePath)
		{
			lock( Locker )
			{
				FilesToDeleteOnDispose.Add( filePath );
			}
		}

		public bool RemoveFileToDeleteOnDispose(string filePath)
		{
			lock( Locker )
			{
				return FilesToDeleteOnDispose.Remove( filePath );
			}
		}

		protected void SetConcurrentOperationsObject(object operationObject)
		{
			lock( Locker )
			{
				CurrentOperationObject = operationObject;  // This line will make the CheckConcurrentOperations() method to throw an exception on still running upload's threads
			}
		}

		public bool CheckConcurrentOperations(object operationObject, bool throwException=true)
		{
			lock( Locker )
			{
				if( CurrentOperationObject == operationObject )
				{
					return true;
				}
				else
				{
					// The 'CurrentOperationObject' does not match the 'operationObject' we are working from
					if( throwException )
						// This thread is processing an obsolete operation and so must be stopped
						throw new OperationAbortedException( "Operation aborted, another concurrent operation is running" );
					else
						return false;
				}
			}
		}

		/// <summary>
		/// Download the current file
		/// </summary>
		public virtual void Get(SyncedHttpHandler handler, string connectionID, HttpContext context)
		{
			var response = context.Response;
			response.Clear();

			Stream stream;
			string fileName;
			lock( Locker )
			{
				stream = GetDownloadStream( context.Request, out fileName );
			}

			using( stream )
			{
				response.AddHeader( "Content-Disposition", "attachment; filename=" + fileName );
				response.ContentType = "application/octet-stream";

				var bufferSize = 1024 * 1024;
				var buffer = new byte[ bufferSize ];
				for( var n=stream.Read(buffer, 0, bufferSize); n>0; n=stream.Read(buffer, 0, bufferSize) )
				{
					response.OutputStream.Write( buffer, 0, n );
				}
				response.End();
			}
		}

		/// <summary>
		/// Upload a new file, replacing the current one if any
		/// </summary>
		public virtual void Post(SyncedHttpHandler handler, string connectionID, HttpContext context)
		{
			bool uploadSuccessful = false;
			string fileName = null;
			long fileSize = 0;
			Stream outStream = null;
			Action<bool,long> onUploadTerminatedCallback = null;
			Exception terminationException = null;
			try
			{
				lock( Locker )
				{
					// Stop any concurrent operations
					SetConcurrentOperationsObject( context );
				}

				// If the peer response message handler is included in the HTTP request string, save it
				var peerMessageHandler = context.Request.QueryString[ RqstResponseHandler ];
				if(! string.IsNullOrEmpty(peerMessageHandler) )
					OutMsgHandlerType = peerMessageHandler;

				var contentLength = context.Request.ContentLength;

				if( (contentLength >= 0) // NB: when the file size > ~2Gb, the browsers bug and send negative ContentLenghts
				 && (contentLength <= UploadLengthForStreamParser) )
				{
					// Directly use the Request.Files to perform the upload

					if( context.Request.Files.Count != 1 )
						throw new ArgumentException( "Expected '1' uploaded file, got '" + context.Request.Files.Count + "'" );
					var postedFile = context.Request.Files[ 0 ];
					fileSize = postedFile.ContentLength;
					var uploadFileName = postedFile.FileName;
					if( uploadFileName.Contains(System.IO.Path.DirectorySeparatorChar) )
						// IE sends the full path instead of the file name...
						uploadFileName = System.IO.Path.GetFileName( uploadFileName );
					SendNewFileMessage( uploadFileName );
					outStream = GetUploadStream( context.Request, uploadFileName, out fileName, out onUploadTerminatedCallback );
					ASSERT( !string.IsNullOrEmpty(fileName), "'GetUploadStream()' did not set the 'fileName'" );

					if( (MaximumUploadSize != null) && (fileSize > MaximumUploadSize.Value) )
						throw new MaximumUploadSizeException( "This uploaded file is too big ; The maximum upload size has been set to '" + MaximumUploadSize + "'" );

					var bufferSize = 1024 * 8;
					var buffer = new byte[ bufferSize ];
					for( int n=postedFile.InputStream.Read(buffer, 0,bufferSize); n>0; n=postedFile.InputStream.Read(buffer, 0,bufferSize) )
					{
						CheckConcurrentOperations( context );
						outStream.Write( buffer, 0, n );
					}
					outStream.Close();
					outStream.Dispose();
					outStream = null;
					uploadSuccessful = true;
				}
				else  // contentLength <= UploadLengthForStreamParser
				{
					// Use the HtmlPostedMultipartStreamParser to perform the upload

					var nextProgressMessage = DateTime.Now + NotificationInterval;
					var streamParser = new HtmlPostedMultipartStreamParser( context.Request );
					streamParser.OnNewFile += (uploadFileName)=>
						{
							CheckConcurrentOperations( context );
							outStream = GetUploadStream( context.Request, uploadFileName, out fileName, out onUploadTerminatedCallback );
							ASSERT( !string.IsNullOrEmpty(fileName), "'GetUploadStream()' did not set the 'fileName'" );
							SendNewFileMessage( fileName );
						};
					streamParser.OnFileContentReceived += (buffer, n)=>
						{
							CheckConcurrentOperations( context );
							outStream.Write( buffer, 0, n );
							fileSize += n;

							if( (MaximumUploadSize != null) && (n > MaximumUploadSize.Value) )
								throw new MaximumUploadSizeException( "This uploaded file is too big ; The maximum upload size has been set to '" + MaximumUploadSize + "'" );

							var now = DateTime.Now;
							if( now >= nextProgressMessage )
							{
								SendUploadProgressMessage( fileName, streamParser.CurrentLength, streamParser.ContentLength );
								nextProgressMessage = now + NotificationInterval;
							}
						};
					streamParser.OnEndOfFile += ()=>
						{
							CheckConcurrentOperations( context );
							outStream.Close();
							outStream.Dispose();
							outStream = null;
						};
					streamParser.ProcessContext( context );
					if( outStream != null )
					{
						FAIL( "'streamParser.OnEndOfFile' has not been called" );
						return;
					}

					if( (MaximumUploadSize != null) && (fileSize > MaximumUploadSize.Value) )
						throw new MaximumUploadSizeException( "This uploaded file is too big ; The maximum upload size has been set to '" + MaximumUploadSize + "'" );

					uploadSuccessful = true;
				}
			}
			catch( OperationAbortedException )
			{
				// This is a regular abort => Leave 'uploadSuccessful == false' => NOOP
			}
			catch( System.Exception ex )
			{
				// Save exception to give to 'onUploadTerminatedCallback'
				terminationException = ex;
			}

			if(! uploadSuccessful )
			{
				if( outStream != null )
				{
					try { outStream.Dispose(); }
					catch( System.Exception ex ) { FAIL( "'outStream.Dispose()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
				}
			}

			lock( Locker )
			{
				if( CheckConcurrentOperations(context, throwException:false) )
				{
					// The CurrentOperationObject was assigned by this thread => We can unassing it
					SetConcurrentOperationsObject( null );
				}
				else
				{
					// The CurrentOperationObject was overriden by another thread => NOOP
				}
			}

			// Send termination message
			try { SendEndOfFileFileMessage( fileName, fileSize, uploadSuccessful, terminationException ); }
			catch( System.Exception ex ) { FAIL( "'SendEndOfFileFileMessage()' threw exception (" + ex.GetType().FullName + "): " + ex.Message ); }

			if( onUploadTerminatedCallback != null )
			{
				try { onUploadTerminatedCallback( uploadSuccessful, fileSize ); }
				catch( System.Exception ex ) { FAIL( "'OnUploadTerminated()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
			}
		}

		public Message CreateCustomMessage(string customMessageType)
		{
			ASSERT( !string.IsNullOrEmpty(OutMsgHandlerType), "Property 'OutMsgHandlerType' has not been set yet" );
			ASSERT( !string.IsNullOrEmpty(customMessageType), "Missing parameter 'customMessageType'" );

			var message = Message.CreateEmtpyMessage( OutMsgHandlerType );
			message.Add( OutMsgTypeKey, customMessageType );
			return message;
		}

		private void SendNewFileMessage(string fileName)
		{
			ASSERT( !string.IsNullOrEmpty(fileName), "Missing parameter 'fileName'" );

			var message = CreateCustomMessage( OutMsgTypeStart );
			message.Add( OutMsgParmFileName, fileName );

			if( OnSendingNewFileMessage != null )
				OnSendingNewFileMessage( message );

			MessageHandler.SendMessageToConnection( ConnectionID, message );
		}

		private void SendUploadProgressMessage(string fileName, long current, long total )
		{
			ASSERT( !string.IsNullOrEmpty(fileName), "Missing parameter 'fileName'" );
			ASSERT( current >= 0, "Invalid parameter 'current'" );
			ASSERT( (total > 0) || (total == -1), "Invalid parameter 'total'" );

			var message = CreateCustomMessage( OutMsgTypeProgress );
			message.Add( OutMsgParmFileName, fileName );
			message.Add( OutMsgParmCurrent, current );
			if( total != -1 )
				message.Add( OutMsgParmTotal, total );
			MessageHandler.SendMessageToConnection( ConnectionID, message );
		}

		private void SendEndOfFileFileMessage(string fileName, long fileSize, bool success, Exception exception)
		{
			ASSERT( !string.IsNullOrEmpty(fileName), "Missing parameter 'fileName'" );
			ASSERT( fileSize >= 0, "Invalid parameter 'fileSize'" );

			var message = CreateCustomMessage( OutMsgTypeFinish );
			message.Add( OutMsgParmFileName, fileName );
			message.Add( OutMsgParmFileSize, fileSize );
			message.Add( OutMsgParmSuccess, success );

			if( exception != null )
			{
				if( OnUploadException != null )
				{
					OnUploadException( exception, message );
				}
				else
				{
					var exceptionMessage = "Exception received (" + exception.GetType() + "): " + exception.Message + "\n"
										 + exception.StackTrace;
					message.Add( OutMsgParmException, exceptionMessage );
				}
			}

			MessageHandler.SendMessageToConnection( ConnectionID, message );
		}

		public static void ReceiveMessage(MessageHandler messageHandler, Message message)
		{
			var fileID = message.TryGetString( RqstFileID );
			if( fileID == "" )
				fileID = null;  // Optional

			var instance = GetInstance( messageHandler.ConnectionList, connectionID:message.SenderConnectionID, persistantObjectName:fileID );
			if( instance == null )
				throw new ArgumentException( "The PageFile instance has not been created for ConnectionID '" + message.SenderConnectionID + "' and FileID '" + (fileID ?? "<NULL>") + "'" );

			var messageType = message.TryGetString( InMsgTypeKey );
			if( string.IsNullOrEmpty(messageType) )
				throw new ArgumentException( "The message is missing parameter '" + InMsgTypeKey + "'" );
			switch( messageType )
			{
				case InMsgTypeAbort:
					instance.SetConcurrentOperationsObject( null );  // This will make 'CheckConcurrentOperations()' to fail
					break;
				default:
					instance.OnCustomMessageReceived( message, messageType );
					break;
			}
		}
	}
}
