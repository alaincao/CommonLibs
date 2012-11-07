﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Web;

using CommonLibs.Utils;

namespace CommonLibs.Web
{
	public class HtmlPostedMultipartStreamParser
	{
		public static readonly byte[]				CRLF						= new byte[]{ 0x0D, 0x0A };
		public static readonly byte[]				CRLFCRLF					= new byte[]{ 0x0D, 0x0A, 0x0D, 0x0A };

		private enum States
		{
			Initial,
			ReadingContentHeader,
			ReadingVariableContent,
			ReadingFileContent
		}
		private States								State						= States.Initial;

		private Encoding							ContentEncoding;
		private string								ContentType;
		private byte[]								ContentBoundaryInitial;
		private byte[]								ContentBoundary;

		/// <remarks>Only available when running from ProcessRequest()</remarks>
		public long									ContentLength				{ get; private set; }
		public long									CurrentLength				{ get; private set; }

		private byte[]								PendingBuffer				= null;

		/// <summary>
		/// Triggered when a new file is being received from the HTTP stream<br/>
		/// Parameter 1: The file name
		/// </summary>
		public event Action<string>					OnNewFile;
		/// <summary>
		/// Triggered when a new variable is being received from the HTTP stream<br/>
		/// Parameter 1: The posted variable name
		/// </summary>
		public event Action<string>					OnNewVariable;

		/// <summary>
		/// Triggered when data can be written to the file declared by the event 'OnNewFile'<br/>
		/// Parameter 1: The data buffer that can be read from<br/>
		/// Parameter 2: The number of bytes that can be read from the data buffer (Parametr 1)
		/// </summary>
		public event Action<byte[],int>				OnFileContentReceived;

		/// <summary>
		/// Triggered when data can be written to the file declared by the event 'OnNewVariable'<br/>
		/// Parameter 1: The data buffer that can be read from<br/>
		/// Parameter 2: The number of bytes that can be read from the data buffer (Parametr 1)
		/// </summary>
		public event Action<byte[],int>				OnVariableContentReceived;

		/// <summary>
		/// Triggered when the end of the file declared by the event 'OnNewFile' can be closed
		/// </summary>
		public event Action							OnEndOfFile;
		/// <summary>
		/// Triggered when the end of the variable declared by the event 'OnNewVariable' can be closed
		/// </summary>
		public event Action							OnEndOfVariable;

		public HtmlPostedMultipartStreamParser(HttpRequest request)
		{
			ContentEncoding = request.ContentEncoding;
			ContentType = request.ContentType;
			ContentLength = request.ContentLength;
			if( ContentLength == 0 )
				// Happens when uploaded file size > ~2G (when ContentLength > int.MaxInt, browsers sends invalid negative ContentSize)
				ContentLength = -1;  // NB: Setting to -1 so that 'bytesRead == contentLength' below doesn't match

			if(! ContentType.Contains("multipart/form-data") )
				throw new ArgumentException( "POSTed form data is not of type 'multipart/form-data'" );

			var contentType = ContentType;
			var i = contentType.ToLower().IndexOf( "boundary=" );
			string str = "--" + contentType.Substring( i + "boundary=".Length );
			ContentBoundaryInitial = Encoding.ASCII.GetBytes( str );
			ContentBoundary = CRLF.Concat( ContentBoundaryInitial );  // The ContentBoundaries are prefixed with CR/LF (except for the first(initial) one)
		}

		/// <summary>
		/// Read from the context's posted HTTP stream
		/// </summary>
		/// <param name="context">The HTTP request's context</param>
		/// <remarks>Will read directly from the underlying HttpWorkerRequest. So the context is short-circtuited and cannot be reused after calling this method (the Request object will not be complete)</remarks>
		public void ProcessContext(HttpContext context)
		{
			// Get the underlying worker request
			//var worker = (HttpWorkerRequest)context.GetType().GetProperty( "WorkerRequest", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic ).GetValue( context, null );
			var worker = (HttpWorkerRequest)((IServiceProvider)context).GetService( typeof(HttpWorkerRequest) );

			int bufferSize = 1024 * 1024;  // NB: No need to parametrize that since TCP packets generally arrives at 8k each => bufferSize should always be > n below
			var buffer = new byte[bufferSize];

			var preloadedBuffer = worker.GetPreloadedEntityBody();
			if( preloadedBuffer != null )
				ReceivePostedData( preloadedBuffer, preloadedBuffer.Length );

			long bytesRead = preloadedBuffer != null ? preloadedBuffer.Length : 0;
			if(! worker.IsEntireEntityBodyIsPreloaded() )
			{
				while( true )
				{
					if( bytesRead == ContentLength )
						// ContentLength reached
						break;
					int n = worker.ReadEntityBody( buffer, bufferSize );
					if( n == 0 )
						// Nothing has been read from HTTP stream => Socket closed?
						break;
					bytesRead += n;

					ReceivePostedData( buffer, n );

					CurrentLength = bytesRead;
				}
			}
			TerminatePostedData();
		}

