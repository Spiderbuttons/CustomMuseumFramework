using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using CustomMuseumFramework.Helpers;
using CustomMuseumFramework.Models;
using StardewModdingAPI.Enums;
using StardewValley.Triggers;

namespace CustomMuseumFramework
{
    internal sealed class CMF : Mod
    {
        internal static IModHelper ModHelper { get; private set; } = null!;
        internal static IMonitor ModMonitor { get; private set; } = null!;

        internal static IManifest Manifest { get; private set; } = null!;
        private static CommandHandler CommandHandler { get; set; } = null!;
        private static Harmony Harmony { get; set; } = null!;

        private static Dictionary<string, CustomMuseumData>? _museumData;

        public static Dictionary<string, CustomMuseumData> MuseumData
        {
            get
            {
                return _museumData ??=
                    Game1.content.Load<Dictionary<string, CustomMuseumData>>("Spiderbuttons.CMF/Museums");
            }
        }

        private static Dictionary<string, CustomMuseumQuestData>? _questData;

        public static Dictionary<string, CustomMuseumQuestData> QuestData
        {
            get
            {
                return _questData ??=
                    Game1.content.Load<Dictionary<string, CustomMuseumQuestData>>("Spiderbuttons.CMF/Quests");
            }
        }

        private static Dictionary<string, List<CustomLostBookData>>? _lostBookData;

        public static Dictionary<string, List<CustomLostBookData>> LostBookData
        {
            get
            {
                return _lostBookData ??=
                    Game1.content.Load<Dictionary<string, List<CustomLostBookData>>>("Spiderbuttons.CMF/LostBooks");
            }
        }

        private static Dictionary<string, MuseumManager>? _lostBookLookup;

        public static Dictionary<string, MuseumManager> LostBookLookup
        {
            get
            {
                if (_lostBookLookup == null)
                {
                    _lostBookLookup = new Dictionary<string, MuseumManager>();
                    foreach (var museum in LostBookData)
                    {
                        if (!MuseumManagers.TryGetValue(museum.Key, out var manager)) continue;

                        foreach (var bookset in museum.Value)
                        {
                            _lostBookLookup.TryAdd(bookset.ItemId, manager);
                        }
                    }
                }

                return _lostBookLookup;
            }
        }

        private static Dictionary<string, SortedList<MuseumManager, bool>>? _globalDonatableItems;

        public static Dictionary<string, SortedList<MuseumManager, bool>> GlobalDonatableItems
        {
            get
            {
                if (_globalDonatableItems == null)
                {
                    _globalDonatableItems = new Dictionary<string, SortedList<MuseumManager, bool>>();
                    foreach (var museum in MuseumManagers.Values)
                    {
                        foreach (var itemId in museum.TotalPossibleDonations)
                        {
                            if (!_globalDonatableItems.ContainsKey(itemId))
                                _globalDonatableItems[itemId] =
                                    new SortedList<MuseumManager, bool>(new MuseumManagerComparer());
                            _globalDonatableItems[itemId].TryAdd(museum, museum.HasDonatedItem(itemId));
                        }
                    }
                }

                return _globalDonatableItems;
            }
        }

        public static Dictionary<string, MuseumManager> MuseumManagers { get; } = new();

        public override void Entry(IModHelper helper)
        {
            ModHelper = helper;
            ModMonitor = Monitor;
            Manifest = ModManifest;
            i18n.Init(ModHelper.Translation);
            CommandHandler = new CommandHandler(ModHelper, ModManifest, "cmf");
            CommandHandler.Register();
            Harmony = new Harmony(ModManifest.UniqueID);

            Harmony.PatchAll();

            Helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            Helper.Events.Specialized.LoadStageChanged += this.OnLoadStageChanged;
            Helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            Helper.Events.Content.AssetRequested += this.OnAssetRequested;
            Helper.Events.Content.AssetsInvalidated += this.OnAssetsInvalidated;
            Helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
            Helper.Events.Multiplayer.ModMessageReceived += MultiplayerUtils.receiveChatMessage;
            Helper.Events.Multiplayer.ModMessageReceived += MultiplayerUtils.receiveTrigger;

            GameLocation.RegisterTileAction($"{Manifest.UniqueID}_MuseumMenu", MuseumManager.ActionHandler_MuseumMenu);
            GameLocation.RegisterTileAction($"{Manifest.UniqueID}_Rearrange", MuseumManager.ActionHandler_Rearrange);
            GameLocation.RegisterTileAction($"{Manifest.UniqueID}_LostBook", MuseumManager.ActionHandler_LostBook);

            TriggerActionManager.RegisterTrigger($"{Manifest.UniqueID}_MuseumDonation");
            TriggerActionManager.RegisterTrigger($"{Manifest.UniqueID}_MuseumRetrieval");
            TriggerActionManager.RegisterTrigger($"{Manifest.UniqueID}_BookFound");

            TriggerActionManager.RegisterAction($"{Manifest.UniqueID}_DonateItem", TriggerActions.DonateItem);
            TriggerActionManager.RegisterAction($"{Manifest.UniqueID}_ForceDonateItem", TriggerActions.ForceDonateItem);
            TriggerActionManager.RegisterAction($"{Manifest.UniqueID}_RemoveDonation", TriggerActions.RemoveDonation);

            GameStateQuery.Register($"{Manifest.UniqueID}_MUSEUM_DONATIONS", Queries.MUSEUM_DONATIONS);
            GameStateQuery.Register($"{Manifest.UniqueID}_MUSEUM_HAS_ITEM", Queries.MUSEUM_HAS_ITEM);
            GameStateQuery.Register($"{Manifest.UniqueID}_IS_ITEM_DONATED", Queries.IS_ITEM_DONATED);
            GameStateQuery.Register($"{Manifest.UniqueID}_LOST_BOOKS_FOUND", Queries.LOST_BOOKS_FOUND);
            GameStateQuery.Register($"{Manifest.UniqueID}_TOTAL_LOST_BOOKS_FOUND", Queries.TOTAL_LOST_BOOKS_FOUND);
        }

