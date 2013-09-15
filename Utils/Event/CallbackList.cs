﻿//
// CommonLibs/Utils/Event/CallbackList.cs
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

namespace CommonLibs.Utils.Event
{
	/// <summary>
	/// Thread-safe, exception-safe list of callbacks
	/// </summary>
	public class CallbackList
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		private List<Action>					List							= new List<Action>();
		public int								Count							{ get { lock(List){ return List.Count; } } }

		public void Add(Action callback)
		{
			lock( List )
			{
				List.Add( callback );
			}
		}

		public void Remove(Action callback)
		{
			bool rc;
			lock( List )
			{
				rc = List.Remove( callback );
			}
			ASSERT( rc, "Failed to remove specified callback " + callback );
		}

		public void Clear()
		{
			lock( List )
			{
				List.Clear();
			}
		}

		public void Invoke()
		{
			Action[] callbacks;
			lock( List )
			{
				if( List.Count == 0 )
					return;
				callbacks = List.ToArray();
			}

			foreach( var callback in callbacks )
			{
				try { callback(); }
				catch( System.Exception ex ) { FAIL( "Callback invocation threw an exception(" + ex.GetType().FullName + "): "+ ex.Message ); }
			}
		}
	}

	/// <summary>
	/// Thread-safe, exception-safe list of callbacks
	/// </summary>
	public class CallbackList<T>
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		private List<Action<T>>					List							= new List<Action<T>>();
		public int								Count							{ get { lock(List){ return List.Count; } } }

		public void Add(Action<T> callback)
		{
			lock( List )
			{
				List.Add( callback );
			}
		}

		public void Remove(Action<T> callback)
		{
			bool rc;
			lock( List )
			{
				rc = List.Remove( callback );
			}
			ASSERT( rc, "Failed to remove specified callback " + callback );
		}

		public void Clear()
		{
			lock( List )
			{
				List.Clear();
			}
		}

		public void Invoke(T value)
		{
			Action<T>[] callbacks;
			lock( List )
			{
				if( List.Count == 0 )
					return;
				callbacks = List.ToArray();
			}

			foreach( var callback in callbacks )
			{
				try { callback(value); }
				catch( System.Exception ex ) { FAIL( "Callback invocation threw an exception(" + ex.GetType().FullName + "): "+ ex.Message ); }
			}
		}
	}
}