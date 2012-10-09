using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Web;

namespace CommonLibs.Web.LongPolling.Utils
{
	internal class LockerDisposable : IDisposable
	{
		private ReaderWriterLockSlim Locker;
		private bool WriteAccess;

		internal LockerDisposable(ReaderWriterLockSlim locker, bool writeAccess)
		{
			Locker = locker;
			WriteAccess = writeAccess;
			if( writeAccess )
				Locker.EnterWriteLock();
			else
				Locker.EnterReadLock();
		}

		public void Dispose()
		{
			if( WriteAccess )
				Locker.ExitWriteLock();
			else
				Locker.ExitReadLock();
		}
	}
}
