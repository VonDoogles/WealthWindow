using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace WealthWindow
{

    [StaticConstructorOnStartup]
	public class WealthWindow : Window
	{
		public static WealthWindow Instance { get; } = new WealthWindow();

		static readonly Rect ZeroRect = Rect.zero;

		static readonly AccessTools.FieldRef<Window,bool> ResizeLater = AccessTools.FieldRefAccess<Window,bool>("resizeLater");
		static readonly AccessTools.FieldRef<float[]> CachedTerrainMarketValue = AccessTools.StaticFieldRefAccess<float[]>( AccessTools.Field(typeof(WealthWatcher), "cachedTerrainMarketValue") );


		public override Vector2 InitialSize => new Vector2(750, 800);


		ThingCategoryDef CategoryFloor = new ThingCategoryDef()
		{
			parent = ThingCategoryDefOf.Root,
			defName = "Floors",
			label = "Floors"
		};

		ThingCategoryDef CategoryUnknown = new ThingCategoryDef()
		{
			parent = ThingCategoryDefOf.Root,
			defName = "Unknown",
			label = "Unknown"
		};


		List<IThingHolder> HolderList = new List<IThingHolder>();
		Stack<IThingHolder> HolderStack = new Stack<IThingHolder>();

		class WealthNode
		{
			public Color LayoutColor = Color.HSVToRGB(Rand.Value, Rand.Range(0.4f, 1.0f), Rand.Range(0.3f, 0.9f));
			public Rect LayoutRect = Rect.zero;
			public bool Selected = false;
			public float Wealth = 0.0f;

			public HashSet<Thing> ThingSet = new HashSet<Thing>();

			public ThingCategoryDef Category = null;
			public BuildableDef Def => Terrain as BuildableDef ?? ThingSet.FirstOrDefault()?.def as BuildableDef;
			public TerrainDef Terrain = null;
			public ThingCategoryDef TerrainCategory = null;
			public int TerrainCount = 0;
			public ThingDef ThingDef => ThingSet.FirstOrDefault()?.def;

			public List<WealthNode> Children = new List<WealthNode>();

			public TaggedString LabelCap => Category?.LabelCap ?? Def?.LabelCap ?? "Null Label";

			public Vector2 ScrollPos = Vector2.zero;
			public Rect ScrollRect = Rect.zero;

			public int ThingCount
			{
				get
				{
					if (Children.Any())
					{
						return Children.Sum(ChildNode => ChildNode.ThingCount);
					}
					if (ThingSet.Any())
					{
						return ThingSet.Sum(ThingIter => ThingIter.stackCount);
					}
					return TerrainCount;
				}
			}

			public IEnumerable<GlobalTargetInfo> AllTargetInfo
			{
				get
				{
					if ( Terrain != null && Wealth != 0.0f )
					{
						TerrainDef[] TopGrid = Find.CurrentMap.terrainGrid.topGrid;
						bool[] FogGrid = Find.CurrentMap.fogGrid.fogGrid;
						IntVec3 Size = Find.CurrentMap.Size;
						int Count = Size.x * Size.z;

						for ( int Index = 0; Index < Count; ++Index )
						{
							if ( !FogGrid[ Index ] && TopGrid[ Index ] == Terrain )
							{
								yield return new GlobalTargetInfo( CellIndicesUtility.IndexToCell( Index, Size.x ), Find.CurrentMap );
							}
						}
					}

					foreach ( Thing thing in ThingSet )
					{
						yield return new GlobalTargetInfo( thing );
					}

					foreach ( WealthNode Child in Children )
					{
						foreach ( GlobalTargetInfo TargetInfo in Child.AllTargetInfo )
						{
							yield return TargetInfo;
						}
					}

					yield break;
				}
			}


			public void ResetWealth()
			{
				LayoutRect = ZeroRect;
				Selected = false;
				Wealth = 0.0f;
				TerrainCount = 0;
				Children.Clear();
				ThingSet.Clear();
			}
		}

		Vector2 ItemScrollPos = Vector2.zero;
		Vector2 ListScrollPos = Vector2.zero;
		Rect ListViewRect = Rect.zero;

		Dictionary<BuildableDef, WealthNode> WealthByDef = new Dictionary<BuildableDef, WealthNode>();
		Dictionary<ThingCategoryDef, WealthNode> WealthByCategory = new Dictionary<ThingCategoryDef, WealthNode>();

		float SelectedWealth = 0.0f;
		float WealthMax = 0.0f;
		float WealthTotal = 0.0f;
		Rect WealthRect;
		bool GroupByCategory = false;

		bool SearchFieldFocused;
		string SearchFilter;

		FloatRange ViewRange = FloatRange.ZeroToOne;

		float WatcherBuildings = 0.0f;
		float WatcherFloorsOnly = 0.0f;
		float WatcherItems = 0.0f;
		float WatcherPawns = 0.0f;

		bool bShowBuildings = true;
		bool bShowItems = true;
		bool bShowPawns = true;

		Color HighlightColor = new Color( 0.25f, 0.25f, 0.25f, 1.0f );


		private WealthWindow()
		{
			draggable = true;
            resizeable = true;

			closeOnAccept = false;
			closeOnCancel = true;
			closeOnClickedOutside = false;
			focusWhenOpened = false;
			preventCameraMotion = false;

			layer = WindowLayer.GameUI;
		}


		public override void PreClose()
		{
			base.PreClose();
		}


		public override void PreOpen()
		{
			base.PreOpen();
			SearchFieldFocused = false;
			SearchFilter = "";
			CalculateWealth();
		}


		public override void DoWindowContents(Rect InRect)
		{
			if (ResizeLater(this))
			{
				Rect DesiredRect = windowRect;
				if ( Event.current.shift )
				{
					DesiredRect.x = (UI.screenWidth - DesiredRect.width) / 2.0f;
					DesiredRect.y = (UI.screenHeight - DesiredRect.height) / 2.0f;
				}
				windowRect = DesiredRect.Rounded();
			}

			GUIStyleCache.Push();
			Text.Font = GameFont.Small;

			InRect.SplitHorizontally( 96, out Rect ToolbarRect, out Rect MapRect );
			MapRect = MapRect.ContractedBy( 8 );

			Rect SearchRect = new Rect( ToolbarRect ) { height = 32 };
			Rect OptionsRect = new Rect( SearchRect ) { y = SearchRect.y + 32 };
			Rect InfoRect = new Rect( SearchRect ) { y = SearchRect.y + 64 };

			bool bCalculateWealth = false;

			bCalculateWealth |= ButtonText( ref SearchRect, "Scan Wealth" );
			bCalculateWealth |= Checkbox( ref OptionsRect, "Group by Category", ref GroupByCategory );
			bCalculateWealth |= Checkbox( ref OptionsRect, "Show Buildings", ref bShowBuildings );
			bCalculateWealth |= Checkbox( ref OptionsRect, "Show Items", ref bShowItems );
			bCalculateWealth |= Checkbox( ref OptionsRect, "Show Pawns", ref bShowPawns );

			if ( bCalculateWealth )
			{
				CalculateWealth();
			}

			bool ProcessSearch = false;
			{
				if ( Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return )
				{
					ProcessSearch = true;
					Event.current.Use();
				}

				SearchRect.SplitVertically( SearchRect.width - 80, out Rect SearchFilterRect, out Rect SearchButtonRect );

				GUI.SetNextControlName( "SearchField" );
				SearchFilter = Widgets.TextField( SearchFilterRect.ContractedBy( 2 ), SearchFilter );

				if ( !SearchFieldFocused )
				{
					UI.FocusControl( "SearchField", this );
					SearchFieldFocused = true;
				}

				ProcessSearch |= Widgets.ButtonText( SearchButtonRect.ContractedBy( 2 ), "Search" );

				if ( ProcessSearch )
				{
					CalculateWealth();
				}
			}

			GUIStyleCache.Push();
			Text.Anchor = TextAnchor.MiddleLeft;
			Text.Font = GameFont.Tiny;
			TaggedString TagLabel = $"WealthGraph:  (Buildings: ${WatcherBuildings}  FloorsOnly: ${WatcherFloorsOnly}  Items: ${WatcherItems}  Pawns: ${WatcherPawns})\nSelected Wealth: ${SelectedWealth} of ${WealthTotal}";
			Widgets.Label( InfoRect, TagLabel );
			GUIStyleCache.Pop();

			WealthRect.SplitHorizontally( 24, out Rect SliderRect, out Rect GraphRect );

			FloatRange ViewRangeOld = ViewRange;
			Widgets.FloatRange( SliderRect, SliderRect.GetHashCode(), ref ViewRange, 0.0f, WealthMax );

			if (WealthRect != MapRect)
			{
				WealthRect = MapRect;
			}

			{
				IEnumerable<WealthNode> NodeQuery = GroupByCategory ? WealthByCategory.Values.AsEnumerable() : WealthByDef.Values.AsEnumerable();
				List<WealthNode> NodeList = NodeQuery.Where( Node => NodeInView( Node ) ).OrderByDescending( Node => Node.Wealth ).ToList();

				float ViewWealthMax = NodeList.Count() > 0 ? NodeList.Max( Node => Node.Wealth ) :  0;
				float OneOverViewWealth = ViewWealthMax != 0 ? 1.0f / ViewWealthMax : 1.0f;

				float NodePadding = 6;
				float NodeWidth = 320 + NodePadding;
				float NodeHeight = 64 + NodePadding;

				GraphRect.yMin += 8;
				GraphRect.SplitVertically( NodeWidth + 20, out Rect DefsRect, out Rect ItemsRect );

				Action<Rect, WealthNode> DrawWealthNode = ( NodeRect, Node ) =>
				{
					NodeRect = NodeRect.ContractedBy( 2 );

					if ( Node.Selected )
					{
						Widgets.DrawBoxSolid( NodeRect, HighlightColor );
					}

					GUI.color = Color.gray;
					Widgets.DrawBox( NodeRect, 1 );
					GUI.color = Color.white;

					Rect InnerRect = NodeRect.ContractedBy( NodePadding );
					InnerRect.SplitHorizontally( InnerRect.height - 16, out Rect UpperRect, out Rect BarRect );

					UpperRect.SplitVertically( UpperRect.height + 4, out Rect IconRect, out Rect TextRect );

					Widgets.DefIcon( IconRect.ContractedBy( 2 ), Node.Def );

					GUIStyleCache.Push();
					{
						Text.Anchor = TextAnchor.UpperLeft;
						Widgets.Label( TextRect, Node.LabelCap );

						Text.Anchor = TextAnchor.UpperRight;
						Widgets.Label( TextRect, (TaggedString)$"${Node.Wealth}" );

						Text.Anchor = TextAnchor.LowerLeft;
						Text.Font = GameFont.Tiny;
						float AvgCost = Node.ThingCount != 0 ? Node.Wealth / Node.ThingCount : 0;
						Widgets.Label( TextRect, (TaggedString)$"{Node.ThingCount} x ~${AvgCost}" );
					}
					GUIStyleCache.Pop();

					BarRect.width *= ( Node.Wealth * OneOverViewWealth );
					GUI.DrawTexture( BarRect.ContractedBy( 0, ( BarRect.height - 8 ) * 0.5f ), BaseContent.WhiteTex );

					if ( Widgets.ButtonInvisible( InnerRect ) )
					{
						if ( Event.current.control )
						{
							Node.Selected = !Node.Selected;
						}
						else if ( Event.current.shift )
						{
							int NodeIndex = NodeList.IndexOf( Node );
							int First = Mathf.Min( NodeIndex, NodeList.FindIndex( N => N.Selected ) );
							int Last = Mathf.Max( NodeIndex, NodeList.FindLastIndex( N => N.Selected ) );

							for ( int Idx = 0; Idx < NodeList.Count; ++Idx )
							{
								NodeList[ Idx ].Selected = Idx >= First && Idx <= Last;
							}
						}
						else
						{
							NodeList.ForEach( N => N.Selected = false );
							Node.Selected = true;
						}
					}
				};

				ExGUI.VirtualList( DefsRect, ref ListScrollPos, NodeList, DrawWealthNode, new Vector2( NodeWidth, NodeHeight ) );

				Action<Rect, GlobalTargetInfo> DrawItem = ( ItemRect, TargetInfo ) =>
				{
					ItemRect.SplitVertically( ItemRect.height + 4, out Rect IconRect, out Rect LabelRect );

					if ( TargetInfo.HasThing )
					{
						Thing Item = TargetInfo.Thing;
						Widgets.ThingIcon( IconRect, Item );

						float ThingWealth = ( Item.def.IsBuildingArtificial
											  ? Item.GetStatValue( StatDefOf.MarketValueIgnoreHp )
											  : Item.MarketValue * Item.stackCount );

						Widgets.Label( LabelRect, (TaggedString)$"{Item.LabelCap} ${ThingWealth}" );
					}
					else if ( TargetInfo.IsMapTarget )
					{
						TerrainDef FloorDef = Find.CurrentMap.terrainGrid.TerrainAt( TargetInfo.Cell );
						Widgets.DefIcon( IconRect.ContractedBy( 2 ), FloorDef );

						float FloorWealth = CachedTerrainMarketValue()[ (int)FloorDef.index ];
						Widgets.Label( LabelRect, (TaggedString)$"{FloorDef.LabelCap} ${FloorWealth}" );
					}

					if ( Widgets.ButtonInvisible( ItemRect ) )
					{
						CameraJumper.TryJumpAndSelect( TargetInfo );
					}
				};

				ItemsRect.xMin += 8;
				float ItemWidth = ItemsRect.width - 20;

				List<GlobalTargetInfo> ItemList = ( from Node in NodeList where Node.Selected
													from Item in Node.AllTargetInfo select Item ).ToList();
				GUIStyleCache.Push();
				Text.Anchor = TextAnchor.MiddleLeft;
				ExGUI.VirtualList( ItemsRect, ref ItemScrollPos, ItemList, DrawItem, new Vector2( ItemWidth, 32 ) );
				GUIStyleCache.Pop();

				SelectedWealth = NodeList.Where( Node => Node.Selected ).Sum( Node => Node.Wealth );
			}

			if ( Event.current.type == EventType.ScrollWheel && windowRect.AtZero().Contains( Event.current.mousePosition ) )
			{
				Event.current.Use();
			}

			GUIStyleCache.Pop();
		}


		void CalculateWealth()
		{
			WealthWatcher Watcher = Find.CurrentMap.wealthWatcher;
			Watcher.ForceRecount();

			Traverse WatcherTraverse = Traverse.Create( Watcher );
			WatcherBuildings = WatcherTraverse.Field( "wealthBuildings" ).GetValue<float>();
			WatcherItems = WatcherTraverse.Field( "wealthItems" ).GetValue<float>();
			WatcherFloorsOnly = WatcherTraverse.Field( "wealthFloorsOnly" ).GetValue<float>();
			WatcherPawns = WatcherTraverse.Field( "wealthPawns" ).GetValue<float>();

			ResetWealth();

			if ( bShowBuildings )
			{
				List<Thing> BuildingList = Find.CurrentMap.listerThings.ThingsInGroup( ThingRequestGroup.BuildingArtificial );
				foreach ( Thing Building in BuildingList )
				{
					if ( Building.Faction == Faction.OfPlayer )
					{
						VisitBuilding( Building );
					}
				}

				VisitFloors();
			}

			if ( bShowItems )
			{
				Predicate<IThingHolder> PassCheck = ThingHolder =>
				{
					if ( ThingHolder is PassingShip || ThingHolder is MapComponent )
					{
						return false;
					}
					Pawn ThingPawn = ThingHolder as Pawn;
					return ThingPawn == null || (ThingPawn.Faction == Faction.OfPlayer && !ThingPawn.IsQuestLodger());
				};

				bool bAllowUnreal = false;
				bool bAlsoGetSpawnedThings = true;
				ThingRequest ThingReq = ThingRequest.ForGroup( ThingRequestGroup.HaulableEver );

				VisitAllThingsRecursively( ThingReq, VisitThing, PassCheck, bAllowUnreal, bAlsoGetSpawnedThings );
			}

			if ( bShowPawns )
			{
				VisitPawns();
			}

			foreach ( WealthNode Node in WealthByDef.Values )
			{
				if ( Node.ThingCount != 0 )
				{
					ThingCategoryDef CategoryDef = Node.ThingDef?.FirstThingCategory ?? Node.TerrainCategory ?? CategoryUnknown;
					if ( !WealthByCategory.TryGetValue( CategoryDef, out WealthNode CategoryNode ) )
					{
						CategoryNode = new WealthNode()
						{
							Category = CategoryDef
						};
						WealthByCategory.Add( CategoryDef, CategoryNode );
					}

					CategoryNode.Children.Add( Node );
					CategoryNode.Wealth += Node.Wealth;
				}
			}

			if ( GroupByCategory )
			{
				WealthMax = WealthByCategory.Values.Max( Node => Node.Wealth );
			}
			else
			{
				WealthMax = WealthByDef.Values.Max( Node => Node.Wealth );
			}

			ItemScrollPos = Vector2.zero;
			ListScrollPos = Vector2.zero;

			ViewRange.min = 0.0f;
			ViewRange.max = WealthMax;
		}


		bool ButtonText( ref Rect InRect, string Label, float Spacing = 16.0f )
		{
			Rect ButtonRect = new Rect( InRect ) { width = Verse.Text.CalcSize( Label ).x + 32 };
			InRect.xMin += ButtonRect.width + Spacing;
			return Widgets.ButtonText( ButtonRect, Label );
		}


		bool Checkbox( ref Rect InRect, string Label, ref bool Value, float Spacing = 16.0f )
		{
			Rect CheckboxRect = new Rect( InRect ) { width = Verse.Text.CalcSize( Label ).x + 24.0f + 10.0f };
			InRect.xMin += CheckboxRect.width + Spacing;

			bool ValueOld = Value;
			Widgets.CheckboxLabeled( CheckboxRect, Label, ref Value );
			return Value != ValueOld;
		}


		bool NodeInView( WealthNode Node )
		{
			return Node.ThingCount > 0 && Node.Wealth >= ViewRange.min && Node.Wealth <= ViewRange.max;
		}


		void ResetWealth()
		{
			WealthByDef.Values.Do( Node => Node.ResetWealth() );
			WealthByCategory.Values.Do( Node => Node.ResetWealth() );
            WealthMax = 0.0f;
			WealthTotal = 0.0f;
			WealthRect = ZeroRect;
		}


		void VisitBuilding( Thing Building )
		{
			if ( Building.def == null )
			{
				return;
			}

			bool bMatchesFilter = ( string.IsNullOrEmpty( SearchFilter )
									|| Building.Label.IndexOf( SearchFilter, System.StringComparison.OrdinalIgnoreCase ) != -1
									|| Building.def.label.IndexOf( SearchFilter, System.StringComparison.OrdinalIgnoreCase ) != -1 );
			if ( !bMatchesFilter )
			{
				return;
			}

			if (!WealthByDef.TryGetValue(Building.def, out WealthNode ThingNode))
			{
				ThingNode = new WealthNode();
				WealthByDef.Add(Building.def, ThingNode);
			}

			float ThingWealth = Building.GetStatValue(StatDefOf.MarketValueIgnoreHp);
			ThingNode.Wealth += ThingWealth;
			ThingNode.ThingSet.Add(Building);
			WealthTotal += ThingWealth;
		}


		void VisitFloors()
		{
			TerrainDef[] TopGrid = Find.CurrentMap.terrainGrid.topGrid;
			bool[] FogGrid = Find.CurrentMap.fogGrid.fogGrid;
			IntVec3 Size = Find.CurrentMap.Size;
			int Count = Size.x * Size.z;

			for ( int Index = 0; Index < Count; ++Index )
			{
				if ( !FogGrid[ Index ] )
				{
					TerrainDef FloorDef = TopGrid[ Index ];

					if ( FloorDef == null )
					{
						continue;
					}

					bool bMatchesFilter = ( string.IsNullOrEmpty( SearchFilter )
											|| FloorDef.label.IndexOf( SearchFilter, System.StringComparison.OrdinalIgnoreCase ) != -1 );
					if ( !bMatchesFilter )
					{
						continue;
					}

					if ( !WealthByDef.TryGetValue( FloorDef, out WealthNode FloorNode ) )
					{
						FloorNode = new WealthNode()
						{
							Terrain = FloorDef,
							TerrainCategory = CategoryFloor
						};
						WealthByDef.Add( FloorDef, FloorNode );
					}

					float FloorWealth = CachedTerrainMarketValue()[ (int)FloorDef.index ];
					FloorNode.TerrainCount += 1;
					FloorNode.Wealth += FloorWealth;
					WealthTotal += FloorWealth;
				}
			}
		}


		void VisitPawns()
		{
			foreach ( Pawn pawn in Find.CurrentMap.mapPawns.PawnsInFaction( Faction.OfPlayer ) )
			{
				if ( !pawn.IsQuestLodger() )
				{
					bool bMatchesFilter = ( string.IsNullOrEmpty( SearchFilter )
											|| pawn.Label.IndexOf( SearchFilter, System.StringComparison.OrdinalIgnoreCase ) != -1
											|| pawn.def.label.IndexOf( SearchFilter, System.StringComparison.OrdinalIgnoreCase ) != -1 );
					if ( !bMatchesFilter )
					{
						continue;
					}

					if ( !WealthByDef.TryGetValue( pawn.def, out WealthNode PawnNode ) )
					{
						PawnNode = new WealthNode();
						WealthByDef.Add( pawn.def, PawnNode );
					}

					float PawnWealth = pawn.MarketValue * ( pawn.IsSlave ? 0.75f : 1.0f );

					PawnNode.Wealth += PawnWealth;
					PawnNode.ThingSet.Add( pawn );
					WealthTotal += PawnWealth;
				}
			}
		}


		void VisitThing(Thing ThingIter)
		{
			if (ThingIter == null || !ThingIter.SpawnedOrAnyParentSpawned || ThingIter.PositionHeld.Fogged(Find.CurrentMap))
			{
				return;
			}

			if ( ThingIter.def == null )
			{
				return;
			}

			bool bMatchesFilter = ( string.IsNullOrEmpty( SearchFilter )
									|| ThingIter.Label.IndexOf( SearchFilter, System.StringComparison.OrdinalIgnoreCase ) != -1
									|| ThingIter.def.label.IndexOf( SearchFilter, System.StringComparison.OrdinalIgnoreCase ) != -1 );
			if ( !bMatchesFilter )
			{
				return;
			}

			if (!WealthByDef.TryGetValue(ThingIter.def, out WealthNode ThingNode))
			{
				ThingNode = new WealthNode();
				WealthByDef.Add(ThingIter.def, ThingNode);
			}

			float ThingWealth = ThingIter.MarketValue * ThingIter.stackCount;
			ThingNode.Wealth += ThingWealth;
			ThingNode.ThingSet.Add(ThingIter);
			WealthTotal += ThingWealth;
		}


		void VisitAllThingsRecursively(ThingRequest Request, Action<Thing> Visit, Predicate<IThingHolder> PassCheck, bool bAllowUnreal, bool bVisitSpawnedThings)
		{
			if (bVisitSpawnedThings)
			{
				foreach (Thing ThingOnMap in Find.CurrentMap.listerThings.ThingsMatching(Request))
				{
					Visit(ThingOnMap);
				}
			}

			List<Thing> HolderList = Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.ThingHolder);
			foreach (Thing ThingIter in HolderList ?? Enumerable.Empty<Thing>())
			{
				if (ThingIter is IThingHolder Holder)
				{
					VisitAllThingsRecursively(Holder, Request, Visit, PassCheck, bAllowUnreal);
				}
				if (ThingIter is ThingWithComps WithComps)
				{
					foreach (ThingComp Comp in WithComps.AllComps)
					{
						if (Comp is IThingHolder HolderComp)
						{
							VisitAllThingsRecursively(HolderComp, Request, Visit, PassCheck, bAllowUnreal);
						}
					}
				}
			}
		}


		void VisitAllThingsRecursively(IThingHolder Holder, ThingRequest Request, Action<Thing> Visit, Predicate<IThingHolder> PassCheck, bool bAllowUnreal)
		{
			if (PassCheck != null && !PassCheck(Holder))
			{
				return;
			}

			HolderStack.Clear();
			HolderStack.Push(Holder);

			while (HolderStack.Count != 0)
			{
				IThingHolder Popped = HolderStack.Pop();
				if (bAllowUnreal || ThingOwnerUtility.AreImmediateContentsReal(Popped))
				{
					foreach (Thing ThingIter in Popped.GetDirectlyHeldThings() ?? Enumerable.Empty<Thing>())
					{
						if (Request.Accepts(ThingIter))
						{
							Visit(ThingIter);
						}
					}
				}

				HolderList.Clear();
				Popped.GetChildHolders(HolderList);

				foreach (IThingHolder HolderIter in HolderList)
				{
					if (PassCheck == null || PassCheck(HolderIter))
					{
						HolderStack.Push(HolderIter);
					}
				}
			}

			HolderStack.Clear();
			HolderList.Clear();
		}
	}
}

