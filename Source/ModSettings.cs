using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace WealthWindow
{
    [DefOf]
    public static class KeyBindings
    {
        public static KeyBindingDef WealthWindow;

		static KeyBindings() => DefOfHelper.EnsureInitializedInCtor(typeof(KeyBindings));
    }


	public enum EModifierKey
	{
		None,
		Alt,
		Command,
		Control,
		Shift
	}


    public class WealthWindowSettings : ModSettings
    {
        public string ModName = typeof( WealthWindowMod ).Assembly.GetName().Name;

		public EModifierKey ModifierKey;


        public void DoWindowContents( Rect InRect )
        {
            Listing_Standard Listing = new Listing_Standard()
            {
                ColumnWidth = InRect.width
            };

            Listing.Begin( InRect );

			Rect LabelRect = Listing.Label( "HotKey Modifier".Translate() );

			if ( Widgets.ButtonText( LabelRect.RightPartPixels( 128 ), ModifierKey.ToString() ) )
			{
                List<FloatMenuOption> Options = System.Enum.GetValues( typeof( EModifierKey ) ).Cast<EModifierKey>().ToArray()
                    .Select( Value => new FloatMenuOption( Value.ToString(), () => ModifierKey = Value ) )
                    .ToList();
                Find.WindowStack.Add( new FloatMenu( Options ) );
			}

			Listing.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<EModifierKey>( ref ModifierKey, nameof( ModifierKey ) );
        }
    }
}