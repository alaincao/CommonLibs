using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using CommonLibs.Utils.Event;
using CommonLibs.WPF.SetupGui.Actions;

namespace CommonLibs.WPF.SetupGui.UserControls
{
	using ES=CommonLibs.WPF.ExceptionShield;

	public partial class SqlConnectionString : UserControl
	{
// TODO: Alain: Cache all connection tries (with a way (a button) to clear this cache)
// ConnectionString->The action entry used (to allow tracking changes if not yet finished)
//Dictionary<string,ActionEntry> ConnectionTriesCache

		private string						Creator						= null;
		private ActionsManager				ActionsManager;

		/// <remarks>Must be set after Init()</remarks>
		public string						ConnectionString			{ get { return ConnectionStringHelper.Value; }		set { ConnectionStringHelper.Value = value; } }
		public ConnectionStringHelper		ConnectionStringHelper		{ get; private set; }
		/// <remarks>Must be set after Init()</remarks>
		public string						Server						{ get { return ConnectionStringHelper.Server; }		set { ConnectionStringHelper.Server = value; } }
		/// <remarks>Must be set after Init()</remarks>
		public string						Database					{ get { return ConnectionStringHelper.Database; }	set { ConnectionStringHelper.Database = value; } }
		/// <remarks>Must be set after Init()</remarks>
		public string						User						{ get { return ConnectionStringHelper.User; }		set { ConnectionStringHelper.User = value; } }
		/// <remarks>Must be set after Init()</remarks>
		public string						Password					{ get { return ConnectionStringHelper.Password; }	set { ConnectionStringHelper.Password = value; } }
		private const string				PasswordHideString			= "XXX";
		public bool							IsConnectionOk				{ get { return IsConnectionOkHelper.Value; } private set { IsConnectionOkHelper.Value = value; } }
		public ValueHelper<bool>			IsConnectionOkHelper		{ get; private set; }

		/// <summary>
		/// Set this callback if an additional test must be executed on the query to declare the connection attempt successful.
		/// </summary>
		public Action<ActionEntry,SqlConnection> AdditionalTest			= null;

		/// <summary>
		/// Callback to use to create instances of CommonLibs.ExceptionManager.Manager
		/// </summary>
		public CommonLibs.ExceptionManager.CreateManagerDelegate	ExceptionManagerFactory		{	get { return imgAction.ExceptionManagerFactory; }
																									set { imgAction.ExceptionManagerFactory = value; } }

		private ActionHelper				ConnectionString_ValueChangedActionDelayer	= null;
		private ActionEntry					CurrentConnectionAction		= null;

		public SqlConnectionString()
		{
			InitializeComponent();
		}

		public void Init(ActionsManager actionsManager, string creator)
		{
			Init( actionsManager, creator, new ValueHelper<string>() );
		}

		public void Init(ActionsManager actionsManager, string creator, IValueHelper<string> connectionStringValueHelper)
		{
			System.Diagnostics.Debug.Assert( Creator == null, "Init() should be called only once!" );

			Creator = creator;
			ActionsManager = actionsManager;
			IsConnectionOkHelper = new ValueHelper<bool>{ Value = false };
			ConnectionString_ValueChangedActionDelayer = new ActionHelper();

			bool hadInitialConnectionString;
			if( string.IsNullOrEmpty(connectionStringValueHelper.Value) )
			{
				// Fill control with initial values
				connectionStringValueHelper.Value = ConnectionStringHelper.SampleConnectionString;
				hadInitialConnectionString = false;
			}
			else
			{
				hadInitialConnectionString = true;
			}

			ConnectionStringHelper = new Utils.Event.ConnectionStringHelper( connectionStringValueHelper );

			// Link Show Password CheckBox
			cbShowPassword.Checked += ES.Routed( cbShowPassword_Changed );
			cbShowPassword.Unchecked += ES.Routed( cbShowPassword_Changed );
			cbShowPassword_Changed();

			// Link TextBoxes to ConnectionStringHelper

			txtServer.Text = Server;
			ConnectionStringHelper.ServerHelper.ValueChanged += ()=>{ txtServer.Text = Server; };
			txtServer.TextChanged += ES.TextChanged( ()=>{ Server = txtServer.Text; } );

			txtDatabase.Text = Database;
			ConnectionStringHelper.DatabaseHelper.ValueChanged += ()=>{ txtDatabase.Text = Database; };
			txtDatabase.TextChanged += ES.TextChanged( ()=>{ Database = txtDatabase.Text; } );

			txtUser.Text = User;
			ConnectionStringHelper.UserHelper.ValueChanged += ()=>{ txtUser.Text = User; };
			txtUser.TextChanged += ES.TextChanged( ()=>{ User = txtUser.Text; } );

			txtPasswordClear.Text =
			txtPasswordHidden.Password =
			txtPasswordConfirm.Password = Password;
			ConnectionStringHelper.PasswordHelper.ValueChanged += ()=>
				{
					var password = Password;
					txtPasswordClear.Text = password;
					if( txtPasswordHidden.Password != password )	txtPasswordHidden.Password = password;
					if( txtPasswordConfirm.Password != password )	txtPasswordConfirm.Password = password;
				};
			txtPasswordClear.TextChanged += (sender,e)=>{ Password = txtPasswordClear.Text; };
			txtPasswordHidden.PasswordChanged += ES.Routed( txtPasswordHiddenConfirm_PasswordChanged );
			txtPasswordConfirm.PasswordChanged += ES.Routed( txtPasswordHiddenConfirm_PasswordChanged );

			// Monitor ConnectionString changes

			txtConnectionString.Text = ConnectionStringHelper.GetConnectionStringWithoutPassword( PasswordHideString );
			ConnectionString_ValueChangedActionDelayer.Action = ConnectionString_ValueChangedDelayed;
			ConnectionStringHelper.ValueHelper.ValueChanged += ConnectionString_ValueChanged;

			if( hadInitialConnectionString )
				// Try connection right now if a ConnectionString is already available
				ConnectionString_ValueChangedActionDelayer.Trigger();
			// Set delay on the ConnectionString's ActionHelper
			ConnectionString_ValueChangedActionDelayer.DelaySeconds = 1;
		}

