﻿//
// CommonLibs/Utils/ExtensionMethods.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2018 Alain CAO
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

using System;
using System.Collections.Generic;
using System.Linq;

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

		public static object TryGetValue(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj = null;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			return obj;
		}

		public static string TryGetString(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			return "" + obj;
		}

		public static byte? TryGetByte(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			var a = obj as byte?;
			if( a != null )
				return a.Value;
			byte b;
			if( byte.TryParse(""+obj, out b) )
				return b;
			else
				return null;
		}

		public static short? TryGetShort(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			var a = obj as short?;
			if( a != null )
				return a.Value;
			short b;
			if( short.TryParse(""+obj, out b) )
				return b;
			else
				return null;
		}

		public static int? TryGetInt(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			var a = obj as int?;
			if( a != null )
				return a.Value;
			int b;
			if( int.TryParse(""+obj, out b) )
				return b;
			else
				return null;
		}

		public static long? TryGetLong(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error
			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			var a = obj as long?;
			if( a != null )
				return a.Value;
			long b;
			if( long.TryParse(""+obj, out b) )
				return b;
			else
				return null;
		}

		public static bool? TryGetBool(this IDictionary<string,object> dict, string key)
		{
			CommonLibs.Utils.Debug.ASSERT( dict != null, System.Reflection.MethodInfo.GetCurrentMethod(), "Missing 'this' parameter" );
			CommonLibs.Utils.Debug.ASSERT( !string.IsNullOrEmpty(key), System.Reflection.MethodInfo.GetCurrentMethod(), "Missing parameter 'key'" );  // Assuming null or "" is a sign of an error

			object obj;
			if(! dict.TryGetValue(key, out obj) )
				return null;
			var a = obj as bool?;
			return a;
		}

		public static T NewAnonymousType<T>(this T templateObject)
		{
			return default(T);
		}

		public static IQueryable<T> NewAnonymousQueryable<T>(this T templateObject)
		{
			return (new T[]{}).AsQueryable();
		}

		public static IEnumerable<T> NewAnonymousEnumerable<T>(this T templateObject, bool fillWithTemplate=false)
		{
			if( fillWithTemplate )
				return new T[]{ templateObject };
			else
				return (new T[]{}).AsEnumerable();
		}

		public static T[] NewAnonymousArray<T>(this T templateObject)
		{
			return new T[]{};
		}

		public static List<T> NewAnonymousList<T>(this T templateObject)
		{
			return new List<T>();
		}

		public static List<T> NewAnonymousListFromEnumerable<T>(this IEnumerable<T> templateObject)
		{
			return new List<T>();
		}

		public static Dictionary<K,T> NewAnonymousDictionary<T,K>(this T templateObject, K templateKey)
		{
			return new Dictionary<K,T>();
		}

		public static void AddRange<K,V>(this IDictionary<K,V> self, IDictionary<K,V> other)
		{
			foreach( var pair in other )
				self[ pair.Key ] = pair.Value;
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

		/// <summary>Use on Tasks that are not awaited</summary>
		public static void FireAndForget(this System.Threading.Tasks.Task task)
		{
		#if DEBUG

			// Check if any exception occurred
			task.ContinueWith( (_)=>
				{
					if( task.Exception != null )
						CommonLibs.Utils.Debug.ASSERT( false, nameof(FireAndForget), $"An async task that was not awaited threw an exception ({task.Exception.GetType().FullName}): {task.Exception.Message}" );
				} );

		#endif
		}
	}
}
