using System;

namespace CommonLibs
{
	[Serializable]
	public class CommonException : System.ApplicationException
	{
		public CommonException() : base()  {}
		public CommonException(string message) : base(message)  {}
		public CommonException(string message, Exception innerException)  : base(message, innerException) {}

		protected CommonException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
		{
			throw new NotImplementedException( $"Serialization of '{nameof(CommonException)}' is not implemented" );
		}
	}
}
