using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CommonLibs.Web.LongPolling.Utils
{
	public class GenericPageFile : PageFile
	{
		public const string							OutMsgParmFileID				= PageFile.RqstFileID;

		public const string							HandlerType						= "de97320d-76b6-4bf1-667d-505a8f520c43";
		public string								CustomObjectName				{ get { return customObjectName.ToString(); } }
		public Guid									customObjectName				= Guid.NewGuid();

		public const string							RqstUploadReceiverAssembly		= "UploadReceiverAssembly";
		public const string							RqstUploadReceivertype			= "UploadReceiverType";
		private const string						UploadReceiverMethodName		= "GetInstance";

		public override int							UploadLengthForStreamParser		{ get { return 40*1024; } }

		public OnUploadStartedDelegate				OnUploadStarted					= null;
		/// <param name="request">The HttpRequest</param>
		/// <param name="uploadFileName">The client's file name</param>
		/// <param name="filePath">Set to the path the file must be saved</param>
		public delegate void OnUploadStartedDelegate(HttpRequest request, string uploadFileName, out string filePath);

		public OnUploadTerminatedDelegate			OnUploadTerminated				= null;
		/// <param name="request">The HttpRequest</param>
		/// <param name="uploadFileName">The file name as it has been uploaded</param>
		/// <param name="filePath">The file path</param>
		/// <param name="succes">True if the upload was successfully completed. False if not</param>
		/// <param name="size">The uploaded file size</param>
		public delegate void OnUploadTerminatedDelegate(HttpRequest request, string uploadFileName, string filePath, bool succes, long size);

		/// <summary>
		/// Parameter 1: The HttpRequest<br/>
		/// Returns: The file path to download
		/// </summary>
		public DownloadRequestedDeleagate DownloadRequested = null;
		public delegate void DownloadRequestedDeleagate(HttpRequest request, out string fileName, out string filePath);

		public GenericPageFile(MessageHandler messageHandler, string connectionID) : base(messageHandler, connectionID)
		{
			var obj = MessageHandler.ConnectionList.GetConnectionCustomObject( connectionID, CustomObjectName, ()=>this );
			ASSERT( obj == this, "The registration of this 'GenericPageFile' did not return this instance (another one already exists)" );

			OnSendingNewFileMessage += GenericPageFile_OnSendingNewFileMessage;
		}

		public static Dictionary<string,string> CreateUploaderQueryParameters(Type receiverType)
		{
			CommonLibs.Utils.Debug.ASSERT( receiverType.GetInterface(typeof(IFilePageReceiver).Name) != null, System.Reflection.MethodInfo.GetCurrentMethod(), "The provided 'receiverType' does not implement interface '" + typeof(IFilePageReceiver).Name + "'" );
			CommonLibs.Utils.Debug.ASSERT( receiverType.GetMethod(UploadReceiverMethodName, System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static, null, new Type[] {	typeof(LongPolling.ConnectionList), typeof(HttpRequest), typeof(string) }, null) != null, System.Reflection.MethodInfo.GetCurrentMethod(), "The provided 'receiverType' does not have a static method '" + UploadReceiverMethodName + "'" );

			var assemblyName = receiverType.Assembly.ManifestModule.Name;
			var parms = new Dictionary<string,string>();
			parms[ SyncedHttpHandler.RequestParmType ] = HandlerType;
			parms[ RqstUploadReceiverAssembly ] = assemblyName;
			parms[ RqstUploadReceivertype ] = receiverType.FullName;
			return parms;
		}

		public static SyncedHttpHandler.ISyncedRequestHandler GenericSyncedHandler(LongPolling.ConnectionList connectionList, HttpRequest request, string connectionID, Func<PageFile> createCallback)
		{
			var customObjectName = request.QueryString[ PageFile.RqstFileID ];
			if( !string.IsNullOrEmpty(customObjectName) )
			{
				// Try get instance using the CustomObjectName
				var instance = (GenericPageFile)connectionList.GetConnectionCustomObject( connectionID, customObjectName );
				if( instance != null )
					return instance;
			}
			// None found => Create a new instance

			var assemblyName = request.QueryString[ RqstUploadReceiverAssembly ];
			if( string.IsNullOrEmpty(assemblyName) )
				throw new ArgumentException( "GenericFileUpload failed: query parameter '" + RqstUploadReceiverAssembly + "' is missing" );
			var typeName = request.QueryString[ RqstUploadReceivertype ];
			if( string.IsNullOrEmpty(typeName) )
				throw new ArgumentException( "GenericFileUpload failed: query parameter '" + RqstUploadReceivertype + "' is missing" );

			var assembly = AppDomain.CurrentDomain.GetAssemblies().Where( v=>v.ManifestModule.Name == assemblyName ).Single();
			var type = assembly.GetType( typeName );
			var methodParameters = new Type[] {	typeof(LongPolling.ConnectionList),
												typeof(HttpRequest),
												typeof(string) };
			var method = type.GetMethod( UploadReceiverMethodName, System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static, null, methodParameters, null );
			if( method == null )
				throw new ArgumentException( "GenericFileUpload failed: Could not find method '" + UploadReceiverMethodName + "()' in type '" + assemblyName+"."+typeName + "'" );
			var uploadReceiver = (IFilePageReceiver)method.Invoke( type, new object[]{connectionList, request, connectionID} );
			if( uploadReceiver == null )
				throw new ArgumentException( "GenericFileUpload failed: Method '" + UploadReceiverMethodName + "()' did not returned the 'IFilePageReceiver' instance" );

			var uploader = createCallback();
			uploadReceiver.FileUploadCreated( uploader );
			return uploader;
		}

		private void GenericPageFile_OnSendingNewFileMessage(Message message)
		{
			// Add the CustomObjectName of this instance so the client can send messages back
			message[ OutMsgParmFileID ] = CustomObjectName;
		}

		protected override System.IO.Stream GetUploadStream(HttpRequest request, string uploadFileName, out string fileName, out Action<bool, long> onUploadTerminated)
		{
			fileName = uploadFileName;
			string filePath;
			if( OnUploadStarted == null )
			{
				filePath = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString();
			}
			else
			{
				OnUploadStarted( request, uploadFileName, out filePath );
			}

			var fName = fileName;
			onUploadTerminated = (success,size)=>
				{
					if( OnUploadTerminated != null )
						OnUploadTerminated( request, fName, filePath, success, size );
				};

			return System.IO.File.Create( filePath );
		}

		protected override System.IO.Stream GetDownloadStream(HttpRequest request, out string fileName)
		{
			if( DownloadRequested == null )
				throw new NotImplementedException( "The 'DownloadRequested' callback has not been set" );
			string filePath;
			DownloadRequested( request, out fileName, out filePath );
			var stream = System.IO.File.Open( filePath, System.IO.FileMode.Open );
			return stream;
		}

		public static string CreateDownloadUrl(string syncedHttpHandlerUrl, Type receiverType, string connectionID, IDictionary<string,string> parameters=null)
		{
			CommonLibs.Utils.Debug.ASSERT( receiverType != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'receiverType'" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(connectionID), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'connectionID'" );

			Dictionary<string,string> parms;
			if( parameters != null )
				parms = new Dictionary<string,string>( parameters );
			else
				parms = new Dictionary<string,string>();
			parms[ CommonLibs.Web.LongPolling.SyncedHttpHandler.RequestParmConnectionID ] = connectionID;
			parms[ CommonLibs.Web.LongPolling.SyncedHttpHandler.RequestParmType ] = HandlerType;
			foreach( var pair in CreateUploaderQueryParameters(receiverType) )
				parms[ pair.Key ] = pair.Value;
			var url = System.Web.VirtualPathUtility.ToAbsolute( syncedHttpHandlerUrl )
					+ "?" + string.Join( "&", parms.Select( v=>HttpUtility.UrlEncode(v.Key) + "=" + HttpUtility.UrlEncode(v.Value) ).ToArray() );
			return url;
		}
	}
}
