using System;
using System.Collections.Generic;
using System.Text;

namespace CommonLib.Utils.Event
{
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
				try
				{
					availableChanged = true;
					if( AvailableChanged != null )
						AvailableChanged();
				}
				catch( System.Exception ex )
				{
					System.Diagnostics.Debug.Fail( "Event 'AvailableChanged' failed: " + ex.Message );
				}
			}

			if( ValueChanged != null )
			{
				try
				{
					if( availableChanged )
						ValueChanged();
					else if( (value != null) && (oldValue != null) && (!value.Equals(oldValue)) )
						ValueChanged();
				}
				catch( System.Exception ex )
				{
					System.Diagnostics.Debug.Fail( "Event 'ValueChanged' failed: " + ex.Message );
				}
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
