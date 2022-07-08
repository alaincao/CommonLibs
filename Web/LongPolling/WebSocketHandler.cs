using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.SessionState;
using System.Web.WebSockets;

namespace CommonLibs.Web.LongPolling
{
	public abstract class WebSocketHandler : IHttpHandler, IReadOnlySessionState
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		[Serializable]
		internal class CloseConnectionException : CommonException
		{
			internal bool	IsError		{ get; private set; }
			internal CloseConnectionException()					: base()		{ IsError = false; }
			internal CloseConnectionException(string message)	: base(message)	{ IsError = true; }
			protected CloseConnectionException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
				=> throw new NotImplementedException( $"Serialization of '{nameof(CloseConnectionException)}' is not implemented" );
		}

		public abstract MessageHandler		MessageHandler		{ get; }
		public abstract ConnectionList		ConnectionList		{ get; }

		public static int					DefaultBufferSize	{ get; set; } = 1024;
		public static int					InitTimeoutSeconds	{ get; set; } = 5;

		#region For IHttpHandler
		public bool							IsReusable			{ get { return true; } }
		#endregion

		public void ProcessRequest(HttpContext context)
		{
			object messageContext = null;
			if( MessageHandler.SaveMessageContextObject != null )
				messageContext = MessageHandler.SaveMessageContextObject();

			if( context.IsWebSocketRequest )
			{
				context.AcceptWebSocketRequest( (socketContext)=>{ return OnAcceptWebSocketRequest(socketContext, messageContext); } );
			}
			else
			{
				context.Response.Write( "Only websockets here ..." );
				context.Response.End();
			}
		}

		private async Task OnAcceptWebSocketRequest(AspNetWebSocketContext socketContext, object messageContext)
		{
			var conn = new WebSocketConnection( this, socketContext, messageContext );
			System.Exception exception = null;
			try
			{
				await conn.ReceiveInitMessage();
				await conn.MainLoop();
			}
			catch( CloseConnectionException ex )
			{
				if( ex.IsError )
					exception = ex;
			}
			catch( System.Exception ex )
			{
				exception = ex;
			}

			if( conn.Registered )
				ConnectionList.UnregisterConnection( conn );

			// Close socket
			if( conn.Socket.State == System.Net.WebSockets.WebSocketState.Open ) try
			{
				if( exception != null )
				{
					var exceptionMessage = RootMessage.CreateServer_MessagesList( new Message[]{ Message.CreateExceptionMessage(exception:exception) } );
					conn.Sending = true;
					await conn.SendJSon( exceptionMessage );
				}

				await conn.Socket.CloseOutputAsync( System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, string.Empty, System.Threading.CancellationToken.None );
			}
			catch( System.Exception ex )
			{
				FAIL( "Could not gracefully close the websocket: " + ex.Message );
			}
		}
	}
}
