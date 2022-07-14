//
// CommonLibs/ExceptionManager/ObjectExplorer.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace CommonLibs.ExceptionManager
{
public class ObjectExplorer
	{
		private const string	ExploreError		= "Error exploring '{0}': {1}";
		private const string	GetterError			= "Error '{0}': {1}";
		internal const string	NullValue			= "<NULL>";

		private Dictionary<object, ObjectElement>		VisitedObjects		= null;

public const int		MaximumDepth		= 5;
		internal const int		MaxDictKeyLen		= 50;

		internal ObjectExplorer()
		{
		}

		internal ObjectElement ExploreObject(TranslatableElement fieldName, object obj)
		{
			// Create the element then replace its Name
			var element = ExploreObject( "", obj );
			element.Name = fieldName;
			return element;
		}

		/// <returns>An <see cref="ObjectElement"/> of type 'Field'</returns>
		internal ObjectElement ExploreObject(string fieldName, object obj)
		{
			System.Diagnostics.Debug.Assert( VisitedObjects == null, "VisitedObjects should be reset at the exit of this method" );
			try
			{
				VisitedObjects = new Dictionary<object, ObjectElement>();

				string fieldType;
				if( obj == null )
					fieldType = NullValue;
				else
					fieldType = obj.GetType().FullName;
				var element = Explore( fieldName, fieldType, obj, 0 );
				return element;
			}
			finally
			{
				if( VisitedObjects != null )
					VisitedObjects = null;
			}
		}

		/// <returns>An <see cref="ObjectElement"/> of type 'Root'</returns>
		internal ObjectElement ExploreException(Exception exception)
		{
			System.Diagnostics.Debug.Assert( VisitedObjects == null, "VisitedObjects should be reset at the exit of this method" );
			try
			{
				// Create root
				VisitedObjects = new Dictionary<object, ObjectElement>();
				var root = ObjectElement.CreateRoot();

				// Create element for each InnerException
				for( var current=exception; current != null; current = current.InnerException )
				{
					var element = Explore( "", typeof(Exception).ToString(), current, 0 );
					root.Children.Insert( 0, element );
				}

				return root;
			}
			finally
			{
				if( VisitedObjects != null )
					VisitedObjects = null;
			}
		}

		private ObjectElement Explore(string fieldName, string fieldType, object currentObject, int currentDepth)
		{
			try
			{
				System.Diagnostics.Debug.Assert( fieldName != null );
				System.Diagnostics.Debug.Assert( fieldType != null );
				System.Diagnostics.Debug.Assert( currentDepth <= MaximumDepth );
				System.Diagnostics.Debug.Assert( VisitedObjects != null );

				if( currentObject == null )
					return ObjectElement.CreateField( fieldName, fieldType, NullValue );

				// Check that this object has not been visited yet
				ObjectElement visited;
				VisitedObjects.TryGetValue( currentObject, out visited );
				if( visited != null )
					return ObjectElement.CloneElement( fieldName, fieldType, visited );

				// Create the ObjectElement for this object and register it as visited
				ObjectElement currentElement;
				Type currentType = currentObject.GetType();
				BaseException currentBaseException = null;
				Exception currentException = null;
				IDictionary currentDictionary = null;
				IEnumerable currentEnumerable = null;
				var fieldsToSkip = new Dictionary<string,object>();
				string[] stackTrace = null;
				bool exploreChildren;
				if( IsDirectlyInterpretable(currentType) )
				{
					exploreChildren = false;
					currentElement = ObjectElement.CreateField( fieldName, currentType.FullName, currentObject );
				}
				else
				{
					exploreChildren = true;

					if( (currentBaseException = currentObject as BaseException) != null )
					{
						currentException = currentBaseException;
						if( currentBaseException.TranslatableMessage == null )
							// Treat as regular exception
							currentBaseException = null;

						fieldsToSkip.Add( "InnerException", null );
						fieldsToSkip.Add( "Message", null );
						fieldsToSkip.Add( "TranslatableMessage", null );
						fieldsToSkip.Add( "StackTrace", null );
						stackTrace = currentException.StackTrace.Split( new char[]{'\n'} );
						for( int i=0; i<stackTrace.Length; ++i )
							stackTrace[i] = stackTrace[i].Trim();
					}
					else if( (currentException = currentObject as Exception) != null )
					{
						fieldsToSkip.Add( "InnerException", null );
						fieldsToSkip.Add( "Message", null );
						fieldsToSkip.Add( "StackTrace", null );
						stackTrace = currentException.StackTrace.Split( new char[]{'\n'} );
						for( int i=0; i<stackTrace.Length; ++i )
							stackTrace[i] = stackTrace[i].Trim();
					}
					if( (currentDictionary = currentObject as IDictionary) != null )
					{
						fieldsToSkip.Add( "Keys", null );
						fieldsToSkip.Add( "Values", null );
						fieldsToSkip.Add( "Item", null );
						//exploreChildren = false
					}
					else if( (currentEnumerable = currentObject as IEnumerable) != null )
					{
						//exploreChildren = false
					}

					if( currentBaseException != null )
						currentElement = ObjectElement.CreateException( currentBaseException.TranslatableMessage, currentBaseException.GetType().FullName, stackTrace );
					else if( currentException != null )
						currentElement = ObjectElement.CreateException( currentException.Message, currentException.GetType().FullName, stackTrace );
					else
						currentElement = ObjectElement.CreateObject( fieldName, currentObject.GetType().FullName );
				}
				if( currentDepth >= MaximumDepth )
					// Stop exploring 
					exploreChildren = false;
				VisitedObjects.Add( currentObject, currentElement );

				var childDepth = currentDepth + 1;
				if( (currentException == null) && (currentEnumerable == null) && (currentDictionary == null) && currentType.FullName.StartsWith("System.") )
					// Don't explore childs of objects of type 'System.*' (except exceptions, enumerables and dictionaries)
					childDepth = MaximumDepth;
				if( exploreChildren )
				{
					// Explore children
					foreach( var childMember in currentObject.GetType().GetMembers() )
					{
						if( fieldsToSkip.ContainsKey(childMember.Name) )
							continue;

						// Get the child type/value
						string childType;
						object childObject;
						switch( childMember.MemberType )
						{
							case MemberTypes.Field: {
								var field = (FieldInfo)childMember;
								childType = field.FieldType.FullName;
								childObject = field.GetValue( currentObject );
								break; }

							case MemberTypes.Property: {
								var property = (PropertyInfo)childMember;
								var propertyType = property.PropertyType;
								childType = propertyType.FullName;
								try
								{
									childObject = property.GetGetMethod().Invoke( currentObject, new object[]{} );
								}
								catch( System.Exception ex )
								{
									// Error calling the getter => Replace by an error string and continue
									string errorString = string.Format( GetterError, ex.GetType().FullName, ex.Message );
									var childElement2 = ObjectElement.CreateField( childMember.Name, childType, errorString );
									currentElement.Children.Add( childElement2 );
									continue;
								}
								break; }

							default:
								// Nothing to explore
								continue;
						}

						// Recursive call
						var childElement = Explore( childMember.Name, childType, childObject, childDepth );
						currentElement.Children.Add( childElement );
					}

					if( currentDictionary != null )
					{
						// Add dictionary entries
						foreach( DictionaryEntry entry in currentDictionary )
						{
							string name;
							try
							{
								object key = entry.Key;
								if( key == null )
								{
									name = "<NULL>";
								}
								else
								{
									name = key.ToString();
									if( name.Length > MaxDictKeyLen )
										name = name.Substring( 0, MaxDictKeyLen );
									name = "[" + name + "]";
								}
							}
							catch( System.Exception ex )
							{
								// Error converting the Key to string => Replace by an error string and continue
								name = string.Format( GetterError, ex.GetType().FullName, ex.Message );
							}
							// Recursive call
							var childElement = Explore( name, typeof(object).ToString(), entry.Value, childDepth );
							currentElement.Children.Add( childElement );
						}
					}
					else if( currentEnumerable != null )
					{
						// Add enumerable entries
						int i=0;
						foreach( object entry in currentEnumerable )
						{
							// Recursive call
							string name = string.Format( "[{0}]", i++ );
							var childElement = Explore( name, typeof(object).ToString(), entry, childDepth );
							currentElement.Children.Add( childElement );
						}
					}
				}

				return currentElement;
			}
			catch( System.Exception ex )
			{
				// Error exploring => Replace by an error field
				System.Diagnostics.Debug.Fail( "Explore error" );
				var element = ObjectElement.CreateField( fieldName, fieldType, string.Format(ExploreError, ex.GetType(), ex.Message) );
				return element;
			}
		}

		internal static bool IsDirectlyInterpretable(Type type)
		{
			System.Diagnostics.Debug.Assert( type != null, "'type' is not supposed to be null." );
			return	type.IsPrimitive
				|| (type == typeof(string))
				|| (type == typeof(DateTime));
		}
	}
}
