//
// CommonLibs/Utils/AsyncQueue.cs
//
// Author:
//   Alain CAO (alain.cao@sigmaconso.com)
//
// Copyright (c) 2020 - 2021 Alain CAO
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
	}
}
