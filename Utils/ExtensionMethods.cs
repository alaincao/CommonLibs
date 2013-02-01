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
