using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using CustomMuseumFramework.Helpers;
using CustomMuseumFramework.Menus;
using CustomMuseumFramework.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Internal;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Triggers;
using xTile.Dimensions;
using xTile.Tiles;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace CustomMuseumFramework;

[XmlType("Mods_Spiderbuttons_CustomMuseum")]
public class CustomMuseum : GameLocation
{
    private int _totalPossibleDonations = -1;

    private readonly NetMutex mutex = new NetMutex();

    [XmlIgnore] private readonly Dictionary<Item, string> _itemToRewardsLookup = new Dictionary<Item, string>();

    public int TotalPossibleDonations
    {
        get
        {
            if (_totalPossibleDonations >= 0) return _totalPossibleDonations;

            _totalPossibleDonations = -1;

            foreach (var type in ItemRegistry.ItemTypes)
            {
                foreach (var item in type.GetAllIds())
                {
                    if (IsItemSuitableForDonation(item, checkDonatedItems: false))
                    {
                        _totalPossibleDonations++;
                    }
                }
            }

            return _totalPossibleDonations;
        }
        internal set => _totalPossibleDonations = value;
    }

    [XmlElement("DonatedItems")]
    public NetVector2Dictionary<string, NetString> DonatedItems { get; } = [];

    public CustomMuseum()
    {
    }