		/// <summary>
		/// Receives data from the HTTP context's request
		/// </summary>
		/// <param name="buffer">The data array to read from</param>
		/// <param name="n">The number of bytes that can be read from the 'buffer'</param>
		public void ReceivePostedData(byte[] buffer, int n)
		{
			switch( State )
			{
				case States.Initial: {
					if( buffer.IndexOf(ContentBoundaryInitial) != 0 )
						throw new ArgumentException( "POSTed data does not start with the ContentBoundary" );
					// Switch to state 'ReadingContentHeader'
					n = n - ContentBoundaryInitial.Length;
					var newBuffer = new byte[ n ];
					buffer.CopyTo( newBuffer, srcStartIndex:ContentBoundaryInitial.Length, n:n );
					buffer = newBuffer;
					State = States.ReadingContentHeader;
					goto case States.ReadingContentHeader; }
				case States.ReadingVariableContent:
				case States.ReadingFileContent:
					SearchForContentBoundary( buffer, n );
					break;
				case States.ReadingContentHeader:
					SearchForEndOfContentHeader( buffer, n );
					break;
				default:
					throw new NotImplementedException( "Unknown State '" + State + "'" );
			}
		}

		/// <summary>
		/// Notify this instance that there is no more data to read from the POSTed stream (e.g. that the 'ReceivePostedData()' will not be called anymore)
		/// </summary>
		public void TerminatePostedData()
		{
			switch( State )
			{
				case States.Initial:
					// Nothing has been done => NOOP
					break;
				case States.ReadingContentHeader:
					// End of stream reached
					if( PendingBuffer != null )
					{
						// Here, the buffer should be equal to "--" which is the "end of content" mark
					}
					break;
				case States.ReadingVariableContent:
					if( PendingBuffer != null )
						// Flush PendingBuffer
						if( OnVariableContentReceived != null )
							OnVariableContentReceived( PendingBuffer, PendingBuffer.Length );
					break;
				case States.ReadingFileContent:
					// End of stream reached without encountering the last ContentBoundary => Request aborted?
					// NB: Can also happen when the file is >int.MaxValue(~2Gb) (+ the browsers sends an invalid content size (negative) )
					if( PendingBuffer != null )
						// Flush PendingBuffer
						if( OnFileContentReceived != null )
							OnFileContentReceived( PendingBuffer, PendingBuffer.Length );
					break;
				default:
					throw new NotImplementedException( "Unknown State '" + State + "'" );
			}
		}

		private void SearchForEndOfContentHeader(byte[] buffer, int n)
		{
			byte[] headerBuffer;
			if( PendingBuffer == null )
			{
				if( n == buffer.Length )
				{
					headerBuffer = buffer;
				}
				else
				{
					headerBuffer = new byte[ n ];
					buffer.CopyTo( headerBuffer, n:n );
				}
			}
			else
			{
				headerBuffer = PendingBuffer.Concat( buffer, dstCount:n );
			}

			var eohIndex = headerBuffer.IndexOf( CRLFCRLF );
			if( eohIndex == -1 )
			{
				// End Of Header not found

				// Save as PendingBuffer for next iteration
				PendingBuffer = headerBuffer;
			}
			else  // End Of Header found
			{
				var headerContent = new byte[ eohIndex ];
				headerBuffer.CopyTo( headerContent, n:eohIndex );
				string fileName;
				string variableName;
				ParseHeader( headerContent, out fileName, out variableName );
				if( fileName != null )
				{
					if( OnNewFile != null )
						OnNewFile( fileName );

					// Switch to ReadingFileContent state
					State = States.ReadingFileContent;
				}
				else
				{
					if( OnNewVariable != null )
						OnNewVariable( variableName );

					// Switch to ReadingVariableContent state
					State = States.ReadingVariableContent;
				}

				PendingBuffer = null;
				var newBufferSize = headerBuffer.Length - (eohIndex + CRLFCRLF.Length);
				if( newBufferSize > 0 )
				{
					var newBuffer = new byte[ newBufferSize ];
					headerBuffer.CopyTo( newBuffer, srcStartIndex:(eohIndex + CRLFCRLF.Length), n:newBufferSize );
					SearchForContentBoundary( newBuffer, newBufferSize );
				}
				else
				{
					CommonLibs.Utils.Debug.ASSERT( newBufferSize == 0, this, "'newBufferSize' is not supposed to be negative here" );
				}
			}
		}

