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
using SpaceShared.APIs;
using StardewValley.TokenizableStrings;

namespace CustomMuseumFramework
{
    internal sealed class CMF : Mod
    {
        internal static IModHelper ModHelper { get; set; } = null!;
        internal static IMonitor ModMonitor { get; set; } = null!;
        
        internal static IManifest Manifest { get; set; } = null!;
        internal static Harmony Harmony { get; set; } = null!;
        internal static ISpaceCoreApi? SpaceCoreAPI { get; set; } = null!;

        private static Dictionary<string, CustomMuseumData>? _museumData = null;

        public static Dictionary<string, CustomMuseumData> MuseumData
        {
            get
            {
                return _museumData ??= Game1.content.Load<Dictionary<string, CustomMuseumData>>("Spiderbuttons.CustomMuseumFramework/Museums");
            }
        }

        private static Dictionary<string, HashSet<CustomMuseum>>? _globalDonatableItems = null;
        
        public static Dictionary<string, HashSet<CustomMuseum>>? GlobalDonatableItems {
            get
            {
                if (_globalDonatableItems == null) {
                    _globalDonatableItems = new();
                    foreach (var museum in MuseumData.Values)
                    {
                        var loc = Game1.RequireLocation<CustomMuseum>(museum.Id);
                        foreach (var itemId in loc.TotalPossibleDonations)
                        {
                            if (!_globalDonatableItems.ContainsKey(itemId))
                                _globalDonatableItems[itemId] = [];
                            _globalDonatableItems[itemId].Add(loc);
                            Log.Alert("Added " + itemId + " to " + museum.Id);
                        }
                    }
                }
                
                return _globalDonatableItems;
            }
            set => _globalDonatableItems = value;
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

            Helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            Helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            Helper.Events.Content.AssetRequested += this.OnAssetRequested;
            Helper.Events.Content.AssetsInvalidated += this.OnAssetsInvalidated;
            Helper.Events.Multiplayer.ModMessageReceived += MultiplayerUtils.receiveChatMessage;
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Spiderbuttons.CustomMuseumFramework/Museums")) {
                e.LoadFrom(() => new Dictionary<string, CustomMuseumData>(), AssetLoadPriority.Exclusive);
            }

            if (e.NameWithoutLocale.IsEquivalentTo("Strings\\UI"))
            {
                e.Edit(asset =>
                {
                    var editor = asset.AsDictionary<string, string>();
                    // TODO: These need i18n.
                    editor.Data[$"Chat_{ModManifest.UniqueID}_OnDonation"] = "{0} donated '{1}' to the {2} museum.";
                    editor.Data[$"Chat_{ModManifest.UniqueID}_OnMilestone"] = "{0} Farm has donated {1} pieces to the {2} museum.";
                    editor.Data[$"Chat_{ModManifest.UniqueID}_OnCompletion"] = "{0} Farm has completed the {1} museum collection.";
                });
            }
        }
        
        public void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e) {
            if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo("Spiderbuttons.CustomMuseumFramework/Museums"))) {
                Log.Debug("Invalidating museum data.");
                foreach (var museum in MuseumData)
                {
                    Game1.RequireLocation<CustomMuseum>(museum.Key).TotalPossibleDonations.Clear();
                }
                _museumData = null;
                _globalDonatableItems = null;
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            SpaceCoreAPI = ModHelper.ModRegistry.GetApi<ISpaceCoreApi>("spacechase0.SpaceCore");
            if (SpaceCoreAPI == null)
            {
                Log.Error("SpaceCore not found! Custom Museum Framework requires SpaceCore to be installed or it will break your saves!");
                return;
            }
            SpaceCoreAPI.RegisterSerializerType(typeof(CustomMuseum));
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button is SButton.F2)
            {
                // log all the global donatables and the names of the museums they go to
                if (GlobalDonatableItems != null)
                    Log.Alert(GlobalDonatableItems.Count);
                    foreach (var pair in GlobalDonatableItems)
                    {
                        Log.Debug($"{pair.Key}: {string.Join(", ", pair.Value.Select(m => m.Name))}");
                    }
            }

            if (e.Button is SButton.F5 && Game1.currentLocation is CustomMuseum museum)
            {
                Log.Alert(museum.TotalPossibleDonations.Join(null, ", "));
            }

            if (e.Button is SButton.F6)
            {
                Helper.GameContent.InvalidateCache("Spiderbuttons.CustomMuseumFramework/Museums");
            }
        }
    }
}