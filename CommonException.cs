﻿//
// CommonLibs/CommonException.cs
//
// Author:
//   Alain CAO (acao@prophix.com)
//
// Copyright (c) 2022 Alain CAO
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
