//
// CommonLibs/Utils/Event/ValueHelper.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2018 Alain CAO
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
	using E=CommonLibs.Utils.ExceptionShield;

	public interface IValueHelper<T> : ITriggerHoldable
	{
		T							Value				{ get; set; }
		event Action				ValueChanged;
		bool						Available			{ get; }
		event Action				AvailableChanged;
	}

	public class ValueHelper<T> : IValueHelper<T>
	{
		public T					Value				{ get { return value; } set { SetValue(value); } }
		private T					value;
		public event Action			ValueChanged;
		public bool					Available			{ get { return GetAvailable(); } }
		public event Action			AvailableChanged;
		public bool					NoTrigger			{ get; set; }

		public static implicit operator T(ValueHelper<T> self)
		{
			return self.Value;
		}

		private void SetValue(T v)
		{
			T oldValue = value;
			value = v;

			if( NoTrigger )
				return;

			bool availableChanged = false;
			if( (oldValue == null) != (value == null) )
			{
				availableChanged = true;
				TriggerAvailableChanged();
			}

			if( availableChanged )
				TriggerValueChanged();
			else if( (value != null) && (oldValue != null) && (!value.Equals(oldValue)) )
				TriggerValueChanged();
		}

		public void TriggerAvailableChanged()
		{
			if( AvailableChanged != null )
				E.E( AvailableChanged );
		}

		public void TriggerValueChanged()
		{
			if( ValueChanged != null )
				E.E( ValueChanged );
		}

		private bool GetAvailable()
		{
			if( value == null )
				return false;
			else if( (default(T) != null) && value.Equals(default(T)) )
				return false;
			else
				return true;
		}

		public TriggerHolder<ValueHelper<T>> NewHolder()
		{
			return new TriggerHolder<ValueHelper<T>>( this );
		}
	}
}
