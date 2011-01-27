using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

using CommonLibs.Utils.Event;

namespace CommonLibs.WPF.SetupGui
{
	public class ModuleObject
	{
		public string					DisplayName			{ get; private set; }
		public UserControl				ContentControl		{ get; private set; }
		public bool						IsAvailable			{ get { return IsAvailableHelper.Value; } set { IsAvailableHelper.Value = value; } }
		public ValueHelper<bool>		IsAvailableHelper	{ get; private set; }
		public bool						CanSave				{ get { return CanSaveHelper.Value; } }
		public IValueHelper<bool>		CanSaveHelper		{ get; private set; }

		public event Action							OnModuleLoaded;
		public event Action							OnAllInitialModulesLoaded;
		public event Action<Actions.ActionEntry>	OnSave;

		public ModuleObject(string displayName, UserControl contentControl, IValueHelper<bool> canSaveHelper)
		{
			DisplayName = displayName;
			ContentControl = contentControl;
			IsAvailableHelper = new ValueHelper<bool>() { Value = true/*Starts available by default*/ };
			CanSaveHelper = canSaveHelper;
		}
		
		internal void SendOnModuleLoaded()
		{
			if( OnModuleLoaded != null )
				OnModuleLoaded();
		}

		internal void SendOnAllInitialModulesLoaded()
		{
			if( OnAllInitialModulesLoaded != null )
				OnAllInitialModulesLoaded();
		}

		internal void SendOnSave(Actions.ActionEntry entry)
		{
			if( OnSave != null )
				OnSave( entry );
		}
	}
}
