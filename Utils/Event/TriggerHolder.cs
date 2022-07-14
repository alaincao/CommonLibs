//
// CommonLibs/Utils/Event/TriggerHolder.cs
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
using System.Text;

namespace CommonLibs.Utils.Event
{
	public interface ITriggerHoldable
	{
		bool				NoTrigger			{ get; set; }
	}

	public sealed class TriggerHolder<T> : IDisposable where T : class, ITriggerHoldable
	{
		private readonly bool		OldValue;
		private readonly T			Helper;

		public TriggerHolder(T helper)
		{
			Helper = helper;
			OldValue = Helper.NoTrigger;
			Helper.NoTrigger = true;
		}

		public void Dispose()
		{
			Helper.NoTrigger = OldValue;
		}
	}
}
