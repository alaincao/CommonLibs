﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using CommonLibs.Web.LongPolling;
using CommonLibs.Web.LongPolling.Utils;

namespace CommonLibs.Web.LongPolling.Utils
{
// TODO: Alain: ConnectionPersistentObject/PersistanceTypes.Connection: Obsoleted by ConnectionList.TaskEntry.CustomObject
	[Obsolete("Use 'CommonLibs.Web.LongPolling.ConnectionList.GetConnectionCustomObject()' method instead")]
	public abstract class ConnectionPersistentObject : IDisposable
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		public enum PersistanceTypes
		{
			Undefined		= 0,
			Connection,
			Session
		}

		public PersistanceTypes						PersistanceType					{ get; private set; }
		public ConnectionList						ConnectionList					{ get; private set; }
		public string								SessionID						{ get; private set; }
		public string								ConnectionID					{ get; private set; }

		private Action<string>						ConnectionLostCallback			= null;
		private Action<string>						SessionClosedCallback			= null;

		public bool									IsClosed						{ get { return (Closed != 0); } }
		private int									Closed							= 0;

		protected abstract void OnClose();

		public ConnectionPersistentObject(ConnectionList connectionList, PersistanceTypes persistanceType, string connectionID, string sessionID=null)
		{
			ASSERT( connectionList != null, "Missing parameter 'connectionList'" );
			ASSERT(	( persistanceType == PersistanceTypes.Connection && (!string.IsNullOrEmpty(connectionID)) )
				||	( persistanceType == PersistanceTypes.Session && (!string.IsNullOrEmpty(sessionID)) ), "Missing either 'connectionID' or 'sessionID' parameter" );

			ConnectionList = connectionList;
			if( sessionID != null )
			{
				ASSERT( sessionID != "", "Missing parameter 'sessionID'" );
				SessionID = sessionID;
			}
			else
			{
				SessionID = CommonLibs.Web.LongPolling.ConnectionList.GetSessionID( HttpContext.Current );
				ASSERT( !string.IsNullOrEmpty(SessionID), "'" + GetType().FullName + "' objects cannot be created outside an HTTP context with the ASP.NET's session accessible" );
			}

			switch( persistanceType )
			{
				case PersistanceTypes.Connection:
					PersistanceType = PersistanceTypes.Connection;
					ASSERT( !string.IsNullOrEmpty(connectionID), "Missing parameter 'connectionID'" );
					ConnectionID = connectionID;

					if(! ConnectionList.CheckConnectionIsValid(SessionID, ConnectionID) )
						// This connection is not registered to this ConnectionList
						throw new ArgumentException( "Connection '" + ConnectionID + "' of session '" + SessionID + "' does not exist" );

					// Assign ConnectionLostCallback
					ConnectionLostCallback = new Action<string>( ConnectionList_ConnectionLost );
					ConnectionList.ConnectionLost += ConnectionLostCallback;

					if(! ConnectionList.CheckConnectionIsValid(SessionID, ConnectionID) )
					{
						// Very unlikely, but this connection has just been closed !
						// => Since this may have occured right after the first 'CheckConnectionIsValid()' AND right before the 'ConnectionList.ConnectionLost' event assignment,
						//		the 'ConnectionList_ConnectionLost()' callback might never be called...
						// => Manually unregister the callback
						FAIL( "The connection '" + ConnectionID + "' has been unexpectedly closed" );
						ConnectionList.ConnectionLost -= ConnectionLostCallback;

						throw new ArgumentException( "Connection '" + ConnectionID + "' of session '" + SessionID + "' does not exist" );
					}
					break;

				case PersistanceTypes.Session:
					PersistanceType = PersistanceTypes.Session;
					ASSERT( connectionID == null, "Parameter 'connectionID' should not be specified here" );
					ConnectionID = null;

					if(! ConnectionList.CheckSessionIsValid(SessionID) )
						// This session is not registered to this ConnectionList
						throw new ArgumentException( "Session '" + SessionID + "' does not exist" );

					// Assign SessionClosedCallback
					SessionClosedCallback = new Action<string>( ConnectionList_SessionClosed );
					ConnectionList.SessionClosed += new Action<string>(ConnectionList_SessionClosed);

					if(! ConnectionList.CheckSessionIsValid(SessionID) )
					{
						// Very unlikely, but this session has just been closed !
						// => Since this may have occured right after the first 'CheckSessionIsValid()' AND right before the 'ConnectionList.SessionClosed' event assignment,
						//		the 'ConnectionList_SessionClosed()' callback might never be called...
						// => Manually unregister the callback
						FAIL( "The session '" + SessionID + "' has been unexpectedly closed" );
						ConnectionList.SessionClosed -= SessionClosedCallback;

						throw new ArgumentException( "Session '" + SessionID + "' does not exist" );
					}
					break;

				default:
					throw new NotImplementedException( "Unknown persistanceType '" + persistanceType + "'" );
			}
		}

		private void ConnectionList_SessionClosed(string sessionID)
		{
			if( SessionID == sessionID )
			{
				ConnectionList.SessionClosed -= SessionClosedCallback;
				SessionClosedCallback = null;

				Close();
			}
		}

		private void ConnectionList_ConnectionLost(string connectionID)
		{
			if( ConnectionID == connectionID )
			{
				ConnectionList.ConnectionLost -= ConnectionLostCallback;
				ConnectionLostCallback = null;

				Close();
			}
		}

		public void Dispose()
		{
			if( Closed != 0 )
				// Already terminated
				return;
			Close();
		}

		public void Close()
		{
			var oldClosed = System.Threading.Interlocked.Increment( ref Closed );
			if( oldClosed > 0 )
				// Was already closed
				return;

			// Call child class method
			try { OnClose(); }
			catch( System.Exception ex ) { FAIL( "'OnTerminate()' threw exception '" + ex.GetType().FullName + "': " + ex.Message ); }
		}
	}
}