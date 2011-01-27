using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.DirectoryServices;

using CommonLibs.Utils.Event;
using CommonLibs.WPF.SetupGui.Actions;

// TODO: Alain: Be sure this works without the "IIS6 management compatibility" things on Vista/7/2008

namespace CommonLibs.WPF.SetupGui.UserControls
{
	using ES=CommonLibs.WPF.ExceptionShield;

	public partial class IISWebApplication : UserControl
	{
		private string						Creator						= null;
		private ActionsManager				ActionsManager;
		private string						WebAppFriendlyName			= null;

		/// <summary>
		/// The physical path of the website. This property is only available when an actual website directory has been selected.
		/// </summary>
		public string						PhysicalPath				{ get { return PhysicalPathHelper.Value; } private set { PhysicalPathHelper.Value = value; } }
		public ValueHelper<string>			PhysicalPathHelper			{ get; private set; }
		private ActionHelper				txtPhysicalPath_Delayer		= new ActionHelper();
		private ActionEntry					CheckPhysicalPathEntry		= null;

		// IIS virtual directory parameters:
		public bool							IISEnableDirBrowsing		{ get; set; }
		public bool							IISAccessRead				{ get; set; }
		public bool							IISAccessExecute			{ get; set; }
		public bool							IISAccessWrite				{ get; set; }
		public bool							IISAccessScript				{ get; set; }
		public string						IISDefaultDoc				{ get; set; }

		/// <summary>
		/// The content of the "Physical Path" TextBox.
		/// </summary>
		public string						PhysicalPathProposition		{ get { return txtPhysicalPath.Text; } set { txtPhysicalPath.Text = value; } }

		public IISWebApplication()
		{
			PhysicalPathHelper = new ValueHelper<string>();
			IISEnableDirBrowsing = true;
			IISAccessRead = true;
			IISAccessExecute = true;
			IISAccessWrite = true;
			IISAccessScript = true;
			IISDefaultDoc = "index.aspx,index.html,index.htm,default.aspx";

			InitializeComponent();
		}

		public void Init(ActionsManager actionsManager, string creator, string webAppFriendlyName)
		{
			System.Diagnostics.Debug.Assert( Creator == null, "Init() should be called only once!" );

			Creator = creator;
			ActionsManager = actionsManager;
			WebAppFriendlyName = webAppFriendlyName;

			Action checkPhysicalPath = ()=>CheckPhysicalPathEntry = ActionsManager.AppendAction( Creator, "Checking physical path", CheckPhysicalPath );
			Action loadWebsites = ()=>ActionsManager.AppendAction( Creator, "Load website list", LoadWebsites );
			Action loadVirtualDirectories = ()=>ActionsManager.AppendAction( Creator, "Load IIS virtual directories list", LoadVirtualDirectories );

			// Launch initialization actions
			loadWebsites();  // Will append action LoadVirtualDirectories
			checkPhysicalPath();

			// Bind controls

			btnBrowsePath.Click += ES.Routed( btnBrowsePath_Click );

			txtPhysicalPath.TextChanged += ES.TextChanged( ()=>
				{
					imgStatus.Source = CommonLibs.Resources.WPF_SetupGui_UserControls_IISWebApplication.ImageRunning;
					txtPhysicalPath_Delayer.Trigger();
				} );
			txtPhysicalPath_Delayer.Action = checkPhysicalPath;
			txtPhysicalPath_Delayer.DelaySeconds = 1;

			PhysicalPathHelper.ValueChanged += ES.Action( loadVirtualDirectories );
			ddWebSites.SelectionChanged += ES.SelectionChanged( loadVirtualDirectories );
			//ddVirtualDirs.TextChanged += ddVirtualDirs_TextChanged;  <= NB: Done in XAML

			btnReplace.Click += ES.Routed( ()=>ActionsManager.AppendAction(Creator, "Replacing the existing virtual directory", btnReplace_Click) );
			btnCreate.Click += ES.Routed( ()=>ActionsManager.AppendAction(Creator, "Creating a new virtual directory", btnCreate_Click) );
			btnRemove.Click += ES.Routed( ()=>ActionsManager.AppendAction(Creator, "Remove existing virtual directory", btnRemove_Click) );
		}

		private void ddVirtualDirs_TextChanged( object sender, TextChangedEventArgs e )
		{
			ES.E( CheckStatus );
		}

		private void btnBrowsePath_Click()
		{
			var fd = new Microsoft.Win32.OpenFileDialog{ Filter="web.config|web.config", CheckFileExists=true };
			bool? rc = fd.ShowDialog();
			if( rc == true )
				txtPhysicalPath.Text = (new FileInfo(fd.FileName)).Directory.FullName;
		}

