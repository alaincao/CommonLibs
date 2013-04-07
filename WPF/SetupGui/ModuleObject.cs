//
// CommonLibs/WPF/SetupGui/ModuleObject.cs
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
