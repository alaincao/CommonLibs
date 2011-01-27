using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace CommonLibs.WPF.SetupGui
{
	public class ModulesManager
	{
		private ObservableCollection<ModuleObject>	AllModulesList;
		public ObservableCollection<ModuleObject>	AvailableModulesList			{ get; private set; }
		private ContentControl						ContentControl;

		private ModuleObject						SelectedModule		= null;

		public ModulesManager(IEnumerable<ModuleObject> modules, ContentControl contentControl)
		{
			System.Diagnostics.Debug.Assert( modules != null, "Missing parameter 'modules'" );
			System.Diagnostics.Debug.Assert( contentControl != null, "Missing parameter 'contentControl'" );

			AllModulesList = new ObservableCollection<ModuleObject>( modules );
			AvailableModulesList = new ObservableCollection<ModuleObject>();
			foreach( var module in AllModulesList )
			{
				if( module.IsAvailable )
					AvailableModulesList.Add( module );

				var tmp = module;
				module.IsAvailableHelper.ValueChanged += ()=>Module_IsAvailableChanged( tmp/*NB: Needs to be a local variable*/ );
			}
			ContentControl = contentControl;

			AvailableModulesList.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler( VisibleModulesList_CollectionChanged );

			foreach( var module in AllModulesList )
				module.SendOnModuleLoaded();
			foreach( var module in AllModulesList )
				module.SendOnAllInitialModulesLoaded();
		}

		private void Module_IsAvailableChanged(ModuleObject module)
		{
			if( module.IsAvailable )
			{
				int insertIndex;
				if( AvailableModulesList.Count == 0 )
				{
					// Will be the only one in the list
					insertIndex = 0;
				}
				else
				{
					int indexInAll = AllModulesList.IndexOf( module );
					System.Diagnostics.Debug.Assert( indexInAll >= 0, "The specified module is not in the AllModulesList" );

					// Search for the visible module before
					for( int i=indexInAll-1; i>=0; --i )
					{
						if( AllModulesList[i].IsAvailable )
						{
							insertIndex = AvailableModulesList.IndexOf( AllModulesList[i] ) + 1;
							System.Diagnostics.Debug.Assert( insertIndex >= 0, "The visible module before has not been found in AvailableModulesList" );
							goto InsertIndexFound;
						}
					}

					// Search for the visible module after
					for( int i=indexInAll+1; i<AllModulesList.Count; ++i )
					{
						if( AllModulesList[i].IsAvailable )
						{
							insertIndex = AvailableModulesList.IndexOf( AllModulesList[i] );
							System.Diagnostics.Debug.Assert( insertIndex >= 0, "The visible module before has not been found in AvailableModulesList" );
							goto InsertIndexFound;
						}
					}

					throw new ApplicationException( "Could not find the insertIndex in the available module list" );
				}
			InsertIndexFound:
				System.Diagnostics.Debug.Assert( insertIndex >= 0, "Invalid value for insertIndex" );
				AvailableModulesList.Insert( insertIndex, module );
			}
			else  // module is not visible
			{
				AvailableModulesList.Remove( module );
			}
		}

		private void VisibleModulesList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			switch( e.Action )
			{
				case NotifyCollectionChangedAction.Add: {
					foreach( ModuleObject module in e.NewItems )
						module.IsAvailable = true;
					break; }

				case NotifyCollectionChangedAction.Remove:{
					foreach( ModuleObject module in e.OldItems )
					{
						if( module == SelectedModule )
							ContentControl.Content = null;
						module.IsAvailable = false;
					}
					break; }

				case NotifyCollectionChangedAction.Reset: {
					foreach( var module in AllModulesList.Where( v=>v.IsAvailable ) )
					{
						if( module == SelectedModule )
							ContentControl.Content = null;
						module.IsAvailable = false;
					}
					break; }

				case NotifyCollectionChangedAction.Move:
				case NotifyCollectionChangedAction.Replace:
				default:
					throw new NotImplementedException( GetType().FullName + "::" + System.Reflection.MethodInfo.GetCurrentMethod().Name + "(" + e.Action + ")" );
			}
		}

		public void SelectModule(ModuleObject module)
		{
			if( module == null )
			{
				ContentControl.Content = null;
				SelectedModule = null;
			}
			else
			{
				var control = module.ContentControl;
				ContentControl.Content = control;
				control.Width = double.NaN;
				control.Height = double.NaN;
				control.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
				control.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
				SelectedModule = module;
			}
		}
	}
}
