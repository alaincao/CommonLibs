//
// CommonLibs/Utils/Event/PropertyHelper.cs
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
