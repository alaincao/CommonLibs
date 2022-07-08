//
// CommonLibs/Web/LongPolling/Utils/GenericPageFile.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CommonLibs.Web.LongPolling.Utils
{
	public class GenericPageFile : PageFile
	{
		public const string							OutMsgParmFileID				= PageFile.RqstFileID;

		public const string							HandlerType						= "GenericPageFile-de97320d";
		public string								CustomObjectName				{ get=>GetCustomObjectName(); set=>customObjectName = value; }
		private string								customObjectName				= null;

		public const string							RqstUploadReceiverAssembly		= "UploadReceiverAssembly";
		public const string							RqstUploadReceiverType			= "UploadReceiverType";
		public const string							RqstUploadReceiverMethod		= "UploadReceiverMethod";

		public override int							UploadLengthForStreamParser		{ get { return 40*1024; } }

		public OnUploadStartedDelegate				OnUploadStarted					{ get; set; } = null;
		/// <param name="request">The HttpRequest</param>
		/// <param name="uploadFileName">The client's file name</param>
		/// <param name="filePath">Set to the path the file must be saved</param>
		public delegate void OnUploadStartedDelegate(HttpRequest request, string uploadFileName);

		/// <summary>Set this method to change the default way to create upload file path</summary>
		public Func<HttpRequest,string,string>		OnUploadGetFilePath				{ get; set; } = DefaultGetFilePath;

		public OnUploadTerminatedDelegate			OnUploadTerminated				{ get; set; } = null;
		/// <param name="request">The HttpRequest</param>
		/// <param name="uploadFileName">The file name as it has been uploaded</param>
		/// <param name="filePath">The file path</param>
		/// <param name="success">True if the upload was successfully completed. False if not</param>
		/// <param name="size">The uploaded file size</param>
		public delegate void OnUploadTerminatedDelegate(HttpRequest request, string uploadFileName, string filePath, bool success, long size);

		/// <summary>
		/// Parameter 1: The HttpRequest<br/>
		/// Parameter 2: Set to the name of the file to download<br/>
		/// Parameter 3: Set to the path of the file to download (required only if outputStream is set to null)<br/>
		/// Parameter 4: Set to the stream to download (required only if filePath is set to null)
		/// </summary>
		public DownloadRequestedDeleagate DownloadRequested { get; set; } = null;
		public delegate void DownloadRequestedDeleagate(HttpRequest request, out string fileName, out string filePath, out System.IO.Stream outputStream);

		public GenericPageFile(MessageHandler messageHandler, string connectionID) : base(messageHandler, connectionID)
		{
			OnSendingNewFileMessage += GenericPageFile_OnSendingNewFileMessage;
		}

		/// <remarks>Will assign a default GUID if not previously set</remarks>
		private string GetCustomObjectName()
		{
			if( customObjectName != null )
				return customObjectName;
			customObjectName = Guid.NewGuid().ToString();
			return customObjectName;
		}

		public static Dictionary<string,string> GetQueryParameters(Type receiverType, string methodName="OnPageFileCreated", string connectionID=null)
		{
			CommonLibs.Utils.Debug.ASSERT( receiverType != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'receiverType'" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(methodName), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'methodName'" );

			#if DEBUG
				var methodParameters = new Type[] {	typeof(LongPolling.ConnectionList),
													typeof(string),
													typeof(GenericPageFile),
													typeof(HttpRequest) };
				var method = receiverType.GetMethod( methodName, System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static, null, methodParameters, null );
				if( method == null )
					CommonLibs.Utils.Debug.ASSERT( false, System.Reflection.MethodInfo.GetCurrentMethod(), "The provided 'receiverType' does not have or does not correctly implement method '" + methodName + "'" );
			#endif

			var parms = new Dictionary<string,string>();
			var assemblyName = receiverType.Assembly.ManifestModule.Name;
			parms[ SyncedHttpHandler.RequestParmType ] = HandlerType;
			if( connectionID != null )
				parms[ SyncedHttpHandler.RequestParmConnectionID ] = connectionID;
			parms[ RqstUploadReceiverAssembly ] = assemblyName;
			parms[ RqstUploadReceiverType ] = receiverType.FullName;
			parms[ RqstUploadReceiverMethod ] = methodName;
			return parms;
		}

		public static SyncedHttpHandler.ISyncedRequestHandler GenericSyncedHandler(LongPolling.ConnectionList connectionList, HttpRequest request, string connectionID, Func<GenericPageFile> createCallback)
		{
			var customObjectName = request.QueryString[ PageFile.RqstFileID ];
			if( customObjectName == "" )
				customObjectName = null;
			GenericPageFile pageFileInstance;
			if( customObjectName != null )
			{
				// Try get the PageFile instance using the CustomObjectName
				pageFileInstance = (GenericPageFile)connectionList.GetConnectionCustomObject( connectionID, customObjectName );
				if( pageFileInstance != null )
					// Found: Return it
					return pageFileInstance;
			}
			// Not found => Create a new instance
			pageFileInstance = createCallback();

			if( customObjectName != null )
			{
				// Register it in the ConnectionList
				pageFileInstance.CustomObjectName = customObjectName;
				var obj = pageFileInstance.MessageHandler.ConnectionList.GetConnectionCustomObject( connectionID, pageFileInstance.CustomObjectName, ()=>pageFileInstance );
				CommonLibs.Utils.Debug.ASSERT( obj == pageFileInstance, System.Reflection.MethodInfo.GetCurrentMethod(), "The registration of this 'GenericPageFile' did not return this instance (another one already exists)" );
			}

			// And invoke the callback on the receiver class to give it the new instance
			var assemblyName = request.QueryString[ RqstUploadReceiverAssembly ];
			if( string.IsNullOrEmpty(assemblyName) )
				throw new ArgumentException( "GenericFileUpload failed: query parameter '" + RqstUploadReceiverAssembly + "' is missing" );
			var typeName = request.QueryString[ RqstUploadReceiverType ];
			if( string.IsNullOrEmpty(typeName) )
				throw new ArgumentException( "GenericFileUpload failed: query parameter '" + RqstUploadReceiverType + "' is missing" );
			var methodName = request.QueryString[ RqstUploadReceiverMethod ];
			if( string.IsNullOrEmpty(methodName) )
				throw new ArgumentException( "GenericFileUpload failed: query parameter '" + RqstUploadReceiverMethod + "' is missing" );

			var assembly = AppDomain.CurrentDomain.GetAssemblies().Single( v=>v.ManifestModule.Name == assemblyName );
			var type = assembly.GetType( typeName );
			var methodParameters = new Type[] {	typeof(LongPolling.ConnectionList),
												typeof(string),
												typeof(GenericPageFile),
												typeof(HttpRequest) };
			var method = type.GetMethod( methodName, System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static, null, methodParameters, null );
			if( method == null )
				throw new ArgumentException( "GenericFileUpload failed: Could not find method '" + methodName + "()' in type '" + assemblyName+"."+typeName + "'" );
			method.Invoke( type, new object[]{connectionList, connectionID, pageFileInstance, request} );

			return pageFileInstance;
		}

		private void GenericPageFile_OnSendingNewFileMessage(Message message)
		{
			// Add the CustomObjectName of this instance so the client can send messages back
			message[ OutMsgParmFileID ] = CustomObjectName;
		}

		protected override System.IO.Stream GetUploadStream(HttpRequest request, string uploadFileName, out string fileName, out Action<bool, long> onUploadTerminated)
		{
			CommonLibs.Utils.Debug.ASSERT( OnUploadGetFilePath != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Property 'OnUploadGetFilePath' is supposed to be set here" );

			fileName = uploadFileName;
			if( OnUploadStarted != null )
				OnUploadStarted( request, uploadFileName );

			string filePath = OnUploadGetFilePath( request, uploadFileName );

			var fName = fileName;
			onUploadTerminated = (success,size)=>
				{
					if( OnUploadTerminated != null )
						OnUploadTerminated( request, fName, filePath, success, size );
				};

			return System.IO.File.Create( filePath );
		}

		private static string DefaultGetFilePath(HttpRequest request, string uploadedFileName)
		{
			// NB: 'uploadedFileName' parameter not used
			return System.IO.Path.GetTempPath() + Guid.NewGuid().ToString();
		}

		protected override System.IO.Stream GetDownloadStream(HttpRequest request, out string fileName)
		{
			if( DownloadRequested == null )
				throw new NotImplementedException( "The 'DownloadRequested' callback has not been set" );
			string filePath;
			System.IO.Stream outputStream;
			DownloadRequested( request, out fileName, out filePath, out outputStream );
			if( outputStream != null )
				return outputStream;
			else
				return System.IO.File.Open( filePath, System.IO.FileMode.Open );
		}
	}
}
