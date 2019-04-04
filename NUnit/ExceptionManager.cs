//
// CommonLibs/NUnit/ExceptionManager.cs
//
// Author:
//   Alain CAO (alaincao17@gmail.com)
//
// Copyright (c) 2010 - 2013 Alain CAO
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

using NUnit.Framework;

namespace CommonLibs.NUnit
{
	public class TranslationAttribute : Attribute
	{
		public string	FR		{ get; set; }
		public string	EN		{ get; set; }
	}

	public enum TranslationMessages
	{
		[Translation(EN="Creation date", FR="Date de cr�ation")]
		CreationDate,
		[Translation(EN="Message one {0}", FR="Message un {0}")]
		Message1,
		[Translation(EN="Message two {0}, {1}", FR="Message deux {0}, {1}")]
		Message2
	}

	public class TestException : CommonLibs.ExceptionManager.BaseException
	{
		public TestException(TranslationMessages key, params object[] parameters) : base(CreateTranslatedElement(key,parameters))  {}
		public TestException(Exception innerException, TranslationMessages key, params object[] parameters) : base(CreateTranslatedElement(key,parameters), innerException)  {}

		public static CommonLibs.ExceptionManager.TranslatableElement CreateTranslatedElement(TranslationMessages key, params object[] parameters)
		{
			return new CommonLibs.ExceptionManager.TranslatableElement {
										TextKey = key.ToString(),
										Parameters = parameters };
		}

		public static string Translate(string textKey, object[] parameters)
		{
			try
			{
				System.Diagnostics.Debug.Assert( parameters != null );
				var fieldInfo = typeof(TranslationMessages).GetField( textKey );
				var attribute = (TranslationAttribute[])fieldInfo.GetCustomAttributes( typeof(TranslationAttribute), false );
				System.Diagnostics.Debug.Assert( attribute.Length == 1 );
				var msg = string.Format( attribute[0].FR, parameters );
				return msg;
			}
			catch(System.Exception ex)
			{
				return "Error getting translation for key '" + textKey + "': " + ex.Message;
			}
		}
	}

	public class TestManager : CommonLibs.ExceptionManager.Manager
	{
		public TestManager(Exception exception) : base(exception)
		{
			Translate = TestException.Translate;
		}
		public TestManager(CommonLibs.ExceptionManager.ObjectElement tree) : base(tree)
		{
			Translate = TestException.Translate;
		}

		public TestManager AddData(TranslationMessages key, object value, params object[] parameters)
		{
			AddData( TestException.CreateTranslatedElement(key, parameters), value );
			return this;
		}
	}

	[TestFixture()]
	public class ExceptionManager
	{
		[Test()]
		public void TestSimpleException()
		{
			Exception exception;
			try
			{
				ThrowTestException();
				exception = null;  // Unreachable code
			}
			catch( System.Exception ex )
			{
				exception = ex;
			}

			var manager = (TestManager)new TestManager( exception )
								.AddData( TranslationMessages.CreationDate, DateTime.Now )
								.AddData( "Test data", "Hello world" )
								.AddData( "Test null 1", (object)null )
								.AddData( "Test null 2", (string)null );

			using( var writer = new System.IO.StreamWriter(@"C:\Users\be0009\Desktop\a01.txt", false) )
			{
				var sr = new CommonLibs.ExceptionManager.XmlSerializer( manager );
				sr.Write( writer );
			}

			using( var reader = new System.IO.StreamReader(@"C:\Users\be0009\Desktop\a01.txt") )
			{
				manager = (TestManager)CommonLibs.ExceptionManager.XmlSerializer.Read( reader, (tree)=>new TestManager(tree) );
			}

			using( var writer = new System.IO.StreamWriter(@"C:\Users\be0009\Desktop\a02.txt", false) )
			{
				writer.Write( "Message: " + manager.GetMessage() + "\n\n" );
				writer.Write( "Messages:\n- " + string.Join( "\n- ", manager.GetMessages() ) + "\n\n" );

				var sr = new CommonLibs.ExceptionManager.TextWriter( manager );
				sr.Write( writer );
			}
		}

		public static CommonLibs.ExceptionManager.Manager CreateManager()
		{
			try
			{
				ThrowTestException();
				return null;  // Unreachable code
			}
			catch( System.Exception ex )
			{
				var manager = (TestManager)new TestManager( ex )
									.AddData( TranslationMessages.CreationDate, DateTime.Now )
									.AddData( "Test data", "Hello world" )
									.AddData( "Test null 1", (object)null )
									.AddData( "Test null 2", (string)null );
				return manager;
			}
		}

		public static void ThrowTestException()
		{
			try
			{
				foo();
			}
			catch( System.Exception ex )
			{
				var e = new TestException( ex, TranslationMessages.Message2, "Parameter 1", DateTime.Now )
								.AddData( "Console", Console.Out );
				throw e;
			}
		}

		private static  void foo()
		{
			try
			{
				bar();
			}
			catch( System.Exception ex )
			{
				throw new CommonLibs.ExceptionManager.BaseException( "KABOUM!!!", ex )
								.AddData( "Item 1", "Hello world" )
								.AddData( "Item 2", 123.456 )
								.AddData( "Item 3", System.Globalization.CultureInfo.CurrentCulture );
			}
		}

		private static void bar()
		{
			try
			{
				var a = 1;
				var b = 0;
				a = a / b;
			}
			catch( System.Exception ex )
			{
				var e = new AccessViolationException( "Exception bar", ex );
				e.Data.Add( "Test item 1", new string[]{ "AAA","BBB","CCC" } );
				e.Data.Add( "Test item 2", 12345 );
				e.Data.Add( "Test EOL 1", "\n" );
				e.Data.Add( "Test EOL 2", "\r\n" );
				e.Data.Add( "Test EOL 3", "\n\r" );
				e.Data.Add( "Test EOL 4", '\n' );
				e.Data.Add( "Test EOL 5", '\r' );
				e.Data.Add( "Test null", null );

				throw e;
			}
		}
	}
}