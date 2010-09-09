using System;
using System.Collections.Generic;
using System.Text;

namespace CommonLib.Utils
{
	public class ConnectionStringHelper
	{
		private const string				KEY_SERVER					= "SERVER";
		private const string				KEY_DATABASE				= "DATABASE";
		private const string				KEY_USER					= "UID";
		private const string				KEY_PASSWORD				= "PWD";
//private const string				KEY_PROVIDER				= "PROVIDERNAME";
//private const string				VALUE_PROVIDER				= "System.Data.SqlClient";

		public string						Value						{ get { return ValueHelper.Value; } }
		public Event.IValueHelper<string>	ValueHelper					{ get; private set; }

		public string						Server						{ get { return ServerHelper.Value; }	set { ServerHelper.Value = value; } }
		public Event.ValueHelper<string>	ServerHelper				{ get; private set; }
		public string						Database					{ get { return DatabaseHelper.Value; }	set { DatabaseHelper.Value = value; } }
		public Event.ValueHelper<string>	DatabaseHelper				{ get; private set; }
		public string						User						{ get { return UserHelper.Value; }		set { UserHelper.Value = value; } }
		public Event.ValueHelper<string>	UserHelper					{ get; private set; }
		public string						Password					{ get { return PasswordHelper.Value; }	set { PasswordHelper.Value = value; } }
		public Event.ValueHelper<string>	PasswordHelper				{ get; private set; }

		public ConnectionStringHelper(Event.IValueHelper<string> valueHelper)
		{
			ValueHelper = valueHelper;

			ServerHelper = new Event.ValueHelper<string>();
			DatabaseHelper = new Event.ValueHelper<string>();
			UserHelper = new Event.ValueHelper<string>();
			PasswordHelper = new Event.ValueHelper<string>();

			ValueHelper.ValueChanged += ParseConnectionString;
			ParseConnectionString();

			ServerHelper.ValueChanged += ()=> { SetValue( KEY_SERVER, ServerHelper.Value ); };
			DatabaseHelper.ValueChanged += ()=> { SetValue( KEY_DATABASE, DatabaseHelper.Value ); };
			UserHelper.ValueChanged += ()=> { SetValue( KEY_USER, UserHelper.Value ); };
			PasswordHelper.ValueChanged += ()=> { SetValue( KEY_PASSWORD, PasswordHelper.Value ); };
		}

		public string GetConnectionStringWithoutPassword(string replacementString)
		{
			// Split the current connection string
			var tokens = GetTokens( ValueHelper.Value );

			// Update the tokens with the new value
			foreach( var pair in tokens )
			{
				if( pair[0].ToUpper() == KEY_PASSWORD )
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
					case KEY_SERVER:
						Server = pair[1];
						serverPresent = true;
						break;
					case KEY_DATABASE:
						Database = pair[1];
						databasePresent = true;
						break;
					case KEY_USER:
						User = pair[1];
						userPresent = true;
						break;
					case KEY_PASSWORD:
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

		private void SetValue(string key, string newValue)
		{
			// Split the current connection string
			var tokens = GetTokens( ValueHelper.Value );

			// Update the tokens with the new value
			bool found = false;
			foreach( var pair in tokens )
			{
				if( pair[0].ToUpper() == key )
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
				newTokens[ i ] = new string[]{ key, newValue };
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
