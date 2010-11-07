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
				if( AvailableChanged != null )
					E.E( AvailableChanged );
			}

			if( ValueChanged != null )
			{
				if( availableChanged )
					E.E( ValueChanged );
				else if( (value != null) && (oldValue != null) && (!value.Equals(oldValue)) )
					E.E( ValueChanged );
			}
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
