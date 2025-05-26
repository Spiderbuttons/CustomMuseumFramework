using System.Collections.Generic;
using System.Linq;
using CustomMuseumFramework.Helpers;
using CustomMuseumFramework.Menus;
using CustomMuseumFramework.Models;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
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
    private readonly string? LocationName;
    public GameLocation Museum => Game1.RequireLocation(LocationName);

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
        LocationName = location.Name;
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

    public void Reset(bool pop = false)
    {
        foreach (var item in DonatedItems)
        {
            RemoveItem(item.Key, pop);
        }

        CalculateDonations();
    }

    public bool HasDonatedItem()
    {
        return DonatedItems.Values.Any();
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

    private bool HasDonatedItemAt(Vector2 tile)
    {
        return DonatedItems.ContainsKey(tile);
    }

    public bool DonateItem(Vector2 location, string itemId, bool force = false)
    {
        if (location == Vector2.Zero ||
            (!force && !IsTileSuitableForMuseumItem((int)location.X, (int)location.Y))) return false;

        if (force) RemoveItem(location);

        Item item = ItemRegistry.Create(itemId);
        item.modData["CMF_Position"] = $"{location.X} {location.Y}";
        return DonateItem(item);
    }

    private bool DonateItem(Item? item)
    {
        if (item is null) return false;
        Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}").Add(item);
        foreach (var farmer in Game1.getAllFarmers())
        {
            farmer.NotifyQuests(q => q.OnMuseumDonation(item));
        }

        if (CMF.GlobalDonatableItems.TryGetValue(item.QualifiedItemId, out var museumDict))
        {
            if (museumDict.ContainsKey(this)) museumDict[this] = true;
            else museumDict.Add(this, true);
        }

        MultiplayerUtils.broadcastTrigger(new MultiplayerUtils.TriggerPackage($"{CMF.Manifest.UniqueID}_MuseumDonation",
            item.QualifiedItemId, item.QualifiedItemId, Museum.Name));
        return true;
    }

    public bool RemoveItem(string itemId, bool pop = false)
    {
        itemId = ItemRegistry.QualifyItemId(itemId);
        var location = DonatedItems.FirstOrDefault(pair => pair.Value.EqualsIgnoreCase(itemId)).Key;
        if (location == Vector2.Zero) return false;

        return RemoveItem(location, pop);
    }

    public bool RemoveItem(Vector2 location, bool pop = false)
    {
        if (!DonatedItems.TryGetValue(location, out var itemId)) return false;

        var inv = Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}");
        inv.RemoveWhere(item =>
        {
            if (item.QualifiedItemId.EqualsIgnoreCase(itemId))
            {
                if (pop)
                {
                    Vector2 loc = location;
                    if (item.modData.ContainsKey("CMF_Position"))
                    {
                        loc = new Vector2(float.Parse(item.modData["CMF_Position"].Split(' ')[0]),
                            float.Parse(item.modData["CMF_Position"].Split(' ')[1]));
                        item.modData.Remove("CMF_Position");
                    }

                    Game1.createItemDebris(item, loc * 64, 2, Museum);
                }

                return true;
            }

            return false;
        });

        if (CMF.GlobalDonatableItems.TryGetValue(itemId, out var museumDict))
        {
            if (museumDict.ContainsKey(this)) museumDict[this] = false;
            else museumDict.Add(this, false);
        }

        return true;
    }

    public static bool DoesItemSatisfyRequirement(Item? item, DonationRequirement requirement)
    {
        if (item is null) return false;

        if (requirement.ItemIds is null && requirement.Categories is null && requirement.ContextTags is null)
            return true;

        switch (requirement.MatchType)
        {
            case MatchType.Any:
                if (requirement.ItemIds is not null && requirement.ItemIds.Contains(item.QualifiedItemId))
                    return true;
                if (requirement.Categories is not null && requirement.Categories.Contains(item.Category))
                    return true;
                if (requirement.ContextTags is not null &&
                    ItemContextTagManager.DoAnyTagsMatch(requirement.ContextTags, item.GetContextTags()))
                    return true;
                break;
            case MatchType.All:
                if (requirement.ItemIds is not null && !requirement.ItemIds.Contains(item.QualifiedItemId))
                    return false;
                if (requirement.Categories is not null && !requirement.Categories.Contains(item.Category))
                    return false;
                if (requirement.ContextTags is null ||
                    ItemContextTagManager.DoAnyTagsMatch(requirement.ContextTags, item.GetContextTags()))
                    return true;
                break;
        }

        return false;
    }

    public int DonationsSatisfyingRequirement(DonationRequirement requirement)
    {
        var donatedItems = Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}");
        int satisfyingItems = 0;
        foreach (var item in donatedItems)
        {
            if (DoesItemSatisfyRequirement(item, requirement)) satisfyingItems++;
        }

        return satisfyingItems;
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
        Item item = ItemRegistry.Create(itemId);

        if (item.HasContextTag("not_museum_donatable"))
        {
            return false;
        }

        if (checkDonatedItems && HasDonatedItem(item.QualifiedItemId))
        {
            return false;
        }

        List<DonationRequirement> reqs = MuseumData.DonationRequirements;
        if (reqs.Any(r => r.Id is null))
            CMF.ModMonitor.LogOnce(
                $"A DonationRequirement for {Museum.Name} is missing an Id field! This may cause certain game state queries to behave incorrectly.",
                LogLevel.Warn);
        return reqs.Any(req => DoesItemSatisfyRequirement(item, req));
    }

    public bool DoesFarmerHaveAnythingToDonate(Farmer who)
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

        List<CustomMuseumReward> museumRewardData = MuseumData.Rewards;
        Dictionary<string, bool> metRequirements = RewardRequirementsCheck(museumRewardData);
        List<Item> rewards = new List<Item>();
        foreach (CustomMuseumReward reward in museumRewardData)
        {
            string? id = reward.Id;
            if (id is null)
            {
                Log.Warn($"A reward for {Museum.Name} is missing an Id field! This reward will be skipped.");
                continue;
            }

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

    private Dictionary<string, bool> RewardRequirementsCheck(List<CustomMuseumReward> rewardDataList)
    {
        var results = new Dictionary<string, bool>();

        foreach (var reward in rewardDataList)
        {
            if (reward.Id is null)
            {
                Log.Warn($"A reward for {Museum.Name} is missing an Id field! This reward will be skipped.");
                continue;
            }

            if (!results.TryAdd(reward.Id, true))
            {
                Log.Warn($"A reward for {Museum.Name} has a duplicate Id '{reward.Id}'! This reward will be skipped.");
                continue;
            }

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
                    int count = DonationsSatisfyingRequirement(requirement);

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

    private void AddNonItemRewards(CustomMuseumReward? data, string rewardId, Farmer player)
    {
        if (data is null) return;
        if (data.FlagOnCompletion)
        {
            player.mailReceived.Add($"{Museum.Name}_RewardCollected_{rewardId}");
        }

        if (data.Action is not null)
        {
            if (!TriggerActionManager.TryRunAction(data.Action, out var error, out _))
            {
                Log.Error(
                    $"Custom museum {Museum.Name} reward with ID '{rewardId}' ignored invalid action '{data.Action}': {error}");
            }
        }

        if (data.Actions is not null)
        {
            foreach (string action in data.Actions)
            {
                if (!TriggerActionManager.TryRunAction(action, out var error, out _))
                {
                    Log.Error(
                        $"Custom museum {Museum.Name} reward with ID '{rewardId}' ignored invalid action '{action}': {error}");
                }
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
            if (owner is null || !IsNpcClockedIn(owner, MuseumData.Owner?.Area))
            {
                string busyText = MuseumData.Strings.Busy_NoOwner ?? i18n.BusyNoOwner();
                Game1.drawObjectDialogue(TokenParser.ParseText(busyText));
            }
            else
            {
                string busyText = MuseumData.Strings.Busy_Owner ?? string.Format(i18n.BusyOwner(), owner.displayName);
                Game1.drawObjectDialogue(TokenParser.ParseText(busyText));
            }
        }
        else
        {
            bool isOwnerClockedIn = IsNpcClockedIn(owner, MuseumData.Owner?.Area);

            if (DonatedItems.Count >= TotalPossibleDonations.Count)
            {
                string completeText = isOwnerClockedIn switch
                {
                    true => MuseumData.Strings.MuseumComplete_Owner ?? i18n.MuseumCompleteOwner(),
                    false => MuseumData.Strings.MuseumComplete_NoOwner ?? i18n.MuseumCompleteNoOwner()
                };

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
                    true => MuseumData.Strings.NothingToDonate_Owner ?? i18n.NothingToDonateOwner(),
                    false => MuseumData.Strings.NothingToDonate_NoOwner ?? i18n.NothingToDonateNoOwner()
                };

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
                    true => MuseumData.Strings.NoDonations_Owner ?? i18n.NoDonationsOwner(),
                    false => MuseumData.Strings.NoDonations_NoOwner ?? i18n.NoDonationsNoOwner()
                };

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
        if (CMF.GlobalDonatableItems.TryGetValue(item.QualifiedItemId, out var museumDict))
        {
            if (museumDict.ContainsKey(this)) museumDict[this] = true;
            else museumDict.Add(this, true);
        }
    }

    private void RetrieveItemFromMuseum(Item item, Farmer who)
    {
        Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}").RemoveEmptySlots();
        foreach (var farmer in Game1.getAllFarmers())
        {
            farmer.NotifyQuests(q => q.OnMuseumRetrieval(item));
        }

        if (CMF.GlobalDonatableItems.TryGetValue(item.QualifiedItemId, out var museumDict))
        {
            if (museumDict.ContainsKey(this)) museumDict[this] = false;
            else museumDict.Add(this, false);
        }

        MultiplayerUtils.broadcastTrigger(new MultiplayerUtils.TriggerPackage(
            $"{CMF.Manifest.UniqueID}_MuseumRetrieval", item.QualifiedItemId, item.QualifiedItemId, Museum.Name));
    }

    private void ResetModData(Item? i)
    {
        i?.modData.Remove("CMF_Position");
    }

    public void OpenRetrievalMenu()
    {
        Mutex.RequestLock(delegate
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

                    Game1.player.team.GetOrCreateGlobalInventory($"{CMF.Manifest.UniqueID}_{Museum.Name}")
                        .RemoveEmptySlots();
                    Mutex.ReleaseLock();
                }
            };
        });
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
        if (HasDonatedItemAt(new Vector2(x, y))) return false;

        int indexOfBuildingsLayer = Museum.getTileIndexAt(new Point(x, y), "Buildings");
        if (indexOfBuildingsLayer is 1073 or 1074 or 1072 or 1237 or 1238)
        {
            return true;
        }

        return IsTileDonationSpot(x, y);
    }

    private bool IsTileDonationSpot(int x, int y)
    {
        Tile tile = Museum.map.RequireLayer("Buildings")
            .PickTile(new Location(x * Game1.tileSize, y * Game1.tileSize), Game1.viewport.Size);
        if (tile == null || !tile.Properties.TryGetValue("Spiderbuttons.CMF", out string value))
        {
            value = Museum.doesTileHaveProperty(x, y, "Spiderbuttons.CMF", "Buildings");
        }

        return value is not null && value.EqualsIgnoreCase("DonationSpot");
    }

    private bool CanCollectReward(CustomMuseumReward reward, string rewardId, Farmer player,
        Dictionary<string, bool> metRequirements)
    {
        if (reward.FlagOnCompletion && player.mailReceived.Contains($"{Museum.Name}_RewardCollected_{rewardId}"))
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

    private List<Item> GetAllAvailableRewards(CustomMuseumReward reward)
    {
        var results = new List<Item>();
        if (reward.RewardItems is null) return results;
        foreach (var entry in reward.RewardItems)
        {
            var randomSeed = Game1.hash.GetDeterministicHashCode(reward.Id + entry.Id);
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

    private Item? GetFirstAvailableReward(CustomMuseumReward reward)
    {
        if (reward.RewardItems is null) return null;
        var museumRandom = Utility.CreateDaySaveRandom();
        ItemQueryContext itemQueryContext = new ItemQueryContext(Museum, Game1.player, museumRandom,
            $"{Museum.NameOrUniqueName} > GetFirstAvailableReward");
        foreach (var entry in reward.RewardItems)
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
        return MuseumData.Bounds;
    }

    public Vector2? GetFreeDonationSpot()
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

        return null;
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

    public void IncrementLostBookCount(string booksetId)
    {
        if (!Context.IsMainPlayer) return;
        
        if (!Museum.modData.ContainsKey($"Spiderbuttons.CMF_LostBooks_{booksetId}"))
            Museum.modData.Add($"Spiderbuttons.CMF_LostBooks_{booksetId}", "1");
        else
            Museum.modData[$"Spiderbuttons.CMF_LostBooks_{booksetId}"] =
                (int.Parse(Museum.modData[$"Spiderbuttons.CMF_LostBooks_{booksetId}"]) + 1).ToString();

        if (!Museum.modData.ContainsKey($"Spiderbuttons.CMF_TotalLostBooks"))
            Museum.modData.Add("Spiderbuttons.CMF_TotalLostBooks", "1");
        else
            Museum.modData["Spiderbuttons.CMF_TotalLostBooks"] =
                (int.Parse(Museum.modData["Spiderbuttons.CMF_TotalLostBooks"]) + 1).ToString();
    }

    public Dictionary<string, Dictionary<int, Vector2>> getLostBooksLocations()
    {
        Dictionary<string, Dictionary<int, Vector2>> lostBooksLocations = new();
        for (int x = 0; x < Museum.map.Layers[0].LayerWidth; x++)
        {
            for (int y = 0; y < Museum.map.Layers[0].LayerHeight; y++)
            {
                string[] action = Museum.GetTilePropertySplitBySpaces("Action", "Buildings", x, y);
                if (ArgUtility.Get(action, 0) != "Spiderbuttons.CMF_LostBook") continue;

                if (!ArgUtility.TryGet(action, 1, out var bookDataId, out var error, false, "string bookDataId") ||
                    !ArgUtility.TryGetInt(action, 2, out var bookIndex, out error, "int bookIndex"))
                {
                    Museum.LogTileActionError(action, x, y, error);
                    continue;
                }

                if (!lostBooksLocations.TryGetValue(bookDataId, out var bookLocations))
                {
                    bookLocations = new Dictionary<int, Vector2>();
                    lostBooksLocations[bookDataId] = bookLocations;
                }

                if (!bookLocations.ContainsKey(bookIndex))
                {
                    bookLocations[bookIndex] = new Vector2(x, y);
                }
                else Log.Warn($"Duplicate lost book location found for {bookDataId} at index {bookIndex}.");
            }
        }

        return lostBooksLocations;
    }
}