        private static void CreateManagers()
        {
            Utility.ForEachLocation(loc =>
            {
                if (MuseumData.ContainsKey(loc.Name))
                {
                    MuseumManagers.TryAdd(loc.Name, new MuseumManager(loc.Name));
                }

                return true;
            });
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            MuseumManagers.Clear();
            _museumData = null;
            _globalDonatableItems = null;
            _questData = null;
            _lostBookData = null;
            _lostBookLookup = null;
        }

        private void OnLoadStageChanged(object? sender, LoadStageChangedEventArgs e)
        {
            if (e.NewStage is not LoadStage.Loaded) return;

            CreateManagers();
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != Manifest.UniqueID || e.Type != "Spiderbuttons.CMF_LostBook") return;

            var args = e.ReadAs<string[]>();
            var museumId = args[0];
            var booksetId = args[1];

            if (MuseumManagers.TryGetValue(museumId, out var manager))
            {
                manager.IncrementLostBookCount(booksetId);
            }
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Spiderbuttons.CMF/Museums"))
            {
                e.LoadFrom(() => new Dictionary<string, CustomMuseumData>(), AssetLoadPriority.Exclusive);
            }

            if (e.NameWithoutLocale.IsEquivalentTo("Spiderbuttons.CMF/Quests"))
            {
                e.LoadFrom(() => new Dictionary<string, CustomMuseumQuestData>(), AssetLoadPriority.Exclusive);
            }

            if (e.NameWithoutLocale.IsEquivalentTo("Spiderbuttons.CMF/LostBooks"))
            {
                e.LoadFrom(() => new Dictionary<string, List<CustomLostBookData>>(), AssetLoadPriority.Exclusive);
            }
        }

        private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
        {
            if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Spiderbuttons.CMF/Museums")))
            {
                Log.Trace("Invalidating museum data.");
                foreach (var manager in MuseumManagers.Values) manager.TotalPossibleDonations.Clear();
                _museumData = null;
                _globalDonatableItems = null;
            }

            if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Spiderbuttons.CMF/Quests")))
            {
                Log.Trace("Invalidating quest data.");
                _questData = null;
            }

            if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Spiderbuttons.CMF/LostBooks")))
            {
                Log.Trace("Invalidating lost book data.");
                _lostBookData = null;
                _lostBookLookup = null;
            }

            // Any kind of item can be donated to a museum, so... we need to invalidate our caches whenever an item-related asset is changed (':
            // It's not TOO bad though, since the caches are only rebuilt on demand aka when someone visits a museum.
            if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Data/Objects") ||
                                                 name.IsEquivalentTo("Data/Furniture") ||
                                                 name.IsEquivalentTo("Data/Boots") ||
                                                 name.IsEquivalentTo("Data/Hats") ||
                                                 name.IsEquivalentTo("Data/Pants") ||
                                                 name.IsEquivalentTo("Data/Shirts") ||
                                                 name.IsEquivalentTo("Data/Tools") ||
                                                 name.IsEquivalentTo("Data/Trinkets") ||
                                                 name.IsEquivalentTo("Data/Weapons") ||
                                                 name.IsEquivalentTo("Data/Mannequins") ||
                                                 name.IsEquivalentTo("Data/AdditionalWallpaperFlooring") ||
                                                 name.IsEquivalentTo("Data/FloorsAndPaths") ||
                                                 name.IsEquivalentTo("Data/BigCraftables")))
            {
                Log.Trace("Invalidating donatable item lists.");
                foreach (var manager in MuseumManagers.Values) manager.TotalPossibleDonations.Clear();
                _globalDonatableItems = null;
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button is SButton.F2)
            {
                //
            }
        }
    }
}