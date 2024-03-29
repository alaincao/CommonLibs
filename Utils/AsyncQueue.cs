﻿//
// CommonLibs/Utils/AsyncQueue.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2020 - 2021 Alain CAO
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

namespace CommonLibs.Utils
{
	/// <summary>
	/// Producer/consumer queue that can be used with async/await paradigms
	/// </summary>
	public class AsyncQueue<T>
	{
		[System.Diagnostics.Conditional("DEBUG")] private void LOG(string message)					{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] private void FAIL(string message)					{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		private readonly Queue<T>		Queue		= new Queue<T>();
		private TaskCompletionSource<T>	TCS			= null;
		private object					Locker		=> Queue;

		public Task<AsyncQueue<T>> Push(T v)
		{
			TaskCompletionSource<T> tcs;
			lock( Locker )
			{
				if( TCS == null )
				{
					// Nobody's waiting => Enqueue & bail out
					Queue.Enqueue( v );
					goto EXIT;
				}
				else
				{
					// The queue is empty and the consumer is waiting => Give directly to the consumer ; no need to enqueue
					ASSERT( Queue.Count == 0, "Queue is supposed to be empty here!" );  // Logic error ??
					tcs = TCS;
					TCS = null;
				}
			}

			// Give the item to the waiting consumer
			tcs.SetResult( v );

		EXIT:
			return Task.FromResult( this );
		}

		public Task<AsyncQueue<T>> Push(IEnumerable<T> items)
		{
			Action afterLock = null;
			lock( Locker )
			{
				foreach( var item in items )
				{
					if( TCS != null )
					{
						// This is the first item & the consumer is waiting => Give it to him
						ASSERT( afterLock == null, $"'{nameof(afterLock)}' is supposed to be null here" );  // Logic error ...
						var firstItem = item;
						var tcs = TCS;
						TCS = null;
						afterLock = ()=>
							{
								tcs.SetResult( firstItem );
							};
						continue;
					}

					// Enqueue all remaining items
					Queue.Enqueue( item );
				}
			}
			afterLock?.Invoke();

			return Task.FromResult( this );
		}

		public async Task<T> Pop()
		{
			TaskCompletionSource<T> tcs;
			lock( Locker )
			{
				if( Queue.Count > 0 )
					// Item ready => Get it and bail out immediately
					return Queue.Dequeue();

				if( TCS != null )
				{
					// Another consumer is already waiting ?
					FAIL( $"AsyncQueue: Only one consumer is supported" );  // if this is required, the 'TCS' property should be transformed to a list(queue?) ...
					throw new InvalidOperationException( $"AsyncQueue: Only one consumer is supported" );
				}

				// Adding 'TCS' to declare I am waiting for something
				TCS = tcs = new TaskCompletionSource<T>();
			}
			return await tcs.Task;
		}

		public async Task<T[]> PopAvailable()
		{
			TaskCompletionSource<T> tcs;
			lock( Locker )
			{
				if( Queue.Count > 0 )
				{
					// Items ready => Get them and bail out immediately
					var items = Queue.ToArray();
					Queue.Clear();
					return items;
				}

				if( TCS != null )
				{
					// Another consumer is already waiting ?
					FAIL( $"AsyncQueue: Only one consumer is supported" );  // if this is required, the 'TCS' property should be transformed to a list(queue?) ...
					throw new InvalidOperationException( $"AsyncQueue: Only one consumer is supported" );
				}

				// Adding 'TCS' to declare I am waiting for something
				TCS = tcs = new TaskCompletionSource<T>();
			}
			return new T[]{ await tcs.Task };  // TODO: If multiple items were pushed, only the first one will be returned here => Find a way for the TCS to return 1 (ie. Pop() ) or multiple (ei. PopAvailable() ) items
		}
	}
}
