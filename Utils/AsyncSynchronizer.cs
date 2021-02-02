//
// CommonLibs/Utils/AsyncSynchronizer.cs
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
using System.Threading.Tasks;

namespace CommonLibs.Utils
{
	/// <summary>Can be used with async/await paradigms in place of "lock{}" blocks</summary>
	public class AsyncSynchronizer
	{
		private AsyncQueue<Tuple<TaskCompletionSource<bool>,Func<Task>>>	Queue		= new AsyncQueue<Tuple<TaskCompletionSource<bool>,Func<Task>>>();

		public AsyncSynchronizer()
		{
			// Launch the loop in background
			Loop().FireAndForget();
		}

		public async Task Run(Func<Task> callback)
		{
			// Push the callback in the queue
			var tcs = Tuple.Create( new TaskCompletionSource<bool>(), callback );
			await Queue.Push( tcs );

			// Wait for the callbak to be executed in 'Loop()'
			await tcs.Item1.Task;
		}

		/// <summary>The one and only loop to run the callbacks</summary>
		private async Task Loop()
		{
			while( true )
			{
				var item = await Queue.Pop();
				try
				{
					// Run callback
					await item.Item2();

					// Notify 'Loop()' the next item in the queue can be porocessed
					item.Item1.SetResult( /*dummy*/true );
				}
				catch( System.Exception ex )
				{
					item.Item1.SetException( ex );
				}
			}
		}
	}
}