		private void ParseHeader(byte[] headerContent, out string fileName, out string variableName)
		{
			var headerString = ContentEncoding.GetString( headerContent );
			var headerLines = headerString	.Split( new string[]{"\r\n"}, StringSplitOptions.RemoveEmptyEntries )
											.Select( v=>v.Trim() );
			var contentLine = headerLines	.Where( v=>v.StartsWith("Content-Disposition: ") )
											.Single()
											.Substring( "Content-Disposition: ".Length );
			var contentTokens = contentLine	.Split( ';' )
											.Select( v=>v.Trim() );
			fileName = contentTokens	.Where( v=>v.StartsWith("filename") )
										.Select( v=>v.Substring(v.IndexOf('=')+1).Trim() )
										.SingleOrDefault();
			if( fileName != null )
			{
				if( fileName.StartsWith("\"") /*|| fileName.StartsWith("'")*/ )
					// Remove surrounding quotes
					fileName = fileName.Substring( 1, fileName.Length-2 );
				// Remove directory path if any
				fileName = System.IO.Path.GetFileName( fileName );
			}
			variableName = contentTokens	.Where( v=>v.StartsWith("name") )
											.Select( v=>v.Substring(v.IndexOf('=')+1).Trim() )
											.Single();
		}

		private void SearchForContentBoundary(byte[] buffer, int n)
		{
			byte[] searchBuffer;
			if( PendingBuffer != null )
			{
				searchBuffer = PendingBuffer.Concat( buffer, dstCount:n );
			}
			else
			{
				if( buffer.Length == n )
				{
					searchBuffer = buffer;
				}
				else
				{
					searchBuffer = new byte[ n ];
					buffer.CopyTo( searchBuffer, n:n );
				}
			}

			var boundaryIndex = searchBuffer.IndexOf( ContentBoundary, startIndex:0 );
			if( boundaryIndex == -1 )
			{
				// ContentBoundary not found

				// Flush the searchBuffer (minus the end)
				var flushBufferSize = searchBuffer.Length - ContentBoundary.Length;
				switch( State )
				{
					case States.ReadingVariableContent:
						if( OnVariableContentReceived != null )
							OnVariableContentReceived( searchBuffer, flushBufferSize );
						break;
					case States.ReadingFileContent:
						if( OnFileContentReceived != null )
							OnFileContentReceived( searchBuffer, flushBufferSize );
						break;
					default:
						throw new NotImplementedException( "State '" + State + "' is not supported here" );
				}

				// Put the end of the current buffer as pending for next iteration
				PendingBuffer = new byte[ ContentBoundary.Length ];
				searchBuffer.CopyTo( PendingBuffer, srcStartIndex:(searchBuffer.Length-PendingBuffer.Length), n:PendingBuffer.Length );
			}
			else
			{
				// ContentBoundary found
				if( boundaryIndex != 0 )
				{
					switch( State )
					{
						// Flush content up to the ContentBoundary
						case States.ReadingVariableContent:
							if( OnVariableContentReceived != null )
								OnVariableContentReceived( searchBuffer, boundaryIndex );
							break;
						case States.ReadingFileContent:
							if( OnFileContentReceived != null )
								OnFileContentReceived( searchBuffer, boundaryIndex );
							break;
						default:
							throw new NotImplementedException( "State '" + State + "' is not supported here" );
					}
				}

				switch( State )
				{
					// Flush content up to the ContentBoundary
					case States.ReadingVariableContent:
						if( OnEndOfVariable != null )
							OnEndOfVariable();
						break;
					case States.ReadingFileContent:
						if( OnEndOfFile != null )
							OnEndOfFile();
						break;
					default:
						throw new NotImplementedException( "State '" + State + "' is not supported here" );
				}

				// Switch to 'ReadingContentHeader' state
				State = States.ReadingContentHeader;
				PendingBuffer = null;
				var newBufferSize = searchBuffer.Length - (boundaryIndex + ContentBoundary.Length);
				if( newBufferSize > 0 )
				{
					var newBuffer = new byte[ newBufferSize ];
					searchBuffer.CopyTo( newBuffer, srcStartIndex:(boundaryIndex+ContentBoundary.Length), n:newBufferSize );
					SearchForEndOfContentHeader( newBuffer, newBufferSize );
				}
				else
				{
					CommonLibs.Utils.Debug.ASSERT( newBufferSize == 0, this, "'newBufferSize' is not supposed to be negative here" );
				}
			}
		}
	}
}