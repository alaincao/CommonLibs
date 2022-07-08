//
// CommonLibs/Utils/Event/ConnectionStringHelper.cs
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
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace CommonLibs.Utils.Event
{
// TODO: Alain: Voir System.Data.SqlClient.SqlConnectionStringBuilder
	public class ConnectionStringHelper
	{
		public static string				SampleConnectionString		{ get; set; } = "Server='localhost';Database='northwind';User ID='sa';Password='secret'";
		//public static string				SampleConnectionString		{ get; set; } = "Data source='localhost';Initial Catalog='northwind';UID='sa';PWD='secret'"

		private const string				KEY_SERVER1					= "Server";
		private const string				KEY_SERVER2					= "Data Source";
		private const string				KEY_DATABASE1				= "Database";
		private const string				KEY_DATABASE2				= "Initial Catalog";
		private const string				KEY_USER1					= "User ID";
		private const string				KEY_USER2					= "UID";
		private const string				KEY_PASSWORD1				= "Password";
		private const string				KEY_PASSWORD2				= "PWD";

		private const string				KEY_SERVER1_UPPER			= "SERVER";
		private const string				KEY_SERVER2_UPPER			= "DATA SOURCE";
		private const string				KEY_DATABASE1_UPPER			= "DATABASE";
		private const string				KEY_DATABASE2_UPPER			= "INITIAL CATALOG";
		private const string				KEY_USER1_UPPER				= "USER ID";
		private const string				KEY_USER2_UPPER				= "UID";
		private const string				KEY_PASSWORD1_UPPER			= "PASSWORD";
		private const string				KEY_PASSWORD2_UPPER			= "PWD";

		private static readonly string[]	KeysServer					= new string[] { KEY_SERVER1, KEY_SERVER2 };
		private static readonly string[]	KeysDatabase				= new string[] { KEY_DATABASE1, KEY_DATABASE2 };
		private static readonly string[]	KeysUser					= new string[] { KEY_USER1, KEY_USER2 };
		private static readonly string[]	KeysPassword				= new string[] { KEY_PASSWORD1, KEY_PASSWORD2 };

		public string						Value						{ get { return ValueHelper.Value; } set { ValueHelper.Value = value; } }
		public IValueHelper<string>			ValueHelper					{ get; private set; }

		public string						Server						{ get { return ServerHelper.Value; }	set { ServerHelper.Value = value; } }
		public ValueHelper<string>			ServerHelper				{ get; private set; }
		public string						Database					{ get { return DatabaseHelper.Value; }	set { DatabaseHelper.Value = value; } }
		public ValueHelper<string>			DatabaseHelper				{ get; private set; }
		public string						User						{ get { return UserHelper.Value; }		set { UserHelper.Value = value; } }
		public ValueHelper<string>			UserHelper					{ get; private set; }
		public string						Password					{ get { return PasswordHelper.Value; }	set { PasswordHelper.Value = value; } }
		public ValueHelper<string>			PasswordHelper				{ get; private set; }

		public ConnectionStringHelper(IValueHelper<string> valueHelper)
		{
			ValueHelper = valueHelper;

			ServerHelper = new ValueHelper<string>();
			DatabaseHelper = new ValueHelper<string>();
			UserHelper = new ValueHelper<string>();
			PasswordHelper = new ValueHelper<string>();

			ValueHelper.ValueChanged += ParseConnectionString;
			ParseConnectionString();

			ServerHelper.ValueChanged += ()=> { SetValue( KeysServer, ServerHelper.Value ); };
			DatabaseHelper.ValueChanged += ()=> { SetValue( KeysDatabase, DatabaseHelper.Value ); };
			UserHelper.ValueChanged += ()=> { SetValue( KeysUser, UserHelper.Value ); };
			PasswordHelper.ValueChanged += ()=> { SetValue( KeysPassword, PasswordHelper.Value ); };
		}

		public string GetConnectionStringWithoutPassword(string replacementString)
		{
			// Split the current connection string
			var tokens = GetTokens( ValueHelper.Value );

			// Update the tokens with the new value
			var keysPassword = KeysPassword.Select( v=>v.ToUpper() ).ToArray();
			foreach( var pair in tokens )
			{
				if( keysPassword.Contains(pair[0].ToUpper()) )
				{
					pair[ 1 ] = replacementString;
					break;
				}
			}

			// Recreated the connection string
			var a = new string[ tokens.Length ];
			for( int i=0; i<a.Length; ++i )
				a[ i ] = tokens[i][0] + "='" + tokens[i][1] + "'";
			string newConnectionString = string.Join( ";", a );
			return newConnectionString;
		}

		private static string[][] GetTokens(string connectionString)
		{
			string[] tokens = connectionString.Split( new char[]{';'} );

			var rc = new List<string[]>( tokens.Length );
			for(int i=0; i<tokens.Length; ++i )
			{
				if( string.IsNullOrEmpty(tokens[i]) )
					continue;
				var keyValue = tokens[i].Split(new char[]{'='});
				if( keyValue.Length != 2 )
					throw new ArgumentException( "Connection string parse error at token '" + tokens[i] + "'" );

				string key = keyValue[0];
				key = key.Trim();

				string value = keyValue[1];
				value = value.Trim();
				if( value.Length >= 2 )
					if( (value.StartsWith("\"") && value.EndsWith("\""))
					 || (value.StartsWith("'") && value.EndsWith("'")) )
						value = value.Substring( 1, value.Length-2 );

				rc.Add( new string[]{ key, value } );
			}
			return rc.ToArray();
		}

		private void ParseConnectionString()
		{
			string strConnectionString = ValueHelper.Value;
			if( strConnectionString == null )
			{
				Server		= null;
				Database	= null;
				User		= null;
				Password	= null;
				return;
			}

			var tokens = GetTokens( strConnectionString );
			bool serverPresent = false;
			bool databasePresent = false;
			bool userPresent = false;
			bool passwordPresent = false;
			foreach( var pair in tokens )
			{
				switch( pair[0].ToUpper() )
				{
					case KEY_SERVER1_UPPER:
					case KEY_SERVER2_UPPER:
						Server = pair[1];
						serverPresent = true;
						break;
					case KEY_DATABASE1_UPPER:
					case KEY_DATABASE2_UPPER:
						Database = pair[1];
						databasePresent = true;
						break;
					case KEY_USER1_UPPER:
					case KEY_USER2_UPPER:
						User = pair[1];
						userPresent = true;
						break;
					case KEY_PASSWORD1_UPPER:
					case KEY_PASSWORD2_UPPER:
						Password = pair[1];
						passwordPresent = true;
						break;
				}
			}
			if(! serverPresent )
				Server = null;
			if(! databasePresent )
				Database = null;
			if(! userPresent )
				User = null;
			if(! passwordPresent )
				Password = null;
		}

		/// <param name="keys">A list of possible keys. Must contain at least 1 value. The first being the default one.</param>
		private void SetValue(string[] keys, string newValue)
		{
// TODO: Alain: Support for setting 'null' that will remove the token from the ConnectionString
			System.Diagnostics.Debug.Assert( (keys != null) && (keys.Length > 0), "Parameter 'keys' must contain at least 1 value" );
			var keysUpper = keys.Select( v=>v.ToUpper() ).ToArray();

			// Split the current connection string
			var tokens = GetTokens( ValueHelper.Value );

			// Update the tokens with the new value
			bool found = false;
			foreach( var pair in tokens )
			{
				if( keysUpper.Contains(pair[0].ToUpper()) )
				{
					pair[ 1 ] = newValue;
					found = true;
					break;
				}
			}

			// If the key was not found, add it
			if(! found )
			{
				var newTokens = new string[ tokens.Length+1 ][];
				int i;
				for( i=0; i<tokens.Length; ++i )
					newTokens[i] = tokens[i];
				newTokens[ i ] = new string[]{ keys[0], newValue };
				tokens = newTokens;
			}

			// Recreated the connection string
			var a = new string[ tokens.Length ];
			for( int i=0; i<a.Length; ++i )
				a[ i ] = tokens[i][0] + "='" + tokens[i][1] + "'";
			string newConnectionString = string.Join( ";", a );
			ValueHelper.Value = newConnectionString;
		}
	}
}
