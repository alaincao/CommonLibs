//
// CommonLibs/Utils/ExtensionMethods.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2018 Alain CAO
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
using System.Text;

namespace CommonLibs.Utils
{
	public static class ExtensionMethods
	{
		public static string EscapeQuotes(this string str)
		{
			if( str == null )
				return null;
			return str.Replace( "'", "\\'" );
		}

		public static string EscapeDQuotes(this string str)
		{
			if( str == null )
				return null;
			return str.Replace( "\"", "\\\"" );
		}

		public static string Left(this string str, int n)
		{
			if( str == null )
				return null;
			if( n >= str.Length )
				return str;
			var rv = str.Substring( 0, n );
			return rv;
		}

		public static string Right(this string str, int n)
		{
			if( str == null )
				return null;
			if( n >= str.Length )
				return str;
			string rv = str.Substring( (str.Length-n), n );
			return rv;
		}

		public static int IndexOf(this byte[] self, byte[] searchPattern, int startIndex=0)
		{
			CommonLibs.Utils.Debug.ASSERT( self != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'self'" );
			CommonLibs.Utils.Debug.ASSERT( searchPattern != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'searchPattern'" );
			CommonLibs.Utils.Debug.ASSERT( startIndex >= 0, System.Reflection.MethodInfo.GetCurrentMethod(), "Invalid value for parameter 'startIndex': " + startIndex );

			var endIndex = self.Length - searchPattern.Length;
			for(; startIndex<endIndex; ++startIndex )
			{
				for( int i=0; i<searchPattern.Length; ++i )
				{
					if( self[startIndex+i] != searchPattern[i] )
						// Pattern does not match
						goto NotFound;
				}
				// Pattern found:
				return startIndex;
			NotFound:
				continue;
			}
			// Pattern not found in 'self'
			return -1;
		}

		public static int? TryGet<K>(this IDictionary<K,int> dict, K key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'this'" );
			CommonLibs.Utils.Debug.ASSERT( key != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );

			int value;
			if( dict.TryGetValue(key, out value) )
				return value;
			return null;
		}

		public static V TryGet<K,V>(this IDictionary<K,V> dict, K key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'this'" );
			CommonLibs.Utils.Debug.ASSERT( key != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );

			V value;
			if( dict.TryGetValue(key, out value) )
				return value;
			return default( V );
		}

		public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> callback)
		{
			foreach( var item in enumerable )
				callback( item );
		}

		public static void CopyTo(this byte[] src, byte[] dst, int srcStartIndex=0, int dstStartIndex=0, int n=-1)
		{
			CommonLibs.Utils.Debug.ASSERT( src != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'src'" );
			CommonLibs.Utils.Debug.ASSERT( dst != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'dst'" );

			if( n == -1 )
				n = src.Length - srcStartIndex;
			//for( int i=0; i<n; ++i )
			//    dst[ dstStartIndex+i ] = src[ dstStartIndex+i ];
			System.Buffer.BlockCopy( src, srcStartIndex, dst, dstStartIndex, n );
		}

		public static byte[] Concat(this byte[] src, byte[] dst, int srcStartIndex=0, int srcCount=-1, int dstStartIndex=0, int dstCount=-1)
		{
			CommonLibs.Utils.Debug.ASSERT( src != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'src'" );
			CommonLibs.Utils.Debug.ASSERT( dst != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'dst'" );

			if( srcCount == -1 )
				srcCount = src.Length - srcStartIndex;
			if( dstCount == -1 )
				dstCount = dst.Length - dstStartIndex;

			var rv = new byte[ srcCount + dstCount ];
			src.CopyTo( rv, dstStartIndex:0, srcStartIndex:srcStartIndex, n:srcCount );
			dst.CopyTo( rv, dstStartIndex:srcCount, srcStartIndex:dstStartIndex, n:dstCount );
			return rv;
		}
	}
}
