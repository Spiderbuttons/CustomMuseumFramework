using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using CustomMuseumFramework.Helpers;
using CustomMuseumFramework.Models;
using StardewValley.Objects;
using StardewValley.TokenizableStrings;

namespace CustomMuseumFramework
{
    internal sealed class CMF : Mod
    {
        internal static IModHelper ModHelper { get; set; } = null!;
        internal static IMonitor ModMonitor { get; set; } = null!;
        
        internal static IManifest Manifest { get; set; } = null!;
        internal static Harmony Harmony { get; set; } = null!;

        private static Dictionary<string, CustomMuseumData>? _museumData = null;

        public static Dictionary<string, CustomMuseumData> MuseumData
        {
            get
            {
                return _museumData ??= Game1.content.Load<Dictionary<string, CustomMuseumData>>("Spiderbuttons.CMF/Museums");
            }
        }
        
        public static Dictionary<string, MuseumManager> MuseumManagers { get; } = new();

        private static Dictionary<string, HashSet<MuseumManager>>? _globalDonatableItems = null;
        
        public static Dictionary<string, HashSet<MuseumManager>>? GlobalDonatableItems {
            get
            {
                if (_globalDonatableItems == null) {
                    _globalDonatableItems = new Dictionary<string, HashSet<MuseumManager>>();
                    foreach (var museum in MuseumManagers.Values)
                    {
                        foreach (var itemId in museum.TotalPossibleDonations)
                        {
                            if (!_globalDonatableItems.ContainsKey(itemId))
                                _globalDonatableItems[itemId] = [];
                            _globalDonatableItems[itemId].Add(museum);
                        }
                    }
                }
                
                return _globalDonatableItems;
            }
        }

        // TODO: Donatable items need description text. Keep a dictionary of item ids and the museums they go with.
        
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
                NoDonations_NoOwner = "The museum has nothing on display right now."
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
            if (e.NameWithoutLocale.IsEquivalentTo("Spiderbuttons.CMF/Museums")) {
                e.LoadFrom(() => new Dictionary<string, CustomMuseumData>(), AssetLoadPriority.Exclusive);
            }
        }
        
        public void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e) {
            if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Spiderbuttons.CMF/Museums"))) {
                Log.Debug("Invalidating museum data.");
                foreach (var manager in MuseumManagers.Values)
                {
                    manager.TotalPossibleDonations.Clear();
                }
                _museumData = null;
                _globalDonatableItems = null;
            }
            else
            {
                foreach (var name in e.NamesWithoutLocale)
                {
                    Log.Alert(name.BaseName);
                }
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button is SButton.F2)
            {
                // // log all the global donatables and the names of the museums they go to
                if (GlobalDonatableItems != null)
                {
                    Log.Alert(GlobalDonatableItems.Count);
                    foreach (var pair in GlobalDonatableItems)
                    {
                        Log.Debug($"{pair.Key}: {string.Join(", ", pair.Value.Select(m => m.Museum.Name))}");
                    }
                }
            }

            if (e.Button is SButton.F6)
            {
                // CMF.MuseumManagers[Game1.currentLocation.Name].Mutex.ReleaseLock();
                Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Game1.currentLocation.Name}").RemoveRange(0, 2);
            }
        }
    }
}