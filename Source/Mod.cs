using HarmonyLib;
using UnityEngine;
using Verse;

namespace WealthWindow
{
    [StaticConstructorOnStartup]
    public static class WealthWindow_Patches
    {
        static WealthWindow_Patches()
        {
            Harmony HarmonyInstance = new Harmony( "WealthWindowMod.Patches" );
            HarmonyInstance.PatchAll();
        }
    }

    public class WealthWindowMod : Mod
    {
        public static WealthWindowMod? Instance { get; private set; }

        public static WealthWindowSettings? Settings { get; private set; }


        public WealthWindowMod(ModContentPack Content )
            : base( Content )
        {
            Instance = this;
            Settings = new WealthWindowSettings();
        }


        public override void DoSettingsWindowContents( Rect InRect )
        {
            base.DoSettingsWindowContents( InRect );
            Settings?.DoWindowContents( InRect );
        }


        public override string SettingsCategory()
        {
            return "WealthWindow_Settings".Translate();
        }
    }
}
