using System;
using System.Collections.Generic;
using System.Linq;
using CustomMuseumFramework.GameLocations;
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
            Harmony = new Harmony(ModManifest.UniqueID);

            Harmony.PatchAll();

            Helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            Helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Spiderbuttons.CustomMuseumFramework/Museums")) {
                e.LoadFrom(() => new Dictionary<string, CustomMuseumData>(), AssetLoadPriority.Exclusive);
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
        }
    }
}