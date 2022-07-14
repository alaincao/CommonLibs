//
// CommonLibs/Utils/Event/PropertyHelper.cs
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
	using E=CommonLibs.Utils.ExceptionShield;

	public class GetterHelper<T> : IValueHelper<T>
	{
		private readonly object		LokObject			= new object();

		public Func<T>				Getter				{ get; set; }
		private T					CurrentValue;
		public T					Value				{ get { return CheckValue(); } set { throw new NotImplementedException("GetterHelper.set_Value()"); } }
		public event Action			ValueChanged;
		public bool					Available			{ get { var v = CheckValue(); return (v == null); } }
		public event Action			AvailableChanged;
		public bool					NoTrigger			{ get; set; }

		public GetterHelper(T initialValue=default(T))
		{
			CurrentValue = initialValue;
		}

		public static implicit operator T(GetterHelper<T> self)
		{
			return self.Value;
		}

		public T CheckValue()
		{
			System.Diagnostics.Debug.Assert( Getter != null, "Property 'Getter' must be set" );

			T oldValue;
			T newValue;
			lock( LokObject )
			{
				oldValue = CurrentValue;
				newValue = Getter();
				CurrentValue = newValue;
			}

			if( NoTrigger )
				return newValue;

			var wasAvailable = (oldValue != null);
			var isAvailable = (newValue != null);

			var availableChanged = (wasAvailable != isAvailable);
			bool valueChanged;
			if( availableChanged )
			{
				valueChanged = true;
			}
			else if(! isAvailable )
			{
				CommonLibs.Utils.Debug.ASSERT( !wasAvailable, this, "If 'availableChanged == false' and 'isAvailable == false', then 'wasAvailable' is supposed to be 'false' too" );
				// Was and is 'null'
				valueChanged = false;
			}
			else
			{
				valueChanged = (! newValue.Equals( oldValue ) );
			}

			if( availableChanged && (AvailableChanged != null) )
				E.E( AvailableChanged );

			if( valueChanged && (ValueChanged != null) )
				E.E( ValueChanged );

			return newValue;
		}

		public TriggerHolder<GetterHelper<T>> NewHolder()
		{
			return new TriggerHolder<GetterHelper<T>>( this );
		}
	}
}
