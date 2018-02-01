//
// CommonLibs/Utils/ThreadResource.cs
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
using System.Threading;

namespace CommonLibs.Utils
{
	/// <summary>
	/// Create references to objects that can be reused but not in different thread.<br/>
	/// E.g. a database connection or a data cache
	/// </summary>
	public class ThreadResource<K,T>
	{
		[System.Diagnostics.Conditional("DEBUG")] protected internal void LOG(string message)				{ CommonLibs.Utils.Debug.LOG( this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected internal void ASSERT(bool test, string message)	{ CommonLibs.Utils.Debug.ASSERT( test, this, message ); }
		[System.Diagnostics.Conditional("DEBUG")] protected internal void FAIL(string message)				{ CommonLibs.Utils.Debug.ASSERT( false, this, message ); }

		public class Reference : IDisposable
		{
			public T					Instance			{ get; private set; }
			internal Action				DisposeCallback		= null;

			public Reference(T instance)
			{
				Instance = instance;
			}

			public void Dispose()
			{
				if( DisposeCallback != null )
					DisposeCallback();
			}
		}

		private class Entry
		{
			internal int?			ThreadID			= null;
			internal DateTime		ExpirationDate;
			internal T				Object;
		}

		private object						Locker				{ get { return Instances; } }
		private Dictionary<K,List<Entry>>	Instances			= new Dictionary<K,List<Entry>>();

		/// <remarks>All instance returned by this function must be enclosed with 'using{}'</remarks>
		public Reference Get(K key, TimeSpan instanceLifeTime, Func<T> constructor)
		{
			var threadID = Thread.CurrentThread.ManagedThreadId;
			var now = DateTime.Now;

			bool unassignThreadIDOnDispose;
			List<Entry> entries;
			Entry entry;
			lock( Locker )
			{
				if(! Instances.TryGetValue(key, out entries) )
				{
					entries = new List<Entry>();
					Instances.Add( key, entries );
				}

				Entry lastFree = null;
				foreach( var e in entries )
				{
					var eThreadID = e.ThreadID;  // NB: Need to have an atomic access to this entry's member because it can be set to null by the 'Dispose' below (race condition)

					if( e.ExpirationDate < now )
					{
						// This entry is expired => Don't use
					}
					else if( eThreadID == null )
					{
						// This entry is available
						lastFree = e;
					}
					else if( eThreadID.Value == threadID )
					{
						// This entry is assigned to this thread => Use it
						entry = e;
						unassignThreadIDOnDispose = false;  // This new reference is not the instance's owner
						goto FOUND;
					}
				}
				// An Entry assigned to this thread has not been found

				if( lastFree != null )
				{
					// There is an instance available for use
					entry = lastFree;
					entry.ThreadID = threadID;
					unassignThreadIDOnDispose = true;  // This new reference is taking ownership of this instance
					goto FOUND;
				}
				else  // A new entry must be created
				{
					goto NOTFOUND;
				}
			}//lock

		NOTFOUND:

			var expirationDate = now + instanceLifeTime;
			var obj = constructor();
			entry = new Entry{	ThreadID = threadID,
								ExpirationDate = expirationDate,
								Object = obj };
			lock( Locker )
			{
				entries.Add( entry );
			}
			unassignThreadIDOnDispose = true;

		FOUND:

			var reference = new Reference( entry.Object );
			if( unassignThreadIDOnDispose )
				reference.DisposeCallback = ()=>
					{
						// Don't need to call twice
						reference.DisposeCallback = null;

						// The top reference to this resource is disposing
						// => This resource is no more associated to this thread => Liberate it
						entry.ThreadID = null;  // Performance: No need to 'lock{}' ; c.f. "atomic access" comment above

						// Dispose any expired references
						CheckExpired();
					};
			return reference;
		}

		public void CheckExpired()
		{
			var now = DateTime.Now;
			var allExpired = new List<Entry>();
			lock( Locker )
			{
				foreach( var pair in Instances.ToArray() )
				{
					var key = pair.Key;
					var list = pair.Value;

					var expired = list	.Where( v=>v.ThreadID == null )  // Entries that are currently not in use
										.Where( v=>v.ExpirationDate < now )  // which are expired
										.ToArray();  // Close enumerators so the list can be modified
					if( expired.Length > 0 )
					{
						foreach( var entry in expired )
						{
							var rc = list.Remove( entry );
							ASSERT( rc, "'list.Remove()' failed" );
						}

						if( list.Count == 0 )
						{
							var rc = Instances.Remove( key );
							ASSERT( rc, "'Instances.Remove()' failed" );
						}

						allExpired.AddRange( expired );
					}
				}
			}//lock

			foreach( var entry in allExpired )
			{
				var disposable = entry.Object as IDisposable;
				if( disposable != null )
				{
					try { disposable.Dispose(); }
					catch( System.Exception ex )  { FAIL( "'disposable.Dispose()' threw an exception (" + ex.GetType().FullName + "): " + ex.Message ); }
				}
			}
		}
	}
}
