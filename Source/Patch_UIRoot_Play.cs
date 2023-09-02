using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WealthWindow
{
    [HarmonyPatch(typeof(UIRoot_Play), "UIRootOnGUI")]
	public static class Patch_UIRoot_Play_UIRootOnGUI
    {
        static void Postfix()
		{
			KeyBindingData keyBindingData;
			KeyCode CodeA = KeyCode.None;
			KeyCode CodeB = KeyCode.None;

			if ( KeyPrefs.KeyPrefsData.keyPrefs.TryGetValue( KeyBindings.WealthWindow, out keyBindingData ) )
			{
				CodeA = keyBindingData.keyBindingA;
				CodeB = keyBindingData.keyBindingB;
			}

			//if ( KeyBindings.WealthWindow.JustPressed )
			if ( Input.GetKeyUp( CodeA ) || Input.GetKeyUp( CodeB ) )
			{
				bool bOpenWindow = false;

				switch ( WealthWindowMod.Settings?.ModifierKey )
				{
					case EModifierKey.Alt:
						bOpenWindow = Event.current.alt;
						break;

					case EModifierKey.Command:
						bOpenWindow = Event.current.command;
						break;

					case EModifierKey.Control:
						bOpenWindow = Event.current.control;
						break;

					case EModifierKey.Shift:
						bOpenWindow = Event.current.shift;
						break;

					case EModifierKey.None:
					default:
						bOpenWindow = true;
						break;
				}

				if ( bOpenWindow && !Find.WindowStack.IsOpen<WealthWindow>() )
				{
					Event.current.Use();
					Find.WindowStack.Add( WealthWindow.Instance );
				}
			}
		}
	}
}

