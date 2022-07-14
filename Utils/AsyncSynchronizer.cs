//
// CommonLibs/Utils/AsyncSynchronizer.cs
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
using System.Threading.Tasks;

namespace CommonLibs.Utils
{
	/// <summary>Can be used with async/await paradigms in place of "lock{}" blocks</summary>
	public class AsyncSynchronizer
	{
		private readonly AsyncQueue<Tuple<TaskCompletionSource<object>,Func<Task<object>>>>		Queue		= new AsyncQueue<Tuple<TaskCompletionSource<object>,Func<Task<object>>>>();

		public AsyncSynchronizer()
		{
			// Launch the loop in background
			Loop().FireAndForget();
		}

		public async Task Run(Func<Task> callback)
		{
			// Push the callback in the queue
			var tcs = Tuple.Create( new TaskCompletionSource<object>(), new Func<Task<object>>(async ()=>{ await callback(); return /*dummy*/false; }) );
			await Queue.Push( tcs );

			// Wait for the callbak to be executed in 'Loop()'
			await tcs.Item1.Task;
		}

		public async Task<T> Run<T>(Func<Task<T>> callback)
		{
			// Push the callback in the queue
			var tcs = Tuple.Create( new TaskCompletionSource<object>(), new Func<Task<object>>(async ()=>(object) await callback()) );
			await Queue.Push(tcs);

			// Wait for the callbak to be executed in 'Loop()'
			return (T)await tcs.Item1.Task;
		}

		/// <summary>The one and only loop to run the callbacks</summary>
		private async Task Loop()
		{
			System.Threading.SynchronizationContext.SetSynchronizationContext( null );
			while( true )
			{
				var item = await Queue.Pop();
				try
				{
					// Run callback
					var rv = await item.Item2();

					// Notify 'Loop()' the next item in the queue can be porocessed
					item.Item1.SetResult( rv );
				}
				catch( System.Exception ex )
				{
					item.Item1.SetException( ex );
				}
			}
		}
	}
}
