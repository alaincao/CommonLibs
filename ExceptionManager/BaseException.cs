//
// CommonLibs/ExceptionManager/BaseException.cs
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
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace CommonLibs.ExceptionManager
{
	public class BaseException : System.ApplicationException
	{
		public TranslatableElement			TranslatableMessage		{ get; private set; }
		public Dictionary<string,object>	ObjectData				{ get; private set; }

		public BaseException() : base()  { Init( null ); }
		public BaseException(string message) : base(message)  { Init( null ); }
		public BaseException(string message, Exception innerException) : base(message, innerException)  { Init( null ); }

		public BaseException(TranslatableElement message) : base(message.TextKey)  { Init( message ); }
		public BaseException(TranslatableElement message, Exception innerException) : base(message.TextKey, innerException)  { Init( message ); }

		private void Init(TranslatableElement message)
		{
			ObjectData = new Dictionary<string,object>();
			TranslatableMessage = message;
		}

		public BaseException AddData(string key, byte value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, char value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, short value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, ushort value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, int value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, uint value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, long value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, ulong value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, Single value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, double value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, string value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, DateTime value)
		{
			Data[key] = value;
			return this;
		}

		public BaseException AddData(string key, object obj)
		{
			if( obj == null )
			{
				Data[key] = obj;
				return this;
			}
			var type = obj.GetType();
			if( ObjectExplorer.IsDirectlyInterpretable(type) )
				Data[key] = obj;
			else
				ObjectData[key] = obj;
			return this;
		}
	}
}
