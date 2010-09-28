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
