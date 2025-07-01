using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Objects;

namespace CustomMuseumFramework.Menus;

public class BigItemGrabMenu : ItemGrabMenu
{
    public BigItemGrabMenu(IList<Item> inventory, bool reverseGrab, bool showReceivingMenu,
        InventoryMenu.highlightThisItem highlightFunction, behaviorOnItemSelect behaviorOnItemSelectFunction,
        string message, behaviorOnItemSelect? behaviorOnItemGrab = null, bool snapToBottom = false,
        bool canBeExitedWithKey = false, bool playRightClickSound = true, bool allowRightClick = true,
        bool showOrganizeButton = false, int source = 0, Item? sourceItem = null, int whichSpecialButton = -1,
        object? context = null, ItemExitBehavior heldItemExitBehavior = ItemExitBehavior.ReturnToPlayer,
        bool allowExitWithHeldItem = false) : base(inventory, reverseGrab, showReceivingMenu,
        highlightFunction, behaviorOnItemSelectFunction, message, behaviorOnItemGrab, snapToBottom, canBeExitedWithKey,
        playRightClickSound, allowRightClick, showOrganizeButton, source, sourceItem, whichSpecialButton, context,
        heldItemExitBehavior, allowExitWithHeldItem)
    {
        this.source = source;
        this.message = message;
        this.reverseGrab = reverseGrab;
        this.showReceivingMenu = showReceivingMenu;
        this.playRightClickSound = playRightClickSound;
        this.allowRightClick = allowRightClick;
        this.inventory.showGrayedOutSlots = true;
        this.sourceItem = sourceItem;
        this.whichSpecialButton = whichSpecialButton;
        this.context = context;
        if (sourceItem != null && Game1.currentLocation.objects.Values.Contains(sourceItem))
        {
            _sourceItemInCurrentLocation = true;
        }
        else
        {
            _sourceItemInCurrentLocation = false;
        }

        if (sourceItem is Chest sourceChest)
        {
            if (base.CanHaveColorPicker())
            {
                Chest itemToDrawColored = new Chest(playerChest: true, sourceItem.ItemId);
                chestColorPicker = new DiscreteColorPicker(xPositionOnScreen, yPositionOnScreen - 64 - borderWidth * 2,
                    sourceChest.playerChoiceColor.Value, itemToDrawColored);
                itemToDrawColored.playerChoiceColor.Value =
                    DiscreteColorPicker.getColorFromSelection(chestColorPicker.colorSelection);
                colorPickerToggleButton = new ClickableTextureComponent(
                    new Rectangle(xPositionOnScreen + width, yPositionOnScreen + height / 3 - 64 + -160, 64, 64),
                    Game1.mouseCursors, new Rectangle(119, 469, 16, 16), 4f)
                {
                    hoverText = Game1.content.LoadString("Strings\\UI:Toggle_ColorPicker"),
                    myID = 27346,
                    downNeighborID = -99998,
                    leftNeighborID = 53921,
                    region = 15923
                };
            }

            if (source == 1 &&
                (sourceChest.SpecialChestType == Chest.SpecialChestTypes.None ||
                 sourceChest.SpecialChestType == Chest.SpecialChestTypes.BigChest) &&
                InventoryPage.ShouldShowJunimoNoteIcon())
            {
                junimoNoteIcon = new ClickableTextureComponent("",
                    new Rectangle(xPositionOnScreen + width, yPositionOnScreen + height / 3 - 64 + -216, 64, 64), "",
                    Game1.content.LoadString("Strings\\UI:GameMenu_JunimoNote_Hover"), Game1.mouseCursors,
                    new Rectangle(331, 374, 15, 14), 4f)
                {
                    myID = 898,
                    leftNeighborID = 11,
                    downNeighborID = 106
                };
            }
        }

        if (whichSpecialButton == 1)
        {
            specialButton = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + width, yPositionOnScreen + height / 3 - 64 + -160, 64, 64),
                Game1.mouseCursors, new Rectangle(108, 491, 16, 16), 4f)
            {
                myID = 12485,
                downNeighborID = (showOrganizeButton ? 12952 : 5948),
                region = 15923,
                leftNeighborID = 53921
            };
            if (context is JunimoHut hut)
            {
                specialButton.sourceRect.X = (hut.noHarvest.Value ? 124 : 108);
            }
        }

        if (snapToBottom)
        {
            movePosition(0, Game1.uiViewport.Height - (yPositionOnScreen + height - spaceToClearTopBorder));
            snappedtoBottom = true;
        }


        int capacity = 108;
        int rows = 6;

        int containerWidth = 64 * (capacity / rows);
        int yOffset = (rows - 3) * 21;
        ItemsToGrabMenu = new InventoryMenu(Game1.uiViewport.Width / 2 - containerWidth / 2,
            yPositionOnScreen - yOffset, playerInventory: false, inventory,
            highlightFunction, capacity, rows);

        yPositionOnScreen += 48;
        this.inventory.SetPosition(this.inventory.xPositionOnScreen, this.inventory.yPositionOnScreen + 38 + 4);
        ItemsToGrabMenu.SetPosition(ItemsToGrabMenu.xPositionOnScreen,
            ItemsToGrabMenu.yPositionOnScreen - 32);
        storageSpaceTopBorderOffset = 20;
        trashCan.bounds.X = ItemsToGrabMenu.width + ItemsToGrabMenu.xPositionOnScreen + borderWidth * 2;
        okButton.bounds.X = ItemsToGrabMenu.width + ItemsToGrabMenu.xPositionOnScreen + borderWidth * 2;

        ItemsToGrabMenu.populateClickableComponentList();
        for (int j = 0; j < ItemsToGrabMenu.inventory.Count; j++)
        {
            if (ItemsToGrabMenu.inventory[j] != null)
            {
                ItemsToGrabMenu.inventory[j].myID += 53910;
                ItemsToGrabMenu.inventory[j].upNeighborID += 53910;
                ItemsToGrabMenu.inventory[j].rightNeighborID += 53910;
                ItemsToGrabMenu.inventory[j].downNeighborID = -7777;
                ItemsToGrabMenu.inventory[j].leftNeighborID += 53910;
                ItemsToGrabMenu.inventory[j].fullyImmutable = true;
            }
        }

        behaviorFunction = behaviorOnItemSelectFunction;
        this.behaviorOnItemGrab = behaviorOnItemGrab;
        canExitOnKey = canBeExitedWithKey;
        if (showOrganizeButton)
        {
            fillStacksButton = new ClickableTextureComponent("",
                new Rectangle(xPositionOnScreen + width, yPositionOnScreen + height / 3 - 64 - 64 - 16, 64, 64), "",
                Game1.content.LoadString("Strings\\UI:ItemGrab_FillStacks"), Game1.mouseCursors,
                new Rectangle(103, 469, 16, 16), 4f)
            {
                myID = 12952,
                upNeighborID = ((colorPickerToggleButton != null) ? 27346 : ((specialButton != null) ? 12485 : (-500))),
                downNeighborID = 106,
                leftNeighborID = 53921,
                region = 15923
            };
            organizeButton = new ClickableTextureComponent("",
                new Rectangle(xPositionOnScreen + width, yPositionOnScreen + height / 3 - 64, 64, 64), "",
                Game1.content.LoadString("Strings\\UI:ItemGrab_Organize"), Game1.mouseCursors,
                new Rectangle(162, 440, 16, 16), 4f)
            {
                myID = 106,
                upNeighborID = 12952,
                downNeighborID = 5948,
                leftNeighborID = 53921,
                region = 15923
            };
        }

        base.RepositionSideButtons();
        if (chestColorPicker != null)
        {
            discreteColorPickerCC = new List<ClickableComponent>();
            for (int i = 0; i < DiscreteColorPicker.totalColors; i++)
            {
                List<ClickableComponent> list = discreteColorPickerCC;
                ClickableComponent obj =
                    new ClickableComponent(
                        new Rectangle(chestColorPicker.xPositionOnScreen + borderWidth / 2 + i * 9 * 4,
                            chestColorPicker.yPositionOnScreen + borderWidth / 2, 36, 28), "")
                    {
                        myID = i + 4343,
                        rightNeighborID = ((i < DiscreteColorPicker.totalColors - 1) ? (i + 4343 + 1) : (-1)),
                        leftNeighborID = ((i > 0) ? (i + 4343 - 1) : (-1))
                    };
                InventoryMenu itemsToGrabMenu = ItemsToGrabMenu;
                obj.downNeighborID = ((itemsToGrabMenu != null && itemsToGrabMenu.inventory.Count > 0) ? 53910 : 0);
                list.Add(obj);
            }
        }

        if (organizeButton != null)
        {
            foreach (ClickableComponent item in ItemsToGrabMenu.GetBorder(InventoryMenu.BorderSide.Right))
            {
                item.rightNeighborID = organizeButton.myID;
            }
        }

        if (trashCan != null && this.inventory.inventory.Count >= 12 && this.inventory.inventory[11] != null)
        {
            this.inventory.inventory[11].rightNeighborID = 5948;
        }

        if (trashCan != null)
        {
            trashCan.leftNeighborID = 11;
        }

        if (okButton != null)
        {
            okButton.leftNeighborID = 11;
        }

        ClickableComponent? top_right = ItemsToGrabMenu.GetBorder(InventoryMenu.BorderSide.Right).FirstOrDefault();
        if (top_right != null)
        {
            if (organizeButton != null)
            {
                organizeButton.leftNeighborID = top_right.myID;
            }

            if (specialButton != null)
            {
                specialButton.leftNeighborID = top_right.myID;
            }

            if (fillStacksButton != null)
            {
                fillStacksButton.leftNeighborID = top_right.myID;
            }

            if (junimoNoteIcon != null)
            {
                junimoNoteIcon.leftNeighborID = top_right.myID;
            }
        }

        base.populateClickableComponentList();
        if (Game1.options.SnappyMenus)
        {
            base.snapToDefaultClickableComponent();
        }

        SetupBorderNeighbors();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        if (snappedtoBottom)
        {
            movePosition((newBounds.Width - oldBounds.Width) / 2,
                Game1.uiViewport.Height - (yPositionOnScreen + height - spaceToClearTopBorder));
        }
        else
        {
            movePosition((newBounds.Width - oldBounds.Width) / 2, (newBounds.Height - oldBounds.Height) / 2);
        }

        ItemsToGrabMenu?.gameWindowSizeChanged(oldBounds, newBounds);
        RepositionSideButtons();
        if (CanHaveColorPicker() && sourceItem is Chest chest)
        {
            chestColorPicker = new DiscreteColorPicker(xPositionOnScreen, yPositionOnScreen - 64 - borderWidth * 2,
                chest.playerChoiceColor.Value, new Chest(playerChest: true, sourceItem.ItemId));
        }
    }
}