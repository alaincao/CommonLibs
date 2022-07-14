//
// CommonLibs/ExceptionManager/BaseException.cs
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
