using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WealthWindow
{
    public static class ExGUI
	{
		public static void VirtualList<ItemType>( Rect InRect, ref Vector2 ScrollPos, IList<ItemType> ItemList, System.Action<Rect, ItemType> DrawItem, Vector2 ItemSize )
		{
			int ItemCount = ItemList.Count;
			float VirtualHeight = Mathf.Max( InRect.height, ItemSize.y * ItemCount );

			Rect ScrollRect = InRect.RightPartPixels( 16 );
			ScrollPos.y = GUI.VerticalScrollbar( ScrollRect, ScrollPos.y, InRect.height, 0.0f, VirtualHeight );

			GUI.BeginGroup( InRect );
			{
				Rect ItemRect = new Rect( 0, 0, ItemSize.x, ItemSize.y );
				Rect ViewRect = InRect.AtZero();

				for ( int Idx = 0; Idx < ItemCount; ++Idx )
				{
					ItemRect.y = ( Idx * ItemSize.y ) - ScrollPos.y;

					if ( ViewRect.Overlaps( ItemRect ) )
					{
						DrawItem( ItemRect, ItemList[ Idx ] );
					}
				}
			}
			GUI.EndGroup();

			if ( Event.current.type == EventType.ScrollWheel && InRect.Contains( Event.current.mousePosition ) )
			{
				float ScrollMaxValue = VirtualHeight - InRect.height;
				ScrollPos.y = Mathf.Clamp( ScrollPos.y + ( Event.current.delta.y * 20.0f ), 0.0f, ScrollMaxValue );
				Event.current.Use();
			}
		}
	}
}