		/// <summary>
		/// Check the content of 'txtPhysicalPath' and update 'PhysicalPath'.
		/// </summary>
		private void CheckPhysicalPath(ActionEntry entry)
		{
			string realPath = null;
			try
			{
				var proposition = PhysicalPathProposition;
				if( string.IsNullOrEmpty(proposition) )
				{
					entry.AddError( "No physical path specified" );
					goto ExitProc;
				}
				entry.LogLine( "Checking directory '" + proposition );
				var dirInfo = new DirectoryInfo( proposition );
				if(! dirInfo.Exists )
				{
					entry.AddError( "Directory '" + proposition + "' does not exists" );
					goto ExitProc;
				}
				var fileInfo = new FileInfo( System.IO.Path.Combine(proposition, "Web.config") );
				if(! fileInfo.Exists )
				{
					entry.AddError( "Directory '" + proposition + "' does not contain a file 'Web.config'" );
					goto ExitProc;
				}

				// Directory OK => We can use it
				realPath = proposition;;
			}
			catch( System.Exception ex )
			{
				entry.AddException( ex );
			}

		ExitProc:
			PhysicalPath = realPath;
			CheckStatus();
		}

		private void CheckStatus()
		{
			var physicalPath = PhysicalPath;
			bool physicalPathIsAvailable = (! string.IsNullOrEmpty(physicalPath) );
			bool webSiteIsAvailable = (ddWebSites.SelectedValue != null);
			bool virtualDirIsAvailable = (! string.IsNullOrEmpty(ddVirtualDirs.Text) );

			bool virtualDirMatchesPhysicalPath;
			bool aVirtualDirIsSelected;
			{
				var selectedVirtualDirItem = (ComboBoxItem)ddVirtualDirs.SelectedValue;
				if( selectedVirtualDirItem != null )
				{
					if( ((string)selectedVirtualDirItem.Content).ToLower() != ddVirtualDirs.Text.ToLower() )
						// There is a selected item but it does not match the (manually entered) text in the ComboBox
						// => Ignore the selected item
						selectedVirtualDirItem = null;
				}
				aVirtualDirIsSelected = (selectedVirtualDirItem != null);

				if( (!physicalPathIsAvailable) || (!virtualDirIsAvailable) || (!aVirtualDirIsSelected) )
				{
					// Either pysical or virtual path is missing
					virtualDirMatchesPhysicalPath = false;
				}
				else
				{
					var virtualDirEntry = new DirectoryEntry( (string)selectedVirtualDirItem.Tag );
					var virtualDirPhysicalPath = ((string)virtualDirEntry.Properties["Path"].Value) ?? "";
					if( physicalPath.ToLower() == virtualDirPhysicalPath.ToLower() )
						virtualDirMatchesPhysicalPath = true;
					else
						virtualDirMatchesPhysicalPath = false;
				}
			}

			bool canCreate;
			bool canRemove;
			bool canReplace;
			BitmapImage image;
			if( virtualDirMatchesPhysicalPath )
			{
				// Everything seems OK
				image = CommonLibs.Resources.WPF_SetupGui_UserControls_IISWebApplication.ImageSuccess;
				canCreate = false;
				canRemove = true;
				canReplace = true;
			}
			else if( (!webSiteIsAvailable) || (!virtualDirIsAvailable) || (!physicalPathIsAvailable) )
			{
				// No physical path entered or no web site selected or no virtual dir entered/chosen
				image = CommonLibs.Resources.WPF_SetupGui_UserControls_IISWebApplication.ImageError;
				canCreate = false;
				canRemove = aVirtualDirIsSelected;
				canReplace = false;
			}
			else if( aVirtualDirIsSelected )
			{
				// A virtual dir has been selected but it does not matches the physical path
				image = CommonLibs.Resources.WPF_SetupGui_UserControls_IISWebApplication.ImageError;
				canCreate = false;
				canRemove = true;
				canReplace = true;
			}
			else
			{
				// A custom virtual dir has been entered
				image = CommonLibs.Resources.WPF_SetupGui_UserControls_IISWebApplication.ImageError;
				canCreate = true;
				canRemove = false;
				canReplace = false;
			}

			btnCreate.IsEnabled = canCreate;
			btnRemove.IsEnabled = canRemove;
			btnReplace.IsEnabled = canReplace;
			if(! physicalPathIsAvailable )
			{
				// txtPhysicalPath is not valid => The status image shows the CheckPhysicalPath()'s ActionEntry
				imgStatus.ActionEntry = CheckPhysicalPathEntry;
			}
			else
			{
				// Manage the image status ourself
				imgStatus.ActionEntry = null;
				imgStatus.Source = image;
			}
		}

