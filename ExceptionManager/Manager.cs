//
// CommonLibs/ExceptionManager/Manager.cs
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

namespace CommonLibs.ExceptionManager
{
// TODO: Alain: #define FRAMEWORK_35 ???
	/// <remarks>Defined in Framework 3.5 as System.Func</remarks>
	public delegate TResult Func<in T1,in T2,out TResult>(T1 arg1, T2 arg2);
	/// <remarks>Defined in Framework 3.5 as System.Func</remarks>
	public delegate TResult Func<in T1,out TResult>(T1 arg1);

	public delegate Manager CreateManagerDelegate(Exception exception);

	public class Manager
	{
		public ObjectElement					Tree			{ get; private set; }

		public Func<string,object[],string>		Translate		{ get; set; }

		public static readonly CreateManagerDelegate		DefaultCreateManagerDelegate	= new CreateManagerDelegate( (exception)=>{ return new Manager(exception); } );

		public Manager(Exception exception)
		{
			Translate = Translate_DefaultImplementation;

			var explorer = new ObjectExplorer();
			Tree = explorer.ExploreException( exception );
		}

		public Manager(ObjectElement tree)
		{
			Translate = Translate_DefaultImplementation;

			System.Diagnostics.Debug.Assert( tree != null, "The provided parameter 'tree' is null" );
			System.Diagnostics.Debug.Assert( tree.Type == ObjectElement.Types.Root, "The provided tree is supposed to be a 'Root' element" );
			Tree = tree;
		}

		internal static string Translate_DefaultImplementation(string textKey, object[] parms)
		{
			var parameters = new string[ parms.Length ];
			for( int i=0; i<parameters.Length; ++i )
				parameters[i] = (parms[i] != null) ? parms[i].ToString() : ObjectExplorer.NullValue;
			return "" + textKey + "('" + string.Join( "','", parameters ) + "')";
		}

		/// <returns>The last exception's message</returns>
		public string GetMessage()
		{
			System.Diagnostics.Debug.Assert( Tree != null, "The 'Tree' property sould have been initialized in the constructor" );
			if( (Tree == null) || (Tree.Children.Count <= 0) )
				return "";

			// Get the last exception
			ObjectElement lastException = null;
			foreach( var child in Tree.Children )
				if( child.Type == ObjectElement.Types.Exception )
					lastException = child;
			if( lastException == null )
			{
				System.Diagnostics.Debug.Fail( "No exception in Tree" );
				return "";
			}

			var translatable = lastException.Value as TranslatableElement;
			if( translatable != null )
				return translatable.ToString( this );
			else
				return lastException.Value.ToString();
		}

		/// <returns>The list of exception's messages (the last thrown first)</returns>
		public string[] GetMessages()
		{
			System.Diagnostics.Debug.Assert( Tree != null, "The 'Tree' property sould have been initialized in the constructor" );
			if( (Tree == null) || (Tree.Children.Count <= 0) )
				return new string[] {};

			var messages = new List<string>();
			for(int i=Tree.Children.Count-1; i>=0; --i )
			{
				var children = Tree.Children[ i ];
				if( children.Type == ObjectElement.Types.Exception )
				{
					var message = children.Value;
					var translatable = message as TranslatableElement;
					if( translatable != null )
						messages.Add( translatable.ToString(this) );
					else
						messages.Add( message.ToString() );
				}
			}
			return messages.ToArray();
		}

		public Manager AddData(string key, object value)
		{
			System.Diagnostics.Debug.Assert( Tree != null, "The property 'Tree' is not supposed to be null" );

			// Insert a new field right before the exception list
			var explorer = new ObjectExplorer();
			var element = explorer.ExploreObject( key, value );
			Tree.Children.Insert( GetFirstExceptionIndex(), element );

			return this;
		}

		public Manager AddData(TranslatableElement key, object value)
		{
			System.Diagnostics.Debug.Assert( Tree != null, "The property 'Tree' is not supposed to be null" );

			// Insert a new field right before the exception list
			var explorer = new ObjectExplorer();
			var element = explorer.ExploreObject( key, value );
			Tree.Children.Insert( GetFirstExceptionIndex(), element );

			return this;
		}

		public override string ToString()
		{
			return GetMessage();
		}

		private int GetFirstExceptionIndex()
		{
			System.Diagnostics.Debug.Assert( Tree != null, "The property 'Tree' is not supposed to be null" );

			int idx = 0;
			while( idx < Tree.Children.Count )
			{
				if( Tree.Children[idx].Type == ObjectElement.Types.Exception )
					break;
				++ idx;
			}
			return idx;
		}
	}
}
