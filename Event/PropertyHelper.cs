using System;
using System.Collections.Generic;
using System.Text;

namespace CommonLibs.Utils.Event
{
	using E=CommonLibs.Utils.ExceptionShield;

	public class PropertyHelper<T> : IValueHelper<T>
	{
		public Action<T>			Setter;
		public Func<T>				Getter;
		public T					Value				{ get { return Getter(); } set { SetValue(value); } }
		public event Action			ValueChanged;
		public bool					Available			{ get { return GetAvailable(); } }
		public event Action			AvailableChanged;
		public bool					NoTrigger			{ get; set; }

		private void SetValue(T v)
		{
			System.Diagnostics.Debug.Assert( Getter != null && Setter != null, "Properties 'Getter' and 'Setter' must be set" );
			T oldValue = Getter();
			Setter( v );
			T value = v;

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
			System.Diagnostics.Debug.Assert( Getter != null && Setter != null, "Properties 'Getter' and 'Setter' must be set" );
			T value = Getter();

			if( value == null )
				return false;
			else if( (default(T) != null) && value.Equals(default(T)) )
				return false;
			else
				return true;
		}

		public TriggerHolder<PropertyHelper<T>> NewHolder()
		{
			return new TriggerHolder<PropertyHelper<T>>( this );
		}
	}
}