		private void LoadWebsites(ActionEntry entry)
		{
			ddWebSites.Items.Clear();

			entry.LogLine( "Accessing active directory entry 'IIS://localhost/W3svc'" );
			var iis = new DirectoryEntry( "IIS://localhost/W3svc" );

			entry.LogLine( "Looking for entries of type 'IIsWebServer'" );
			var websites = iis.Children
									.Cast<DirectoryEntry>()
									.Where( v=>v.SchemaClassName == "IIsWebServer" );

			var items = new List<ComboBoxItem>();
			foreach( var website in websites )
			{
				entry.LogLine( "Found '" + website.Path + "'" );
				var description = (string)website.Properties[ "ServerComment" ].Value;
				string rootPath = website.Path + "/ROOT";
				entry.LogLine( "Getting '" + rootPath + "'" );
				(new DirectoryEntry(rootPath)).Children.Cast<object>().ToArray();  // Check the directory is valid
				items.Add( new ComboBoxItem {
										Content = string.Format( "{0}: {1}", website.Name, description ),
										Tag = rootPath } );
			}
			foreach( var item in items )
				ddWebSites.Items.Add( item );
			if( ddWebSites.Items.Count > 0 )
				ddWebSites.SelectedIndex = 0;
		}

		private void LoadVirtualDirectories(ActionEntry entry)
		{
			ddVirtualDirs.Items.Clear();
			var webSiteItem = ddWebSites.SelectedItem as ComboBoxItem;
			if( webSiteItem == null )
			{
				entry.LogLine( "No website selected" );
				return;
			}
			var webSiteDirectoryEntryPath = (string)webSiteItem.Tag;
			entry.LogLine( "Using website at '" + webSiteDirectoryEntryPath + "'" );
			var webSiteDirectoryEntry = new DirectoryEntry( webSiteDirectoryEntryPath );

			entry.LogLine( "Searching recursively for virtual dirs" );
			var dirList = new List<DirectoryEntry>();
			dirList.Add( webSiteDirectoryEntry );
			BrowseDirectoriesRecursive( entry, webSiteDirectoryEntry, dirList );

			ComboBoxItem selectedItem = null;
			var physicalPath = PhysicalPath;
			foreach( var dirEntry in dirList )
			{
				string rootRelativePath = dirEntry.Path.Substring( webSiteDirectoryEntry.Path.Length );
				if( rootRelativePath.Length == 0 )
					rootRelativePath = "/";
				var item = new ComboBoxItem {	Content = rootRelativePath,
												Tag = dirEntry.Path };
				ddVirtualDirs.Items.Add( item );

				if( (physicalPath != null) && (physicalPath.ToLower() == ((string)dirEntry.Properties["Path"].Value ?? "").ToLower()) )
				{
					entry.LogLine( "Using virtual dir at '" + dirEntry.Path + "'" );
					selectedItem = item;
				}
			}
			if( selectedItem != null )
				ddVirtualDirs.SelectedItem = selectedItem;

			CheckStatus();
		}

		private void BrowseDirectoriesRecursive(ActionEntry entry, DirectoryEntry dirEntry, List<DirectoryEntry> dirList)
		{
			foreach( var child in dirEntry.Children.Cast<DirectoryEntry>() )
			{
				switch( child.SchemaClassName )
				{
					case "IIsWebVirtualDir":
						entry.LogLine( "Found virtual directory at '" + child.Path + "'" );
						dirList.Add( child );
						goto case "IIsWebDirectory";
					case "IIsWebDirectory":
						entry.LaunchSubAction( (e)=>{ BrowseDirectoriesRecursive( e, child, dirList ); } );
						break;
					default:
						System.Diagnostics.Debug.Fail( "Unknown child type '" + child.SchemaClassName + "'" );
						break;
				}
			}
		}

		private void btnReplace_Click(ActionEntry entry)
		{
			entry.LogLine( "Removing old virtual directory" );
			entry.LaunchSubAction( RemoveVirtualDirectory );
			if( (!entry.HasErrors) && (!entry.HasExceptions) )
			{
				entry.LogLine( "Recreate virtual directory" );
				entry.LaunchSubAction( CreateVirtualDirectory );
			}
			entry.LogLine( "Reload IIS virtual directories list" );
			ddVirtualDirs.SelectedIndex = -1;
			entry.LaunchSubAction( LoadVirtualDirectories );
			if( (!entry.HasErrors) && (!entry.HasExceptions) )
			{
				MessageBox.Show( "Virtual directory replaced" );
			}
			else
			{
System.Diagnostics.Debug.Fail( "HERE: DialogBox de l'ActionEntry qui a foiré" );
			}
			CheckStatus();
		}

		private void btnCreate_Click(ActionEntry entry)
		{
			entry.LaunchSubAction( CreateVirtualDirectory );
			entry.LogLine( "Reload IIS virtual directories list" );
			ddVirtualDirs.SelectedIndex = -1;
			entry.LaunchSubAction( LoadVirtualDirectories );
			if( (!entry.HasErrors) && (!entry.HasExceptions) )
			{
				MessageBox.Show( "Virtual directory created" );
			}
			else
			{
System.Diagnostics.Debug.Fail( "HERE: DialogBox de l'ActionEntry qui a foiré" );
			}
			CheckStatus();
		}

