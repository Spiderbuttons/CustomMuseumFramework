using System;
using System.Collections.Generic;
using System.Linq;
using CustomMuseumFramework.Helpers;
using CustomMuseumFramework.Menus;
using CustomMuseumFramework.Models;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Internal;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.TokenizableStrings;
using StardewValley.Triggers;
using xTile.Dimensions;
using xTile.Tiles;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace CustomMuseumFramework;

public class MuseumManager
{
    public GameLocation Museum { get; }

    public CustomMuseumData MuseumData
    {
        get
        {
            if (CMF.MuseumData.TryGetValue(Museum.Name, out var data)) return data;
            throw new KeyNotFoundException(
                $"No museum data found for location '{Museum.Name}.' Make sure your Spiderbuttons.CMF/Museums entry key matches your location key. Please fix this error before proceeding any further!");
        }
    }

    public readonly Dictionary<Item, string> _itemToRewardsLookup = new();

    private readonly HashSet<string> _totalPossibleDonations = [];

    public HashSet<string> TotalPossibleDonations
    {
        get
        {
            if (_totalPossibleDonations.Count > 0) return _totalPossibleDonations;

            CalculateDonations();
            return _totalPossibleDonations;
        }
    }
    
    public Dictionary<Vector2, string> DonatedItems
    {
        get
        {
            var inv = Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}");
            var dict = new Dictionary<Vector2, string>();
            foreach (var item in inv)
            {
                if (item is null) continue;
                if (item.modData.TryGetValue("CMF_Position", out var pos) &&
                    ArgUtility.TryGetVector2(pos.Split(' '), 0, out var v, out _, true))
                {
                    dict[v] = item.QualifiedItemId;
                }
            }
            
            return dict;
        }
    }

    public NetMutex Mutex =>
        Game1.player.team.GetOrCreateGlobalInventoryMutex($"{CMF.Manifest.UniqueID}_{Museum.Name}");
    
    public MuseumManager(GameLocation location)
    {
        Museum = location;
        CalculateDonations();
    }

    private void CalculateDonations()
    {
        _totalPossibleDonations.Clear();
        foreach (var type in ItemRegistry.ItemTypes)
        {
            foreach (var item in type.GetAllIds())
            {
                if (IsItemSuitableForDonation($"{type.Identifier}{item}", checkDonatedItems: false))
                {
                    _totalPossibleDonations.Add($"{type.Identifier}{item}");
                }
            }
        }
    }

    public bool HasDonatedItem()
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
        foreach (var pair in DonatedItems)
        {
            if (pair.Value == itemId) return true;
        }

        return false;
    }

    public bool DonateItem(Vector2 location, string itemId)
    {
        Item item = ItemRegistry.Create(itemId);
        item.modData["CMF_Position"] = $"{location.X} {location.Y}";
        return DonateItem(item);
    }

    private bool DonateItem(Item? item)
    {
        if (item is null) return false;
        Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}").Add(item);
        return true;
    }

    public void RemoveItem(Vector2 v)
    {
        var inv = Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}");
        int indexToRemove = -1;
        for (int i = 0; i < inv.Count; i++)
        {
            if (inv[i] is null) continue;
            if (inv[i].modData.TryGetValue("CMF_Position", out var pos) &&
                ArgUtility.TryGetVector2(pos.Split(' '), 0, out var posV, out _, true) && posV == v)
            {
                Log.Alert("Found");
                indexToRemove = i;
                break;
            }
        }

        if (indexToRemove != -1) inv.RemoveAt(indexToRemove);
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

        var donationCriteria = MuseumData.DonationCriteria;

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

    private string GetRewardItemKey(Item item)
    {
        return $"{Museum.Name}_MuseumRewardItem_{item.QualifiedItemId}_{item.Stack}";
    }

    public List<Item> GetRewardsForPlayer(Farmer player)
    {
        _itemToRewardsLookup.Clear();

        List<CustomMuseumRewardData> museumRewardData = MuseumData.Rewards;
        Dictionary<string, bool> metRequirements = RewardRequirementsCheck(museumRewardData);
        List<Item> rewards = new List<Item>();
        foreach (CustomMuseumRewardData reward in museumRewardData)
        {
            string id = reward.Id;
            if (!CanCollectReward(reward, id, player, metRequirements))
            {
                continue;
            }

            player.mailReceived.Add($"{Museum.Name}_MuseumReward_{id}");
            bool rewardAdded = false;
            if (reward.RewardItems is not null)
            {
                List<Item> items = GetAllAvailableRewards(reward);
                foreach (var item in items)
                {
                    item.specialItem = reward.RewardIsSpecial;
                    if (AddRewardItemIfUncollected(player, rewards, item))
                    {
                        _itemToRewardsLookup[item] = id;
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
                if (requirement.ItemIds is null or [] && requirement.Categories is null or [] &&
                    requirement.ContextTags is null or [])
                {
                    if (requirement.Count == -1)
                    {
                        if (DonatedItems.Count() < TotalPossibleDonations.Count)
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
                    foreach (var donations in DonatedItems.Values)
                    {
                        var item = ItemRegistry.Create(donations);
                        bool hasMatchingId = requirement.ItemIds is null ||
                                             requirement.ItemIds.Contains(item.QualifiedItemId);
                        bool hasMatchingCategory = requirement.Categories is null ||
                                                   requirement.Categories.Contains(item.Category);
                        bool hasMatchingContextTag = requirement.ContextTags is null ||
                                                     ItemContextTagManager.DoAnyTagsMatch(requirement.ContextTags,
                                                         item.GetContextTags());

                        count += requirement.MatchType switch
                        {
                            MatchType.Any when hasMatchingId || hasMatchingCategory || hasMatchingContextTag => 1,
                            MatchType.All when hasMatchingId && hasMatchingCategory && hasMatchingContextTag => 1,
                            _ => 0
                        };

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

    private void AddNonItemRewards(CustomMuseumRewardData? data, string rewardId, Farmer player)
    {
        if (data is null) return;
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
                    $"Custom museum {Museum.Name} reward with ID '{rewardId}' ignored invalid action '{action}': {error}");
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

    private static bool HighlightCollectableRewards(Item item)
    {
        return Game1.player.couldInventoryAcceptThisItem(item);
    }

    public void OpenRearrangeMenu()
    {
        if (!Mutex.IsLocked())
        {
            Mutex.RequestLock(delegate
            {
                Game1.activeClickableMenu = new CustomMuseumMenu(InventoryMenu.highlightNoItems)
                {
                    exitFunction = Mutex.ReleaseLock
                };
            });
        }
    }

    public void OpenRewardMenu()
    {
        Game1.activeClickableMenu = new ItemGrabMenu(GetRewardsForPlayer(Game1.player), reverseGrab: false,
            showReceivingMenu: true, HighlightCollectableRewards, null, "Rewards", OnRewardCollected,
            snapToBottom: false, canBeExitedWithKey: true, playRightClickSound: false, allowRightClick: false,
            showOrganizeButton: false, 0, null, -1, this, allowExitWithHeldItem: true);
    }

    public void OpenDonationMenu()
    {
        Mutex.RequestLock(delegate
        {
            Game1.activeClickableMenu = new CustomMuseumMenu(IsItemSuitableForDonation)
            {
                exitFunction = OnDonationMenuClosed
            };
        });
    }

    private void OnDonationMenuClosed()
    {
        Mutex.ReleaseLock();
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
            AddNonItemRewards(MuseumData.Rewards.First(r => r.Id == rewardId), rewardId, who);

            _itemToRewardsLookup.Remove(item);
        }

        if (!who.hasOrWillReceiveMail(GetRewardItemKey(item)))
        {
            who.mailReceived.Add(GetRewardItemKey(item));
        }
    }

    public void OpenMuseumDialogueMenu()
    {
        string donateText = MuseumData.Strings.MenuDonate ??
                            Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Donate");
        string collectText = MuseumData.Strings.MenuCollect ??
                             Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Collect");

        NPC? owner = Game1.getCharacterFromName(MuseumData.Owner?.Name);
        if (DoesFarmerHaveAnythingToDonate(Game1.player) && !Mutex.IsLocked())
        {
            Response[] choice = ((GetRewardsForPlayer(Game1.player).Count <= 0)
                ? new Response[2]
                {
                    new Response("Donate", TokenParser.ParseText(donateText)),
                    new Response("Leave", Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Leave"))
                }
                : new Response[3]
                {
                    new Response("Donate", TokenParser.ParseText(donateText)),
                    new Response("Collect", TokenParser.ParseText(collectText)),
                    new Response("Leave", Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Leave"))
                });
            Museum.createQuestionDialogue("", choice, "Museum");
        }
        else if (GetRewardsForPlayer(Game1.player).Count > 0)
        {
            Museum.createQuestionDialogue("", new Response[2]
            {
                new Response("Collect", TokenParser.ParseText(collectText)),
                new Response("Leave", Game1.content.LoadString("Strings\\Locations:ArchaeologyHouse_Gunther_Leave"))
            }, "Museum");
        }
        else if (DoesFarmerHaveAnythingToDonate(Game1.player) && Mutex.IsLocked())
        {
            // TODO: These need to be customizable.
            if (owner is null || !IsNpcClockedIn(owner, MuseumData.Owner?.Area))
            {
                Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\UI:NPC_Busy",
                    Museum.DisplayName));
            }
            else
            {
                Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\UI:NPC_Busy",
                    owner.displayName));
            }
        }
        else
        {
            bool isOwnerClockedIn = IsNpcClockedIn(owner, MuseumData.Owner?.Area);

            // TODO: Check to make sure the owner is actually around first, if they exist.
            if (DonatedItems.Count >= TotalPossibleDonations.Count)
            {
                string completeText = isOwnerClockedIn switch
                {
                    true => MuseumData.Strings.MuseumComplete_Owner ?? CMF.DefaultStrings.MuseumComplete_Owner,
                    false => MuseumData.Strings.MuseumComplete_NoOwner ?? CMF.DefaultStrings.MuseumComplete_NoOwner
                } ?? Game1.content.LoadString("Data\\ExtraDialogue:Gunther_MuseumComplete");

                if (isOwnerClockedIn)
                {
                    Game1.DrawDialogue(new Dialogue(owner, null, Game1.parseText(completeText)));
                }
                else Game1.drawObjectDialogue(TokenParser.ParseText(completeText));
            }
            else if (DonatedItems.Any())
            {
                string nothingToDonateText = isOwnerClockedIn switch
                {
                    true => MuseumData.Strings.NothingToDonate_Owner ?? CMF.DefaultStrings.NothingToDonate_Owner,
                    false => MuseumData.Strings.NothingToDonate_NoOwner ?? CMF.DefaultStrings.NothingToDonate_NoOwner
                } ?? Game1.content.LoadString("Data\\ExtraDialogue:Gunther_NothingToDonate");

                if (isOwnerClockedIn)
                {
                    Game1.DrawDialogue(new Dialogue(owner, null, Game1.parseText(nothingToDonateText)));
                }
                else Game1.drawObjectDialogue(TokenParser.ParseText(nothingToDonateText));
            }
            else
            {
                string noDonationsText = isOwnerClockedIn switch
                {
                    true => MuseumData.Strings.NoDonations_Owner ?? CMF.DefaultStrings.NoDonations_Owner,
                    false => MuseumData.Strings.NoDonations_NoOwner ?? CMF.DefaultStrings.NoDonations_NoOwner
                } ?? Game1.content.LoadString("Data\\ExtraDialogue:Gunther_NoArtifactsFound");

                if (isOwnerClockedIn)
                {
                    Game1.DrawDialogue(new Dialogue(owner, null, Game1.parseText(noDonationsText)));
                }
                else Game1.drawObjectDialogue(TokenParser.ParseText(noDonationsText));
            }
        }
    }

    private bool HighlightPreviouslyDonated(Item i)
    {
        return i.modData.ContainsKey("CMF_Position");
    }
    
    private void ReturnToMuseum(Item item, Farmer who)
    {
        var menu = Game1.activeClickableMenu as ItemGrabMenu;
        menu!.heldItem = menu.heldItem.ConsumeStack(1);
        Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}").Add(item);
    }

    private void RetrieveItemFromMuseum(Item item, Farmer who)
    {
        Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}").RemoveEmptySlots();
    }

    private void ResetModData(Item? i)
    {
        i?.modData.Remove("CMF_Position");
    }

    public void OpenRetrievalMenu()
    {
        Game1.activeClickableMenu = new ItemGrabMenu(
            Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}"),
            reverseGrab: false,
            showReceivingMenu: true, HighlightPreviouslyDonated, ReturnToMuseum, "Retrieve", RetrieveItemFromMuseum,
            snapToBottom: false, canBeExitedWithKey: true, playRightClickSound: false, allowRightClick: false,
            showOrganizeButton: false, 0, null, -1, this, allowExitWithHeldItem: true)
        {
            exitFunction = () =>
            {
                foreach (var item in Game1.player.Items)
                {
                    ResetModData(item);
                }
                Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}").RemoveEmptySlots();
                Mutex.ReleaseLock();
            }
        };
    }

    public bool IsNpcClockedIn(NPC? npc, Rectangle? area)
    {
        if (npc is null || area is null || area.Value.IsEmpty) return false;
        var areaToCheck = area.Value.Size.Equals(Point.Zero) switch
        {
            true => new Rectangle(area.Value.X, area.Value.Y, 1, 1),
            false => area.Value
        };

        foreach (var character in Game1.currentLocation.characters)
        {
            if (character.Name.Equals(npc.Name) && areaToCheck.Contains(character.Tile))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsTileSuitableForMuseumItem(int x, int y)
    {
        if (!HasDonatedItemAt(new Vector2(x, y)))
        {
            int indexOfBuildingsLayer = Museum.getTileIndexAt(new Point(x, y), "Buildings");
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
        Tile tile = Museum.map.RequireLayer("Buildings")
            .PickTile(new Location(x * Game1.tileSize, y * Game1.tileSize), Game1.viewport.Size);
        if (tile == null || !tile.Properties.TryGetValue("Spiderbuttons.CMF", out string value))
        {
            value = Museum.doesTileHaveProperty(x, y, "Spiderbuttons.CMF", "Buildings");
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
            ItemQueryContext itemQueryContext = new ItemQueryContext(Museum, Game1.player, museumRandom,
                $"{Museum.NameOrUniqueName} > GetAllAvailableRewards");
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                Log.Error($"Ignored custom museum {Museum.Name} reward item entry with no Id field.");
                continue;
            }

            if (!GameStateQuery.CheckConditions(entry.Condition, Museum, null, null, null, museumRandom))
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
        ItemQueryContext itemQueryContext = new ItemQueryContext(Museum, Game1.player, museumRandom,
            $"{Museum.NameOrUniqueName} > GetFirstAvailableReward");
        foreach (var entry in rewardData.RewardItems)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                Log.Error($"Ignored custom museum {Museum.Name} reward item entry with no Id field.");
                continue;
            }

            if (!GameStateQuery.CheckConditions(entry.Condition, Museum, null, null, null, museumRandom))
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
}