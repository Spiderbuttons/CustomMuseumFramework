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
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Spiderbuttons.CustomMuseumFramework/Museums")) {
                e.LoadFrom(() => new Dictionary<string, CustomMuseumData>(), AssetLoadPriority.Exclusive);
            }

            if (e.NameWithoutLocale.IsEquivalentTo("Strings\\UI"))
            {
                Log.Alert("editing UI strings");
                e.Edit(asset =>
                {
                    var editor = asset.AsDictionary<string, string>();
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
            Log.Alert("Found SpaceCore API!");
            SpaceCoreAPI.RegisterSerializerType(typeof(CustomMuseum));
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (e.Button is not SButton.F5) return;

            var mus = Game1.currentLocation as CustomMuseum;
            Log.Debug(mus.TotalPossibleDonations);
        }
    }
}