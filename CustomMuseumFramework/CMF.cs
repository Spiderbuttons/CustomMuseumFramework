using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        private static Dictionary<string, SortedList<MuseumManager, bool>>? _globalDonatableItems;
        
        public static Dictionary<string, SortedList<MuseumManager, bool>> GlobalDonatableItems {
            get
            {
                if (_globalDonatableItems == null) {
                    // use a custom key comparer for SortedDictionary. keys with MuseumData.OverrideDescription set to true should be first
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
            
            GameStateQuery.Register($"{Manifest.UniqueID}_MUSEUM_DONATIONS", Queries.MUSEUM_DONATIONS);
            
            // TODO: Game State Queries
            // TODO: Trigger Action Actions to donate items by code-force?
            // TODO: Allow customization of what happens when you click on an item in the museum.
            // TODO: Debug commands for resetting museums, including the vanilla museum.
            // TODO: Lost books... maybe.
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            MuseumManagers.Clear();
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
                // print a list of GlobalDonatableItems. include what museums they go to and whether or not the item has already been donated to it
                foreach (var item in GlobalDonatableItems)
                {
                    string itemId = item.Key;
                    Log.Alert($"Item: {itemId}");
                    foreach (var museum in item.Value)
                    {
                        Log.Warn($"  {museum.Key.MuseumData.Id}: {museum.Value}");
                    }
                }
            }

            if (e.Button is SButton.F6)
            {
                // log every class that inherits from Item and has an override method getDescription
                var itemTypes = Assembly.GetAssembly(typeof(Item))?.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(Item)))
                    .Where(t => t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Any(m => m.Name == "getDescription" && !m.GetParameters().Any()))
                    .Select(t => t.GetMethod("getDescription"));
                    
                if (itemTypes != null) 
                {
                    Log.Alert("Item Types:");
                    foreach (var itemType in itemTypes)
                    {
                        Log.Alert($"{itemType.DeclaringType}:{itemType.Name}");
                    }
                }
                else
                {
                    Log.Error("Failed to get item types.");
                }
            }
        }
    }
}