		private void btnRemove_Click(ActionEntry entry)
		{
			entry.LaunchSubAction( RemoveVirtualDirectory );
			entry.LogLine( "Reload IIS virtual directories list" );
			ddVirtualDirs.SelectedIndex = -1;
			entry.LaunchSubAction( LoadVirtualDirectories );
			if( (!entry.HasErrors) && (!entry.HasExceptions) )
			{
				MessageBox.Show( "Virtual directory removed" );
			}
			else
			{
System.Diagnostics.Debug.Fail( "HERE: DialogBox de l'ActionEntry qui a foiré" );
			}
			CheckStatus();
		}

		private void RemoveVirtualDirectory(ActionEntry entry)
		{
			string fullPath, parentDir, dirName;
			GetSelectedDirectoryPath( out fullPath, out parentDir, out dirName );

			entry.LogLine( "Removing directory entry '" + fullPath + "'" );
			using( var directoryEntry = new DirectoryEntry(fullPath) )
			{
				directoryEntry.DeleteTree();
			}
		}

		private void CreateVirtualDirectory(ActionEntry entry)
		{
// TODO: Alain: Test on XP ; seems it needs a boolean here :
// Acces en lecture
// Choix du framework (v2 / v4)
// ASP doit être installé/activé: aspnet_regiis.exe -i -enable
//				a executer dans le répertoire "WINDOWS/Microsoft.NET/Framework/v4.0.30319" ou/et "v2.0.50727"
//				(désinstall: aspnet_regiis.exe -ua)
// sinon, manque les directoryEntry.Properties["ScriptMap"] ==> *.aspx : machin_aspnet.dll
// c.f. http://serverfault.com/questions/1649/why-does-iis-refuse-to-serve-asp-net-content
// => Détection de l'installation/activation

			string fullPath, parentDir, dirName;
			GetSelectedDirectoryPath( out fullPath, out parentDir, out dirName );
			string physicalPath = PhysicalPath;
			System.Diagnostics.Debug.Assert( physicalPath != null, "'PhysicalPath' should be available here" );

			entry.LogLine( "Getting directory entry '" + parentDir + "'" );
			using( var parentDirectoryEntry = new DirectoryEntry(parentDir) )
			{
				parentDirectoryEntry.Children.Cast<object>().ToArray();  // Check that the entry is valid

				entry.LogLine( "Creating entry '" + dirName + "'" );
				// c.f. http://michaelsync.net/2005/12/01/iis-6-virtual-directories-management-with-c
				using( var directoryEntry = parentDirectoryEntry.Children.Add( dirName,"IIsWebVirtualDir") )
				{
					directoryEntry.Properties["Path"][0] = physicalPath;
					directoryEntry.Properties["EnableDirBrowsing"][0] = IISEnableDirBrowsing;
					directoryEntry.Properties["AccessRead"][0] = IISAccessRead;
					directoryEntry.Properties["AccessExecute"][0] = IISAccessExecute;
					directoryEntry.Properties["AccessWrite"][0] = IISAccessWrite;
					directoryEntry.Properties["AccessScript"][0] = IISAccessScript;
					//directoryEntry.Properties["AuthNTLM"][0] = true;
					//directoryEntry.Properties["EnableDefaultDoc"][0] = true;
					directoryEntry.Properties["DefaultDoc"][0] = IISDefaultDoc;
					//directoryEntry.Properties["AspEnableParentPaths"][0] = true;
					directoryEntry.Properties["AppFriendlyName"][0] = WebAppFriendlyName;
					directoryEntry.CommitChanges();

					//'the following are acceptable params
					//'INPROC = 0
					//'OUTPROC = 1
					//'POOLED = 2
// TODO: Alain: What's this?
					directoryEntry.Invoke("AppCreate", 1);
					directoryEntry.CommitChanges();
				}
			}
		}

		private void GetSelectedDirectoryPath(out string fullPath, out string parentDir, out string dirName)
		{
			string webSite = (string)((ComboBoxItem)ddWebSites.SelectedValue).Tag;
			string virtualPath = ddVirtualDirs.Text;
			if(! virtualPath.StartsWith("/") )
				virtualPath = "/" + virtualPath;
			fullPath = webSite + virtualPath;
			if( fullPath.EndsWith("/") )
				fullPath = fullPath.Substring( 0, fullPath.Length-1 );

			var tokens = fullPath.Split( '/' );
			string lastToken = tokens[tokens.Length-1];
			parentDir = fullPath.Substring( 0, fullPath.Length - lastToken.Length - 1 );
			dirName = lastToken;
		}
	}
}
