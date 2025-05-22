using System;
using System.Collections.Generic;
using System.Linq;
using CustomMuseumFramework.Commands;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using CustomMuseumFramework.Helpers;
using CustomMuseumFramework.Models;
using StardewValley.Triggers;

namespace CustomMuseumFramework
{
    internal sealed class CMF : Mod
    {
        internal static IModHelper ModHelper { get; private set; } = null!;
        internal static IMonitor ModMonitor { get; private set; } = null!;
        
        internal static IManifest Manifest { get; private set; } = null!;
        private static Harmony Harmony { get; set; } = null!;

        private static Dictionary<string, CustomMuseumData>? _museumData;

        public static Dictionary<string, CustomMuseumData> MuseumData
        {
            get
            {
                return _museumData ??= Game1.content.Load<Dictionary<string, CustomMuseumData>>("Spiderbuttons.CMF/Museums");
            }
        }

        private static Dictionary<string, CustomMuseumQuestData>? _questData;
        
        public static Dictionary<string, CustomMuseumQuestData> QuestData
        {
            get
            {
                return _questData ??= Game1.content.Load<Dictionary<string, CustomMuseumQuestData>>("Spiderbuttons.CMF/Quests");
            }
        }

        private static Dictionary<string, MuseumManager>? _lostBooks;
        
        public static Dictionary<string, MuseumManager> LostBooks
        {
            get
            {
                if (_lostBooks == null)
                {
                    _lostBooks = new Dictionary<string, MuseumManager>();
                    foreach (var museum in MuseumManagers.Values)
                    {
                        foreach (var book in museum.MuseumData.LostBooks)
                        {
                            _lostBooks.TryAdd(book.ItemId, museum);
                        }
                    }
                }

                return _lostBooks;
            }
        }

        private static Dictionary<string, SortedList<MuseumManager, bool>>? _globalDonatableItems;
        
        public static Dictionary<string, SortedList<MuseumManager, bool>> GlobalDonatableItems {
            get
            {
                if (_globalDonatableItems == null) {
                    _globalDonatableItems = new Dictionary<string, SortedList<MuseumManager, bool>>();
                    foreach (var museum in MuseumManagers.Values)
                    {
                        foreach (var itemId in museum.TotalPossibleDonations)
                        {
                            if (!_globalDonatableItems.ContainsKey(itemId))
                                _globalDonatableItems[itemId] = new SortedList<MuseumManager, bool>(new MuseumManagerComparer());
                            _globalDonatableItems[itemId].TryAdd(museum, museum.HasDonatedItem(itemId));
                        }
                    }
                }
                
                return _globalDonatableItems;
            }
        }
        
        public static Dictionary<string, MuseumManager> MuseumManagers { get; } = new();
        
        // TODO: These need i18n.
        public static readonly MuseumStrings DefaultStrings =
            new()
            {
                OnDonation = "{0} donated '{1}' to the {2} museum.",
                OnMilestone = "{0} Farm has donated {1} pieces to the {2} museum.",
                OnCompletion = "{0} Farm has completed the {1} museum collection.",
                
                MenuDonate = @"[LocalizedText Strings\Locations:ArchaeologyHouse_Gunther_Donate]",
                MenuCollect = @"[LocalizedText Strings\Locations:ArchaeologyHouse_Gunther_Collect]",
                MenuRearrange = @"[LocalizedText Strings\Locations:ArchaeologyHouse_Rearrange]",
                MenuRetrieve = "Retrieve",
                
                Busy_Owner = @"[LocalizedText Strings\UI:NPC_Busy]",
                Busy_NoOwner = "Someone else is donating to the museum right now.",
                
                MuseumComplete_Owner = @"[LocalizedText Data\ExtraDialogue:Gunther_MuseumComplete]",
                MuseumComplete_NoOwner = "The museum has been completed.",
                
                NothingToDonate_Owner = @"[LocalizedText Data\ExtraDialogue:Gunther_NothingToDonate]",
                NothingToDonate_NoOwner = "You have nothing to donate to the museum.",
                
                NoDonations_Owner = "Welcome to the {0} museum! We don't have anything on display right now.",
                NoDonations_NoOwner = "The museum has nothing on display right now.",
                
                CanBeDonated = "{0} would be interested in this."
            };

        public override void Entry(IModHelper helper)
        {
            ModHelper = helper;
            ModMonitor = Monitor;
            Manifest = ModManifest;
            Harmony = new Harmony(ModManifest.UniqueID);

            Harmony.PatchAll();
            
            Helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            Helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            Helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            Helper.Events.Content.AssetRequested += this.OnAssetRequested;
            Helper.Events.Content.AssetsInvalidated += this.OnAssetsInvalidated;
            Helper.Events.Multiplayer.ModMessageReceived += MultiplayerUtils.receiveChatMessage;
            Helper.Events.Multiplayer.ModMessageReceived += MultiplayerUtils.receiveTrigger;
            
            TriggerActionManager.RegisterTrigger($"{Manifest.UniqueID}_MuseumDonation");
            TriggerActionManager.RegisterTrigger($"{Manifest.UniqueID}_MuseumRetrieval");
            
            TriggerActionManager.RegisterAction($"{Manifest.UniqueID}_DonateItem", TriggerActions.DonateItem);
            TriggerActionManager.RegisterAction($"{Manifest.UniqueID}_ForceDonateItem", TriggerActions.ForceDonateItem);
            TriggerActionManager.RegisterAction($"{Manifest.UniqueID}_RemoveDonation", TriggerActions.RemoveDonation);
            
            GameStateQuery.Register($"{Manifest.UniqueID}_MUSEUM_DONATIONS", Queries.MUSEUM_DONATIONS);
            GameStateQuery.Register($"{Manifest.UniqueID}_MUSEUM_HAS_ITEM", Queries.MUSEUM_HAS_ITEM);
            GameStateQuery.Register($"{Manifest.UniqueID}_IS_ITEM_DONATED", Queries.IS_ITEM_DONATED);

            Helper.ConsoleCommands.Add("cmf", "Starts a Custom Museum Framework command.",
                (_, args) => CommandHandler.Handle(args));
            
            // TODO: Lost book GSQs.
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            MuseumManagers.Clear();
            _museumData = null;
            _globalDonatableItems = null;
            _lostBooks = null;
            _questData = null;
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            Utility.ForEachLocation(loc =>
            {
                if (MuseumData.ContainsKey(loc.Name))
                {
                    MuseumManagers.TryAdd(loc.Name, new MuseumManager(loc));
                }
                
                return true;
            });
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
        }
        
        private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
        {
            if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Spiderbuttons.CMF/Museums")))
            {
                Log.Trace("Invalidating museum data.");
                foreach (var manager in MuseumManagers.Values) manager.TotalPossibleDonations.Clear();
                _museumData = null;
                _globalDonatableItems = null;
                _lostBooks = null;
            }
            
            if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Spiderbuttons.CMF/Quests")))
            {
                Log.Trace("Invalidating quest data.");
                _questData = null;
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button is SButton.F2)
            {
                //
            }

            if (e.Button is SButton.F6)
            {
                //
            }
        }
    }
}