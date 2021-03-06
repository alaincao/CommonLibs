﻿//
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