    public CustomMuseum(string mapPath, string name) : base(mapPath, name)
    {
    }

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields.AddField(mutex.NetFields);
        NetFields.AddField(DonatedItems.NetFields);
    }

    public override void TransferDataFromSavedLocation(GameLocation l)
    {
        var savedMuseum = l as CustomMuseum;
        DonatedItems.MoveFrom(savedMuseum?.DonatedItems);
        base.TransferDataFromSavedLocation(l);
    }

    public override void updateEvenIfFarmerIsntHere(GameTime time, bool skipWasUpdatedFlush = false)
    {
        mutex.Update(this);
        base.updateEvenIfFarmerIsntHere(time, skipWasUpdatedFlush);
    }

    private bool HasDonatedItem()
    {
        return DonatedItems.Values.Any();
    }

    private bool HasDonatedItemAt(Vector2 tile)
    {
        return DonatedItems.ContainsKey(tile);
    }

    public bool HasDonatedItem(string? itemId)
    {
        if (itemId is null) return false;

        itemId = ItemRegistry.QualifyItemId(itemId);
        foreach (var pair in DonatedItems.Pairs)
        {
            if (pair.Value == itemId) return true;
        }

        return false;
    }

    public bool IsItemSuitableForDonation(Item? i)
    {
        if (i is null) return false;
        return IsItemSuitableForDonation(i.QualifiedItemId);
    }

    private bool IsItemSuitableForDonation(string? itemId, bool checkDonatedItems = true)
    {
        if (itemId is null) return false;

        itemId = ItemRegistry.QualifyItemId(itemId);
        ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(itemId);
        HashSet<string> tags = ItemContextTagManager.GetBaseContextTags(itemId);

        if (tags.Contains("not_museum_donatable"))
        {
            return false;
        }

        if (checkDonatedItems && HasDonatedItem(itemData.QualifiedItemId))
        {
            return false;
        }

        if (!CMF.MuseumData.TryGetValue(Name, out var museumData))
        {
            Log.Error(
                "No museum data found for this location! Make sure your Museums entry key matches the location ID.");
            return false;
        }

        var donationCriteria = museumData.DonationCriteria;

        if (donationCriteria.ContextTags is not null && donationCriteria.ContextTags.Any(tag => tags.Contains(tag)))
        {
            return true;
        }

        if (donationCriteria.ItemIds is not null && donationCriteria.ItemIds.Contains(itemId))
        {
            return true;
        }

        if (donationCriteria.Categories is not null && donationCriteria.Categories.Contains(itemData.Category))
        {
            return true;
        }

        return false;
    }

    private bool DoesFarmerHaveAnythingToDonate(Farmer who)
    {
        for (int i = 0; i < who.MaxItems; i++)
        {
            if (i < who.Items.Count && IsItemSuitableForDonation(who.Items[i]))
            {
                return true;
            }
        }

        return false;
    }

    protected override void resetLocalState()
    {
        base.resetLocalState();
        // TODO: Mail flag stuff later
        // if (!Game1.player.eventsSeen.Contains("0") && this.doesFarmerHaveAnythingToDonate(Game1.player))
        // {
        //     Game1.player.mailReceived.Add("somethingToDonate");
        // }
        // if (LibraryMuseum.HasDonatedArtifacts())
        // {
        //     Game1.player.mailReceived.Add("somethingWasDonated");
        // }
    }

    public override void cleanupBeforePlayerExit()
    {
        _itemToRewardsLookup.Clear();
        base.cleanupBeforePlayerExit();
    }

    public override bool answerDialogueAction(string? questionAndAnswer, string[] questionParams)
    {
        if (questionAndAnswer is null) return false;

        switch (questionAndAnswer)
        {
            case "Museum_Collect":
                OpenRewardMenu();
                break;
            case "Museum_Donate":
                OpenDonationMenu();
                break;
            case "Museum_Rearrange_Yes":
                OpenRearrangeMenu();
                break;
        }

        return base.answerDialogueAction(questionAndAnswer, questionParams);
    }

    private string GetRewardItemKey(Item item)
    {
        return $"{Name}_MuseumRewardItem_{item.QualifiedItemId}_{item.Stack}";
    }

    public override bool performAction(string[] action, Farmer who, Location tileLocation)
    {
        if (!who.IsLocalPlayer) return base.performAction(action, who, tileLocation);

        CMF.MuseumData.TryGetValue(Name, out var museumData);
        string text = ArgUtility.Get(action, 0);
        if (text.Equals("MuseumMenu"))
        {
            if (museumData is not null && museumData.RequireOwnerForDonation)
            {
                foreach (NPC npc in characters)
                {
                    if (!npc.Name.Equals(museumData.Owner)) continue;
                    if (museumData.OwnerTile is { X: < 0, Y: < 0 })
                    {
                        OpenMuseumDialogueMenu();
                        return true;
                    }

                    if (npc.Tile != museumData.OwnerTile) return false;

                    OpenMuseumDialogueMenu();
                    return true;
                }

                return false;
            }

            OpenMuseumDialogueMenu();
            return true;
        }

        if (text.Equals("Rearrange"))
        {
            if (HasDonatedItem())
            {
                createQuestionDialogue(
                    Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Rearrange"), // TODO: Customizable.
                    createYesNoResponses(), "Museum_Rearrange");
            }

            return true;
        }

        return base.performAction(action, who, tileLocation);
    }

    public List<Item> GetRewardsForPlayer(Farmer player)
    {
        this._itemToRewardsLookup.Clear();
        if (!CMF.MuseumData.TryGetValue(Name, out var museumData))
        {
            Log.Error(
                "No museum data found for this location! Make sure your Museums entry key matches the location ID.");
            return new List<Item>();
        }

        List<CustomMuseumRewardData> museumRewardData = museumData.Rewards;
        Dictionary<string, bool> metRequirements = RewardRequirementsCheck(museumRewardData);
        List<Item> rewards = new List<Item>();
        foreach (CustomMuseumRewardData reward in museumRewardData)
        {
            string id = reward.Id;
            if (!CanCollectReward(reward, id, player, metRequirements))
            {
                continue;
            }

            player.mailReceived.Add(id);
            bool rewardAdded = false;
            if (reward.RewardItems is not null)
            {
                List<Item> items = GetAllAvailableRewards(reward);
                foreach (var item in items)
                {
                    item.specialItem = reward.RewardIsSpecial;
                    if (AddRewardItemIfUncollected(player, rewards, item))
                    {
                        this._itemToRewardsLookup[item] = id;
                        rewardAdded = true;
                    }
                }
            }

            if (!rewardAdded)
            {
                AddNonItemRewards(reward, id, player);
            }
        }

        return rewards;
    }

    private Dictionary<string, bool> RewardRequirementsCheck(List<CustomMuseumRewardData> rewardDataList)
    {
        var results = new Dictionary<string, bool>();

        foreach (var reward in rewardDataList)
        {
            results[reward.Id] = true;
            if (reward.Requirements is null) continue;
            foreach (var requirement in reward.Requirements)
            {
                bool shouldBreak = false;
                if (requirement.ItemId is null && requirement.Category is null && requirement.ContextTag is null)
                {
                    if (requirement.Count == -1)
                    {
                        if (DonatedItems.Count() < TotalPossibleDonations)
                        {
                            results[reward.Id] = false;
                            shouldBreak = true;
                        }
                    }
                    else if (requirement.Count > DonatedItems.Count())
                    {
                        results[reward.Id] = false;
                        shouldBreak = true;
                    }
                }
                else
                {
                    int count = 0;
                    foreach (var item in DonatedItems.Values)
                    {
                        bool hasMatchingId = requirement.ItemId is null || item.Equals(requirement.ItemId);
                        bool hasMatchingCategory = requirement.Category is null ||
                                                   ItemRegistry.GetDataOrErrorItem(item).Category ==
                                                   requirement.Category;
                        bool hasMatchingContextTag = requirement.ContextTag is null ||
                                                     ItemContextTagManager.HasBaseTag(item, requirement.ContextTag);
                        if (hasMatchingId && hasMatchingCategory && hasMatchingContextTag)
                        {
                            count++;
                        }

                        if (count >= requirement.Count)
                        {
                            break;
                        }
                    }

                    if (count < requirement.Count)
                    {
                        results[reward.Id] = false;
                        shouldBreak = true;
                    }
                }

                if (shouldBreak)
                {
                    break;
                }
            }
        }

        return results;
    }

    private void AddNonItemRewards(CustomMuseumRewardData data, string rewardId, Farmer player)
    {
        if (data.FlagOnCompletion)
        {
            player.mailReceived.Add(rewardId);
        }

        if (data.Actions is null)
        {
            return;
        }

        foreach (string action in data.Actions)
        {
            if (!TriggerActionManager.TryRunAction(action, out var error, out _))
            {
                Log.Error(
                    $"Custom museum {Name} reward with ID '{rewardId}' ignored invalid action '{action}': {error}");
            }
        }
    }

    private bool AddRewardItemIfUncollected(Farmer player, List<Item> rewards, Item rewardItem)
    {
        if (!player.mailReceived.Contains(GetRewardItemKey(rewardItem)))
        {
            rewards.Add(rewardItem);
            return true;
        }

        return false;
    }

    private bool HighlightCollectableRewards(Item item)
    {
        return Game1.player.couldInventoryAcceptThisItem(item);
    }

    private void OpenRearrangeMenu()
    {
        if (!mutex.IsLocked())
        {
            mutex.RequestLock(delegate
            {
                Game1.activeClickableMenu = new CustomMuseumMenu(InventoryMenu.highlightNoItems)
                {
                    exitFunction = mutex.ReleaseLock
                };
            });
        }
    }

    private void OpenRewardMenu()
    {
        Game1.activeClickableMenu = new ItemGrabMenu(GetRewardsForPlayer(Game1.player), reverseGrab: false,
            showReceivingMenu: true, HighlightCollectableRewards, null, "Rewards", OnRewardCollected,
            snapToBottom: false, canBeExitedWithKey: true, playRightClickSound: false, allowRightClick: false,
            showOrganizeButton: false, 0, null, -1, this, allowExitWithHeldItem: true);
    }

    private void OpenDonationMenu()
    {
        mutex.RequestLock(delegate
        {
            Game1.activeClickableMenu = new CustomMuseumMenu(IsItemSuitableForDonation)
            {
                exitFunction = OnDonationMenuClosed
            };
        });
    }

    private void OnDonationMenuClosed()
    {
        mutex.ReleaseLock();
        GetRewardsForPlayer(Game1.player);
    }

    private void OnRewardCollected(Item? item, Farmer who)
    {
        if (item is null)
        {
            return;
        }

        if (_itemToRewardsLookup.TryGetValue(item, out var rewardId))
        {
            if (CMF.MuseumData.TryGetValue(Name, out var museumData))
            {
                AddNonItemRewards(museumData.Rewards.First(r => r.Id == rewardId), rewardId, who);
            }

            _itemToRewardsLookup.Remove(item);
        }

        if (!who.hasOrWillReceiveMail(GetRewardItemKey(item)))
        {
            who.mailReceived.Add(GetRewardItemKey(item));
        }
    }

    private void OpenMuseumDialogueMenu()
    {
        if (DoesFarmerHaveAnythingToDonate(Game1.player) && !this.mutex.IsLocked())
        {
            Response[] choice = ((GetRewardsForPlayer(Game1.player).Count <= 0)
                ? new Response[2]
                {
                    new Response("Donate",
                        Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Donate")),
                    new Response("Leave", Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Leave"))
                }
                : new Response[3]
                {
                    new Response("Donate",
                        Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Donate")),
                    new Response("Collect",
                        Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Collect")),
                    new Response("Leave", Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Leave"))
                });
            createQuestionDialogue("", choice, "Museum");
        }
        else if (GetRewardsForPlayer(Game1.player).Count > 0)
        {
            createQuestionDialogue("", new Response[2]
            {
                new Response("Collect",
                    Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Collect")),
                new Response("Leave", Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Leave"))
            }, "Museum");
        }
        else if (DoesFarmerHaveAnythingToDonate(Game1.player) && mutex.IsLocked())
        {
            if (!CMF.MuseumData.TryGetValue(Name, out var museumData) || museumData.Owner is null)
            {
                Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\UI:NPC_Busy",
                    "The museum")); // TODO: This needs i18n.
            }
            else
            {
                Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\UI:NPC_Busy",
                    Game1.RequireCharacter(museumData.Owner).displayName));
            }
        }
        else
        {
            NPC? owner = null;
            if (CMF.MuseumData.TryGetValue(Name, out var museumData))
            {
                owner = Game1.getCharacterFromName(museumData.Owner);
            }

            // TODO: Make these customizable. Also, check to make sure the owner is actually around first, if they exist.
            if (DonatedItems.Count() >= TotalPossibleDonations)
            {
                foreach (var item in DonatedItems.Pairs)
                {
                    Log.Debug($"Item: {item.Key} - {item.Value}");
                }

                Game1.DrawDialogue(new Dialogue(owner, "Data\\ExtraDialogue:Gunther_MuseumComplete",
                    Game1.parseText(Game1.content.LoadString("Data\\ExtraDialogue:Gunther_MuseumComplete"))));
            }
            else if (Game1.player.mailReceived.Contains("artifactFound"))
            {
                Game1.DrawDialogue(new Dialogue(owner, "Data\\ExtraDialogue:Gunther_NothingToDonate",
                    Game1.parseText(Game1.content.LoadString("Data\\ExtraDialogue:Gunther_NothingToDonate"))));
            }
            else
            {
                Game1.DrawDialogue(owner,
                    "Data\\ExtraDialogue:Gunther_NothingToDonate"); // TODO: NoDonationsFound instead of NothingToDonate
            }
        }
    }

    public override bool checkAction(Location tileLocation, xTile.Dimensions.Rectangle viewport, Farmer who)
    {
        if (DonatedItems.TryGetValue(new Vector2(tileLocation.X, tileLocation.Y), out var itemId) ||
            DonatedItems.TryGetValue(new Vector2(tileLocation.X, tileLocation.Y - 1), out itemId))
        {
            ParsedItemData data = ItemRegistry.GetDataOrErrorItem(itemId);
            Game1.drawObjectDialogue(Game1.parseText(" - " + data.DisplayName + " - " + "^" + data.Description));
            return true;
        }

        return base.checkAction(tileLocation, viewport, who);
    }

    public bool IsTileSuitableForMuseumItem(int x, int y)
    {
        if (!HasDonatedItemAt(new Vector2(x, y)))
        {
            int indexOfBuildingsLayer = getTileIndexAt(new Point(x, y), "Buildings");
            if (indexOfBuildingsLayer is 1073 or 1074 or 1072 or 1237 or 1238) // TODO: Check for the right tilesheetId
            {
                return true;
            }

            if (IsTileDonationSpot(x, y))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTileDonationSpot(int x, int y)
    {
        Tile tile = map.RequireLayer("Buildings")
            .PickTile(new Location(x * Game1.tileSize, y * Game1.tileSize), Game1.viewport.Size);
        if (tile == null || !tile.Properties.TryGetValue("Spiderbuttons.CustomMuseumFramework", out string value))
        {
            value = doesTileHaveProperty(x, y, "Spiderbuttons.CustomMuseumFramework", "Buildings");
        }

        return (value is not null && value.Equals("DonationSpot", StringComparison.OrdinalIgnoreCase));
    }

    private bool CanCollectReward(CustomMuseumRewardData reward, string rewardId, Farmer player,
        Dictionary<string, bool> metRequirements)
    {
        if (reward.FlagOnCompletion && player.mailReceived.Contains(rewardId))
        {
            if (reward.RewardItems is not null)
            {
                if (GetFirstAvailableReward(reward) is not null)
                {
                    return true;
                }
            }

            return false;
        }

        if (!metRequirements[rewardId]) return false;
        if (reward.RewardItems is not null)
        {
            if (reward.RewardIsSpecial)
            {
                var items = GetAllAvailableRewards(reward);
                foreach (var item in items)
                {
                    ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(item.QualifiedItemId);
                    if (((itemData.HasTypeId("(F)") || itemData.HasTypeBigCraftable())
                            ? player.specialBigCraftables
                            : player.specialItems).Contains(itemData.ItemId))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private List<Item> GetAllAvailableRewards(CustomMuseumRewardData rewardData)
    {
        var results = new List<Item>();
        if (rewardData.RewardItems is null) return results;
        foreach (var entry in rewardData.RewardItems)
        {
            var randomSeed = Game1.hash.GetDeterministicHashCode(rewardData.Id + entry.Id);
            var museumRandom = Utility.CreateRandom(randomSeed, Game1.uniqueIDForThisGame);
            ItemQueryContext itemQueryContext = new ItemQueryContext(this, Game1.player, museumRandom,
                $"{NameOrUniqueName} > GetAllAvailableRewards");
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                Log.Error($"Ignored custom museum {Name} reward item entry with no Id field.");
                continue;
            }

            if (!GameStateQuery.CheckConditions(entry.Condition, this, null, null, null, museumRandom))
            {
                continue;
            }

            bool error = false;
            Item? result = ItemQueryResolver.TryResolve(entry, itemQueryContext, ItemQuerySearchMode.FirstOfTypeItem,
                avoidRepeat: false, avoidItemIds: null, formatItemId: null,
                logError: delegate(string query, string message)
                {
                    error = true;
                    Log.Error($"Failed parsing item query '{query}': {message}");
                }).FirstOrDefault()?.Item as Item;
            if (error || result is null) continue;
            results.Add(result);
        }

        return results;
    }

    private Item? GetFirstAvailableReward(CustomMuseumRewardData rewardData)
    {
        if (rewardData.RewardItems is null) return null;
        var museumRandom = Utility.CreateDaySaveRandom();
        ItemQueryContext itemQueryContext = new ItemQueryContext(this, Game1.player, museumRandom,
            $"{NameOrUniqueName} > GetFirstAvailableReward");
        foreach (var entry in rewardData.RewardItems)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                Log.Error($"Ignored custom museum {Name} reward item entry with no Id field.");
                continue;
            }

            if (!GameStateQuery.CheckConditions(entry.Condition, this, null, null, null, museumRandom))
            {
                continue;
            }

            bool error = false;
            Item result = ItemQueryResolver.TryResolveRandomItem(entry, itemQueryContext, avoidRepeat: false, null,
                null, null,
                delegate(string query, string message)
                {
                    error = true;
                    Log.Error($"Failed parsing item query '{query}': {message}");
                });
            if (error) continue;
            return result;
        }

        return null;
    }

    private Rectangle GetMuseumDonationBounds()
    {
        return new Rectangle(26, 5, 22, 13); // TODO: ?
    }

    public Vector2 GetFreeDonationSpot()
    {
        Rectangle bounds = GetMuseumDonationBounds();
        for (int x = bounds.X; x <= bounds.Right; x++)
        {
            for (int y = bounds.Y; y <= bounds.Bottom; y++)
            {
                if (IsTileSuitableForMuseumItem(x, y))
                {
                    return new Vector2(x, y);
                }
            }
        }

        return new Vector2(26f, 5f);
    }

    public Vector2 FindMuseumPieceLocationInDirection(Vector2 startingPoint, int direction, int distanceToCheck = 8,
        bool ignoreExistingItems = true)
    {
        Vector2 checkTile = startingPoint;
        Vector2 offset = Vector2.Zero;
        switch (direction)
        {
            case 0:
                offset = new Vector2(0f, -1f);
                break;
            case 1:
                offset = new Vector2(1f, 0f);
                break;
            case 2:
                offset = new Vector2(0f, 1f);
                break;
            case 3:
                offset = new Vector2(-1f, 0f);
                break;
        }

        for (int j = 0; j < distanceToCheck; j++)
        {
            for (int i = 0; i < distanceToCheck; i++)
            {
                checkTile += offset;
                if (IsTileSuitableForMuseumItem((int)checkTile.X, (int)checkTile.Y) ||
                    (!ignoreExistingItems && HasDonatedItemAt(checkTile)))
                {
                    return checkTile;
                }
            }

            checkTile = startingPoint;
            int sign = ((j % 2 != 0) ? 1 : (-1));
            switch (direction)
            {
                case 0:
                case 2:
                    // ReSharper disable once PossibleLossOfFraction
                    checkTile.X += sign * (j / 2 + 1);
                    break;
                case 1:
                case 3:
                    // ReSharper disable once PossibleLossOfFraction
                    checkTile.Y += sign * (j / 2 + 1);
                    break;
            }
        }

        return startingPoint;
    }

    public override void drawAboveAlwaysFrontLayer(SpriteBatch b)
    {
        foreach (TemporaryAnimatedSprite t in temporarySprites)
        {
            if (t.layerDepth >= 1f)
            {
                t.draw(b);
            }
        }
    }

    public override void draw(SpriteBatch b)
    {
        base.draw(b);
        foreach (KeyValuePair<Vector2, string> v in DonatedItems.Pairs)
        {
            ParsedItemData data = ItemRegistry.GetDataOrErrorItem(v.Value);
            b.Draw(Game1.shadowTexture, Game1.GlobalToLocal(Game1.viewport, v.Key * 64f + new Vector2(32f, 52f)),
                Game1.shadowTexture.Bounds, Color.White, 0f,
                new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y), 4f,
                SpriteEffects.None, (v.Key.Y * 64f - 2f) / 10000f);

            var texture = data.GetTexture();
            var sourceRect = data.GetSourceRect();
            int textureOffset = data.GetSourceRect().Height - 16;
            b.Draw(texture, Game1.GlobalToLocal(Game1.viewport, v.Key * 64f) + new Vector2(0, -textureOffset * 4),
                sourceRect,
                Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, (v.Key.Y + 2f) * 64f / 10000f);
        }
    }
}