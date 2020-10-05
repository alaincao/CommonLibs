//
// CommonLibs/Utils/Event/CallbackList.cs
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
using System.Threading.Tasks;

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

	/// <summary>
	/// Thread-safe, exception-safe list of callbacks
	/// </summary>
	public class CallbackList<T,U>
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		private List<Action<T,U>>				List							= new List<Action<T,U>>();
		public int								Count							{ get { lock(List){ return List.Count; } } }

		public void Add(Action<T,U> callback)
		{
			lock( List )
			{
				List.Add( callback );
			}
		}

		public void Remove(Action<T,U> callback)
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

		public void Invoke(T value1, U value2)
		{
			Action<T,U>[] callbacks;
			lock( List )
			{
				if( List.Count == 0 )
					return;
				callbacks = List.ToArray();
			}

			foreach( var callback in callbacks )
			{
				try { callback(value1, value2); }
				catch( System.Exception ex ) { FAIL( "Callback invocation threw an exception(" + ex.GetType().FullName + "): "+ ex.Message ); }
			}
		}
	}

	/// <summary>
	/// Thread-safe, exception-safe list of asynchroneous callbacks
	/// </summary>
	public class CallbackListAsync<T>
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		private List<Func<T,Task>>	List		= new List<Func<T,Task>>();  // nb: Can be 'Task<bool>'
		public int					Count		{ get { lock(List){ return List.Count; } } }

		public void Add(Func<T,Task> callback)
		{
			lock( List )
			{
				List.Add( callback );
			}
		}

		/// <remarks>A callback can return 'false' if the subsequent callbacks must not be invoked</remarks>
		public void Add(Func<T,Task<bool>> callback)
		{
			lock( List )
			{
				List.Add( callback );
			}
		}

		public void Remove(Func<T,Task> callback)
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

		public async Task Invoke(T value)
		{
			Func<T,Task>[] callbacks;
			lock( List )
			{
				if( List.Count == 0 )
					return;
				callbacks = List.ToArray();
			}

			foreach( var callback in callbacks )
			{
				try
				{
					var boolCallback = callback as Func<T,Task<bool>>;
					if( boolCallback == null )
					{
						// This is a 'void' callback
						await callback( value );
					}
					else
					{
						// This callback returns a boolean
						var rc = await boolCallback( value );
						if(! rc )
							// Callback did not return 'true' => Stop here
							return;
					}
				}
				catch( System.Exception ex )
				{
					FAIL( "Callback invocation threw an exception(" + ex.GetType().FullName + "): "+ ex.Message );
				}
			}
		}
	}

	/// <summary>
	/// Thread-safe, exception-safe list of asynchroneous callbacks
	/// </summary>
	public class CallbackListAsync<T,U>
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		private List<Func<T,U,Task>>	List		= new List<Func<T,U,Task>>();
		public int						Count		{ get { lock(List){ return List.Count; } } }

		public void Add(Func<T,U,Task> callback)
		{
			lock( List )
			{
				List.Add( callback );
			}
		}

		public void Remove(Func<T,U,Task> callback)
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

		public async Task Invoke(T value1, U value2)
		{
			Func<T,U,Task>[] callbacks;
			lock( List )
			{
				if( List.Count == 0 )
					return;
				callbacks = List.ToArray();
			}

			foreach( var callback in callbacks )
			{
				try { await callback(value1, value2); }
				catch( System.Exception ex ) { FAIL( "Callback invocation threw an exception(" + ex.GetType().FullName + "): "+ ex.Message ); }
			}
		}
	}
}
