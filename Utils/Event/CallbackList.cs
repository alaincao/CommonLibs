//
// CommonLibs/Utils/Event/CallbackList.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2013 Alain CAO
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

		private readonly List<Action>			List							= new List<Action>();
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

		private readonly List<Action<T>>		List							= new List<Action<T>>();
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
	/// Thread-safe, exception-safe list of asynchroneous callbacks
	/// </summary>
	public class CallbackListAsync<T>
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		private readonly List<Func<T,Task>>		List		= new List<Func<T,Task>>();  // nb: Can be 'Task<bool>'
		public int								Count		{ get { lock(List){ return List.Count; } } }

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

		private readonly List<Func<T,U,Task>>	List		= new List<Func<T,U,Task>>();
		public int								Count		{ get { lock(List){ return List.Count; } } }

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