		#region ConnectionString value monitor

		/// <summary>
		/// 1. Something in the textboxes has changed.
		/// </summary>
		private void ConnectionString_ValueChanged()
		{
			if( CurrentConnectionAction != null )
			{
				// There is a previous connection attempt => Stop monitoring its status
				CurrentConnectionAction.StatusHelper.ValueChanged -= CurrentConnectionActionStatus_ValueChanged;
				CurrentConnectionAction.Abort();
				CurrentConnectionAction = null;
			}

			txtConnectionString.Text = ConnectionStringHelper.GetConnectionStringWithoutPassword( PasswordHideString );
			imgAction.ActionEntry = null;
			IsConnectionOk = false;

			// Trigger the action delayer
			ConnectionString_ValueChangedActionDelayer.Trigger();
		}

		/// <summary>
		/// 2. After a certain time without inactivities in the checkbox, the action delayer wakes up.
		/// </summary>
		private void ConnectionString_ValueChangedDelayed()
		{
			// Create a new ActionEntry to try connection
			CurrentConnectionAction = ActionsManager.PushAction( Creator, "Connection attempt to '" + Server + "'", TryConnectAction, true );
			imgAction.ActionEntry = CurrentConnectionAction;

			// Monitor this action's status
			CurrentConnectionAction.StatusHelper.ValueChanged += CurrentConnectionActionStatus_ValueChanged;
			CurrentConnectionActionStatus_ValueChanged();
		}

		/// <summary>
		/// 3. The connection attempt's Action have changed its status.
		/// </summary>
		private void CurrentConnectionActionStatus_ValueChanged()
		{
			System.Diagnostics.Debug.Assert( CurrentConnectionAction != null, "'CurrentConnectionAction' is supposed to be set here" );

			IsConnectionOk = CurrentConnectionAction.Success;
		}

		#endregion

		private void txtPasswordHiddenConfirm_PasswordChanged()
		{
			if( txtPasswordConfirm.Password == txtPasswordHidden.Password )
			{
				Password = txtPasswordHidden.Password;

				txtPasswordHidden.Background =
				txtPasswordConfirm.Background = txtPasswordClear.Background;
				imgAction.Visibility = System.Windows.Visibility.Visible;
			}
			else
			{
				txtPasswordHidden.Background = Brushes.Red;
				txtPasswordConfirm.Background = Brushes.Red;
				imgAction.Visibility = System.Windows.Visibility.Hidden;
			}
		}

		private void cbShowPassword_Changed()
		{
			if( cbShowPassword.IsChecked.Value )
			{
				txtPasswordClear.Visibility = System.Windows.Visibility.Visible;
				txtPasswordHidden.Visibility = System.Windows.Visibility.Hidden;
				rowPasswordConfirm.Height = new GridLength( 0 );
			}
			else
			{
				txtPasswordClear.Visibility = System.Windows.Visibility.Hidden;
				txtPasswordHidden.Visibility = System.Windows.Visibility.Visible;
				rowPasswordConfirm.Height = GridLength.Auto;
			}
		}

		private void TryConnectAction(ActionEntry entry)
		{
			entry.LogLine( "Trying to connect using connection string: " + ConnectionStringHelper.GetConnectionStringWithoutPassword(PasswordHideString), 50 );
			using( var connection = new SqlConnection(ConnectionString) )
			{
				connection.Open();

				entry.LogLine( "Executing command 'SELECT 1'", ((AdditionalTest == null) ? 90 : 70) );
				using( var command = connection.CreateCommand() )
				{
					command.CommandText = "SELECT 1";
					command.ExecuteNonQuery();
				}

				if( AdditionalTest != null )
					AdditionalTest( entry, connection );
			}
		}
	}
}
