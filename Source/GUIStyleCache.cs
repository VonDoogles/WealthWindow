using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WealthWindow
{
    public class GUIStyleCache
	{
		class GUIStyleCacheItem
		{
			public GameFont Font;
			public TextAnchor Anchor;
			public bool WordWrap;
			public Color GUIColor;
		}

		static Stack<GUIStyleCacheItem> CacheStack = new Stack<GUIStyleCacheItem>();


		public static void Push()
		{
			CacheStack.Push(new GUIStyleCacheItem()
			{
				Font = Text.Font,
				Anchor = Text.Anchor,
				WordWrap = Text.WordWrap,
				GUIColor = GUI.color
			});
		}

		public static void Pop()
		{
			if (CacheStack.Count > 0)
			{
				GUIStyleCacheItem Cache = CacheStack.Pop();
				Text.Font = Cache.Font;
				Text.Anchor = Cache.Anchor;
				Text.WordWrap = Cache.WordWrap;
				GUI.color = Cache.GUIColor;
			}
		}
	}
}

