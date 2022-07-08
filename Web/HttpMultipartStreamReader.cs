//
// CommonLibs/Web/HttpMultipartStreamReader.cs
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
using System.Text;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

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

		private readonly Encoding					ContentEncoding;
		public string								ContentType					{ get; private set; }
		private readonly byte[]						ContentBoundaryInitial;
		private readonly byte[]						ContentBoundary;

		/// <remarks>Only available when running from ProcessRequest()</remarks>
		public readonly long						ContentLength;
		public long									CurrentLength				{ get; private set; }

		private byte[]								PendingBuffer				= null;

		/// <summary>
		/// Triggered when a new file is being received from the HTTP stream<br/>
		/// Parameter 1: The file name
		/// </summary>
		public List<Func<string,Task>>				OnNewFile					{ get; } = new List<Func<string,Task>>();
		/// <summary>
		/// Triggered when a new variable is being received from the HTTP stream<br/>
		/// Parameter 1: The posted variable name
		/// </summary>
		public List<Func<string,Task>>				OnNewVariable				{ get; } = new List<Func<string,Task>>();

		/// <summary>
		/// Triggered when data can be written to the file declared by the event 'OnNewFile'<br/>
		/// Parameter 1: The data buffer that can be read from<br/>
		/// Parameter 2: The number of bytes that can be read from the data buffer (Parametr 1)
		/// </summary>
		public List<Func<byte[],int,Task>>			OnFileContentReceived		{ get; } = new List<Func<byte[],int,Task>>();

		/// <summary>
		/// Triggered when data can be written to the file declared by the event 'OnNewVariable'<br/>
		/// Parameter 1: The data buffer that can be read from<br/>
		/// Parameter 2: The number of bytes that can be read from the data buffer (Parametr 1)
		/// </summary>
		public List<Func<byte[],int,Task>>			OnVariableContentReceived	{ get; } = new List<Func<byte[],int,Task>>();

		/// <summary>
		/// Triggered when the end of the file declared by the event 'OnNewFile' can be closed
		/// </summary>
		public List<Func<Task>>						OnEndOfFile					{ get; } = new List<Func<Task>>();
		/// <summary>
		/// Triggered when the end of the variable declared by the event 'OnNewVariable' can be closed
		/// </summary>
		public List<Func<Task>>						OnEndOfVariable				{ get; } = new List<Func<Task>>();

		public HtmlPostedMultipartStreamParser(HttpRequest request)
		{
			ContentType = request.ContentType;
			ContentEncoding = MediaTypeHeaderValue.Parse( ContentType ).Encoding ?? Encoding.GetEncoding( "ISO-8859-1" );  // Fallsback to default as described here: https://www.w3.org/International/articles/http-charset/index
			ContentLength = request.ContentLength ?? -1;  // Can happen when uploaded file size > ~2G (when ContentLength > int.MaxInt, browsers sends invalid negative ContentSize)
														  // NB: Setting to -1 so that 'bytesRead == contentLength' below never match

			if(! ContentType.Contains("multipart/form-data") )
				throw new ArgumentException( "POSTed form data is not of type 'multipart/form-data'" );

			var contentType = ContentType;
			var i = contentType.ToLower().IndexOf( "boundary=" );
			string str = "--" + contentType.Substring( i + "boundary=".Length );
			ContentBoundaryInitial = Encoding.ASCII.GetBytes( str );
			ContentBoundary = CRLF.Concat( ContentBoundaryInitial );  // The ContentBoundaries are prefixed with CR/LF (except for the first(initial) one)
		}

		public Task ProcessContext(HttpContext context)
			=> ProcessRequest( context.Request );

		/// <summary>
		/// Read from the HTTP request's posted stream
		/// </summary>
		/// <param name="request">The HTTP request to read from</param>
		public async Task ProcessRequest(HttpRequest request)
		{
			int bufferSize = 1024 * 1024;  // NB: No need to parametrize that since TCP packets generally arrives at 8k each => bufferSize should always be > n below
			var buffer = new byte[bufferSize];
			CurrentLength = 0;
			for( var n=await request.Body.ReadAsync(buffer); n>0; n=await request.Body.ReadAsync(buffer) )
			{
				await ReceivePostedData( buffer, n );
				CurrentLength += n;
			}
			await TerminatePostedData();
		}

		/// <summary>
		/// Receives data from the HTTP context's request
		/// </summary>
		/// <param name="buffer">The data array to read from</param>
		/// <param name="n">The number of bytes that can be read from the 'buffer'</param>
		public async Task ReceivePostedData(byte[] buffer, int n)
		{
			switch( State )
			{
				case States.Initial: {
					if( PendingBuffer == null )
					{
						PendingBuffer = new byte[ n ];
						buffer.CopyTo( PendingBuffer, n:n );
					}
					else
					{
						PendingBuffer = PendingBuffer.Concat( buffer, dstCount:n );
					}
					if( PendingBuffer.Length < ContentBoundaryInitial.Length )
						// The content boundary has not been loaded entirely => Proceed with next buffer
						break;
					if( PendingBuffer.IndexOf(ContentBoundaryInitial) != 0 )
						throw new ArgumentException( "POSTed data does not start with the ContentBoundary" );
					// Switch to state 'ReadingContentHeader'
					n = PendingBuffer.Length - ContentBoundaryInitial.Length;
					buffer = new byte[ n ];
					PendingBuffer.CopyTo( buffer, srcStartIndex:ContentBoundaryInitial.Length, n:n );
					PendingBuffer = null;
					State = States.ReadingContentHeader;
					goto case States.ReadingContentHeader; }
				case States.ReadingVariableContent:
				case States.ReadingFileContent:
					await SearchForContentBoundary( buffer, n );
					break;
				case States.ReadingContentHeader:
					await SearchForEndOfContentHeader( buffer, n );
					break;
				default:
					throw new NotImplementedException( "Unknown State '" + State + "'" );
			}
		}

		/// <summary>
		/// Notify this instance that there is no more data to read from the POSTed stream (e.g. that the 'ReceivePostedData()' will not be called anymore)
		/// </summary>
		public async Task TerminatePostedData()
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
						await Task.WhenAll( OnVariableContentReceived.Select( f=>f(PendingBuffer, PendingBuffer.Length) ) );
					break;
				case States.ReadingFileContent:
					// End of stream reached without encountering the last ContentBoundary => Request aborted?
					// NB: Can also happen when the file is >int.MaxValue(~2Gb) (+ the browsers sends an invalid content size (negative) )
					if( PendingBuffer != null )
						// Flush PendingBuffer
						await Task.WhenAll( OnFileContentReceived.Select( f=>f(PendingBuffer, PendingBuffer.Length) ) );
					break;
				default:
					throw new NotImplementedException( "Unknown State '" + State + "'" );
			}
		}

		private async Task SearchForEndOfContentHeader(byte[] buffer, int n)
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
					await Task.WhenAll( OnNewFile.Select( f=>f(fileName) ) );

					// Switch to ReadingFileContent state
					State = States.ReadingFileContent;
				}
				else
				{
					await Task.WhenAll( OnNewVariable.Select( f=>f(variableName) ) );

					// Switch to ReadingVariableContent state
					State = States.ReadingVariableContent;
				}

				PendingBuffer = null;
				var newBufferSize = headerBuffer.Length - (eohIndex + CRLFCRLF.Length);
				if( newBufferSize > 0 )
				{
					var newBuffer = new byte[ newBufferSize ];
					headerBuffer.CopyTo( newBuffer, srcStartIndex:(eohIndex + CRLFCRLF.Length), n:newBufferSize );
					await SearchForContentBoundary( newBuffer, newBufferSize );
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
			var contentLine = headerLines	.Single( v=>v.StartsWith("Content-Disposition: ") )
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

		private async Task SearchForContentBoundary(byte[] buffer, int n)
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

				// Flush the searchBuffer (minus the last bytes that may contain the 'ContentBoundary')
				var flushBufferSize = searchBuffer.Length - ContentBoundary.Length;
				if( flushBufferSize <= 0 )
				{
					// 'searchBuffer' is too small to contain data that can be flushed => Keep it as pending for next iteration
					PendingBuffer = searchBuffer;
				}
				else
				{
					switch( State )
					{
						case States.ReadingVariableContent:
							await Task.WhenAll( OnVariableContentReceived.Select( f=>f(searchBuffer, flushBufferSize) ) );
							break;
						case States.ReadingFileContent:
							await Task.WhenAll( OnFileContentReceived.Select( f=>f(searchBuffer, flushBufferSize) ) );
							break;
						default:
							throw new NotImplementedException( "State '" + State + "' is not supported here" );
					}

					// Put the end of the current buffer as pending for next iteration
					PendingBuffer = new byte[ ContentBoundary.Length ];
					searchBuffer.CopyTo( PendingBuffer, srcStartIndex:(searchBuffer.Length-PendingBuffer.Length), n:PendingBuffer.Length );
				}
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
							await Task.WhenAll( OnVariableContentReceived.Select( f=>f(searchBuffer, boundaryIndex) ) );
							break;
						case States.ReadingFileContent:
							await Task.WhenAll( OnFileContentReceived.Select( f=>f(searchBuffer, boundaryIndex) ) );
							break;
						default:
							throw new NotImplementedException( "State '" + State + "' is not supported here" );
					}
				}

				switch( State )
				{
					// Flush content up to the ContentBoundary
					case States.ReadingVariableContent:
						await Task.WhenAll( OnEndOfVariable.Select( f=>f() ) );
						break;
					case States.ReadingFileContent:
						await Task.WhenAll( OnEndOfFile.Select( f=>f() ) );
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
					await SearchForEndOfContentHeader( newBuffer, newBufferSize );
				}
				else
				{
					CommonLibs.Utils.Debug.ASSERT( newBufferSize == 0, this, "'newBufferSize' is not supposed to be negative here" );
				}
			}
		}
	}
}
