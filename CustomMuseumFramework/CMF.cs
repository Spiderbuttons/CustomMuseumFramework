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
                
                MuseumComplete_Owner = @"[LocalizedText Strings\ExtraDialogue:Gunther_MuseumComplete]",
                MuseumComplete_NoOwner = "The museum has been completed.",
                
                NothingToDonate_Owner = @"[LocalizedText Strings\Locations:ArchaeologyHouse_Gunther_NothingToDonate]",
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
                _museumData = null;
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
            // Log.Alert(typeof(CustomMuseum).AssemblyQualifiedName);
            
            if (!Context.IsWorldReady)
                return;

            if (e.Button is not SButton.F5) return;

            if (Game1.currentLocation is not CustomMuseum museum) return;
            museum.TotalPossibleDonations = -1;
            
            if (MuseumData.TryGetValue(museum.Name, out var data))
            {
                Log.Debug($"Museum ID: {data.Id}");
                Log.Debug($"Owner: {data.Owner}");
                Log.Debug($"OwnerTile: {data.OwnerTile}");
                Log.Debug($"RequireOwnerForDonation: {data.RequireOwnerForDonation}");
                Log.Debug($"DonationCriteria:");
                if (data.DonationCriteria.ItemIds != null)
                    Log.Debug($"  ItemIds: {string.Join(", ", data.DonationCriteria.ItemIds)}");
                if (data.DonationCriteria.ContextTags != null)
                    Log.Debug($"  ContextTags: {string.Join(", ", data.DonationCriteria.ContextTags)}");
                if (data.DonationCriteria.Categories != null)
                    Log.Debug($"  Categories: {string.Join(", ", data.DonationCriteria.Categories)}");
                Log.Debug($"Rewards: {string.Join(", ", data.Rewards.Select(r => r.Id))}");
                Log.Debug($"Milestones: {string.Join(", ", data.Milestones)}");
            }
            else
            {
                Log.Error("Museum data not found!");
            }
            
            foreach (var mail in Game1.player.mailReceived)
            {
                Log.Debug(mail);
            }
        }
    }
}