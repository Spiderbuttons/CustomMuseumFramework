using System;
using System.Collections.Generic;
using CustomMuseumFramework.Helpers;
using CustomMuseumFramework.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;
using xTile.Dimensions;

namespace CustomMuseumFramework.Menus;

public sealed class CustomMuseumMenu : MenuWithInventory
{
    public const int startingState = 0;
    public const int placingInMuseumState = 1;
    public const int exitingState = 2;

    private int fadeTimer;
    private int state;
    private int menuPositionOffset;
    private bool fadeIntoBlack;
    private bool menuMovingDown;
    private float blackFadeAlpha;
    private SparklingText? sparkleText;
    private Vector2 globalLocationOfSparklingItem;

    private readonly MuseumManager MuseumManager;
    private CustomMuseumData MuseumData => MuseumManager.MuseumData;

    private bool holdingMuseumItem;
    private bool reorganizing;

    public CustomMuseumMenu(InventoryMenu.highlightThisItem highlighterMethod) : base(highlighterMethod, okButton: true)
    {
        fadeTimer = 800;
        fadeIntoBlack = true;
        movePosition(0, Game1.uiViewport.Height - yPositionOnScreen - height);
        Game1.player.forceCanMove();
        if (CMF.MuseumManagers.TryGetValue(Game1.currentLocation.Name, out var manager))
        {
            MuseumManager = manager;
        } else throw new InvalidOperationException("The custom museum donation menu must be used from within a custom museum.");
        
        if (Game1.options.SnappyMenus)
        {
            if (okButton is not null)
            {
                okButton.myID = 106;
            }

            populateClickableComponentList();
            currentlySnappedComponent = getComponentWithID(0);
            snapCursorToCurrentSnappedComponent();
        }

        Game1.displayHUD = false;
    }

    public override bool shouldClampGamePadCursor()
    {
        return true;
    }

    public override void receiveKeyPress(Keys key)
    {
        if (fadeTimer > 0)
        {
            return;
        }

        if (Game1.options.doesInputListContain(Game1.options.menuButton, key) &&
            !Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.menuButton) && readyToClose())
        {
            state = 2;
            fadeTimer = 500;
            fadeIntoBlack = true;
        }
        else if (Game1.options.doesInputListContain(Game1.options.menuButton, key) &&
                 !Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.menuButton) && !holdingMuseumItem &&
                 menuMovingDown)
        {
            if (heldItem != null)
            {
                Game1.playSound("bigDeSelect");
                Utility.CollectOrDrop(heldItem);
                heldItem = null;
            }

            ReturnToDonatableItems();
        }
        else if (Game1.options.SnappyMenus && heldItem == null && !reorganizing)
        {
            base.receiveKeyPress(key);
        }

        if (!Game1.options.SnappyMenus)
        {
            if (Game1.options.doesInputListContain(Game1.options.moveDownButton, key))
            {
                Game1.panScreen(0, 4);
            }
            else if (Game1.options.doesInputListContain(Game1.options.moveRightButton, key))
            {
                Game1.panScreen(4, 0);
            }
            else if (Game1.options.doesInputListContain(Game1.options.moveUpButton, key))
            {
                Game1.panScreen(0, -4);
            }
            else if (Game1.options.doesInputListContain(Game1.options.moveLeftButton, key))
            {
                Game1.panScreen(-4, 0);
            }
        }
        else
        {
            if (heldItem == null && !reorganizing)
            {
                return;
            }
            
            Vector2 newCursorPositionTile =
                new Vector2(
                    (int)((Utility.ModifyCoordinateFromUIScale(Game1.getMouseX()) + Game1.viewport.X) / 64f),
                    (int)((Utility.ModifyCoordinateFromUIScale(Game1.getMouseY()) + Game1.viewport.Y) / 64f));
            if (!MuseumManager.IsTileSuitableForMuseumItem((int)newCursorPositionTile.X, (int)newCursorPositionTile.Y) &&
                (!reorganizing || !LibraryMuseum.HasDonatedArtifactAt(newCursorPositionTile)))
            {
                newCursorPositionTile = MuseumManager.GetFreeDonationSpot().GetValueOrDefault();
                if (newCursorPositionTile == Vector2.Zero)
                {
                    return;
                }
                Game1.setMousePosition(
                    (int)Utility.ModifyCoordinateForUIScale(newCursorPositionTile.X * 64f - Game1.viewport.X +
                                                            32f),
                    (int)Utility.ModifyCoordinateForUIScale(newCursorPositionTile.Y * 64f - Game1.viewport.Y +
                                                            32f));
                return;
            }

            if (key == Game1.options.getFirstKeyboardKeyFromInputButtonList(Game1.options.moveUpButton))
            {
                newCursorPositionTile =
                    MuseumManager.FindMuseumPieceLocationInDirection(newCursorPositionTile, 0, 21, !reorganizing);
            }
            else if (key == Game1.options.getFirstKeyboardKeyFromInputButtonList(Game1.options.moveRightButton))
            {
                newCursorPositionTile =
                    MuseumManager.FindMuseumPieceLocationInDirection(newCursorPositionTile, 1, 21, !reorganizing);
            }
            else if (key == Game1.options.getFirstKeyboardKeyFromInputButtonList(Game1.options.moveDownButton))
            {
                newCursorPositionTile =
                    MuseumManager.FindMuseumPieceLocationInDirection(newCursorPositionTile, 2, 21, !reorganizing);
            }
            else if (key == Game1.options.getFirstKeyboardKeyFromInputButtonList(Game1.options.moveLeftButton))
            {
                newCursorPositionTile =
                    MuseumManager.FindMuseumPieceLocationInDirection(newCursorPositionTile, 3, 21, !reorganizing);
            }

            if (!Game1.viewport.Contains(new Location((int)(newCursorPositionTile.X * 64f + 32f),
                    Game1.viewport.Y + 1)))
            {
                Game1.panScreen((int)(newCursorPositionTile.X * 64f - Game1.viewport.X), 0);
            }
            else if (!Game1.viewport.Contains(new Location(Game1.viewport.X + 1,
                         (int)(newCursorPositionTile.Y * 64f + 32f))))
            {
                Game1.panScreen(0, (int)(newCursorPositionTile.Y * 64f - Game1.viewport.Y));
            }

            Game1.setMousePosition(
                (int)Utility.ModifyCoordinateForUIScale((int)newCursorPositionTile.X * 64 - Game1.viewport.X + 32),
                (int)Utility.ModifyCoordinateForUIScale((int)newCursorPositionTile.Y * 64 - Game1.viewport.Y + 32));
        }
    }

    public override bool overrideSnappyMenuCursorMovementBan()
    {
        return false;
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (menuMovingDown || b is not (Buttons.DPadUp or Buttons.LeftThumbstickUp) || !Game1.options.SnappyMenus ||
            currentlySnappedComponent is not { myID: < 12 }) return;
        
        reorganizing = true;
        menuMovingDown = true;
        receiveKeyPress(Game1.options.moveUpButton[0].key);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (fadeTimer > 0)
        {
            return;
        }

        Item oldItem = heldItem;
        if (!holdingMuseumItem)
        {
            int inventory_index = inventory.getInventoryPositionOfClick(x, y);
            if (heldItem == null)
            {
                if (inventory_index >= 0 && inventory_index < inventory.actualInventory.Count &&
                    inventory.highlightMethod(inventory.actualInventory[inventory_index]))
                {
                    heldItem = inventory.actualInventory[inventory_index].getOne();
                    inventory.actualInventory[inventory_index].Stack--;
                    if (inventory.actualInventory[inventory_index].Stack <= 0)
                    {
                        inventory.actualInventory[inventory_index] = null;
                    }
                }
            }
            else
            {
                heldItem = inventory.leftClick(x, y, heldItem);
            }
        }

        if (oldItem == null && heldItem != null && Game1.isAnyGamePadButtonBeingPressed())
        {
            receiveGamePadButton(Buttons.DPadUp);
        }

        if (oldItem != null && heldItem != null &&
            (y < Game1.viewport.Height -
             (height - (borderWidth + spaceToClearTopBorder + 192)) ||
             menuMovingDown))
        {
            Item item = heldItem;
            int mapXTile2 = (int)(Utility.ModifyCoordinateFromUIScale(x) + Game1.viewport.X) / 64;
            int mapYTile2 = (int)(Utility.ModifyCoordinateFromUIScale(y) + Game1.viewport.Y) / 64;
            if (MuseumManager.IsTileSuitableForMuseumItem(mapXTile2, mapYTile2) &&
                (MuseumManager.IsItemSuitableForDonation(item) || item.modData.ContainsKey("CMF_Position")))
            {
                int rewardsCount = MuseumManager.GetRewardsForPlayer(Game1.player).Count;
                if (MuseumManager.DonateItem(new Vector2(mapXTile2, mapYTile2), item.QualifiedItemId, force: true))
                {
                    Game1.playSound("stoneStep");
                    if (MuseumManager.GetRewardsForPlayer(Game1.player).Count > rewardsCount && !holdingMuseumItem)
                    {
                        sparkleText = new SparklingText(Game1.dialogueFont,
                            Game1.content.LoadString("Strings\\StringsFromCSFiles:NewReward"), Color.MediumSpringGreen,
                            Color.White);
                        Game1.playSound("reward");
                        globalLocationOfSparklingItem =
                            new Vector2(mapXTile2 * 64 + 32 - sparkleText.textWidth / 2f,
                                mapYTile2 * 64 - 48);
                    }
                    else
                    {
                        Game1.playSound("newArtifact");
                    }
                    heldItem = item.ConsumeStack(1);

                    if (!holdingMuseumItem)
                    {
                        MultiplayerUtils.broadcastChatMessage(MuseumManager.ON_DONATION(TokenStringBuilder.ItemNameFor(item)));
                    }
                    
                    ReturnToDonatableItems();
                }
            }
        }
        else if (heldItem == null && !inventory.isWithinBounds(x, y))
        {
            int mapXTile = (int)(Utility.ModifyCoordinateFromUIScale(x) + Game1.viewport.X) / 64;
            int mapYTile = (int)(Utility.ModifyCoordinateFromUIScale(y) + Game1.viewport.Y) / 64;
            Vector2 v = new Vector2(mapXTile, mapYTile);
            if (MuseumManager.DonatedItems.TryGetValue(v, out var itemId))
            {
                heldItem = ItemRegistry.Create(itemId, allowNull: true);
                heldItem.modData["CMF_Position"] = $"{mapXTile} {mapYTile}";
                if (MuseumManager.RemoveItem(v) && heldItem != null)
                {
                    // holdingMuseumItem = !MuseumManager.HasDonatedItem(heldItem.QualifiedItemId);
                    holdingMuseumItem = true;
                }
            }
        }

        if (heldItem != null && oldItem == null)
        {
            menuMovingDown = true;
            reorganizing = false;
        }

        if (okButton != null && okButton.containsPoint(x, y) && readyToClose())
        {
            if (fadeTimer <= 0)
            {
                Game1.playSound("bigDeSelect");
            }

            state = 2;
            fadeTimer = 800;
            fadeIntoBlack = true;
        }
    }

    private void ReturnToDonatableItems()
    {
        menuMovingDown = false;
        holdingMuseumItem = false;
        reorganizing = false;
        if (Game1.options.SnappyMenus)
        {
            movePosition(0, -menuPositionOffset);
            menuPositionOffset = 0;
            snapCursorToCurrentSnappedComponent();
        }
    }

    public override void emergencyShutDown()
    {
        if (heldItem is not null && holdingMuseumItem)
        {
            Vector2 tile = MuseumManager.GetFreeDonationSpot().GetValueOrDefault();
            if (MuseumManager.DonateItem(tile, heldItem.QualifiedItemId))
            {
                heldItem = null;
                holdingMuseumItem = false;
            }
        }

        base.emergencyShutDown();
    }

    public override bool readyToClose()
    {
        if (!holdingMuseumItem && heldItem is null)
        {
            return !menuMovingDown;
        }

        return false;
    }

    protected override void cleanupBeforeExit()
    {
        if (heldItem is not null)
        {
            heldItem = Game1.player.addItemToInventory(heldItem);
            if (heldItem is not null)
            {
                Game1.createItemDebris(heldItem, Game1.player.Position, -1);
                heldItem = null;
            }
        }

        Game1.displayHUD = true;
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        Item oldItem = heldItem;
        if (fadeTimer <= 0)
        {
            base.receiveRightClick(x, y, playSound: true);
        }

        if (heldItem is not null && oldItem is null)
        {
            menuMovingDown = true;
        }
    }

    public override void update(GameTime time)
    {
        base.update(time);
        if (sparkleText != null && sparkleText.update(time))
        {
            sparkleText = null;
        }

        if (fadeTimer > 0)
        {
            fadeTimer -= time.ElapsedGameTime.Milliseconds;
            if (fadeIntoBlack)
            {
                blackFadeAlpha = 0f + (1500f - fadeTimer) / 1500f;
            }
            else
            {
                blackFadeAlpha = 1f - (1500f - fadeTimer) / 1500f;
            }

            if (fadeTimer <= 0)
            {
                switch (state)
                {
                    case 0:
                        state = 1;
                        Game1.viewportFreeze = true;
                        Game1.viewport.Location = new Location(1152, 128);
                        Game1.clampViewportToGameMap();
                        fadeTimer = 800;
                        fadeIntoBlack = false;
                        break;
                    case 2:
                        Game1.viewportFreeze = false;
                        fadeIntoBlack = false;
                        fadeTimer = 800;
                        state = 3;
                        break;
                    case 3:
                        exitThisMenuNoSound();
                        break;
                }
            }
        }

        if (menuMovingDown && menuPositionOffset < height / 3)
        {
            menuPositionOffset += 8;
            movePosition(0, 8);
        }
        else if (!menuMovingDown && menuPositionOffset > 0)
        {
            menuPositionOffset -= 8;
            movePosition(0, -8);
        }

        int mouseX = Game1.getOldMouseX(ui_scale: false) + Game1.viewport.X;
        int mouseY = Game1.getOldMouseY(ui_scale: false) + Game1.viewport.Y;
        if ((!Game1.options.SnappyMenus && Game1.lastCursorMotionWasMouse && mouseX - Game1.viewport.X < 64) ||
            Game1.input.GetGamePadState().ThumbSticks.Right.X < 0f)
        {
            Game1.panScreen(-4, 0);
            if (Game1.input.GetGamePadState().ThumbSticks.Right.X < 0f)
            {
                snapCursorToCurrentMuseumSpot();
            }
        }
        else if ((!Game1.options.SnappyMenus && Game1.lastCursorMotionWasMouse &&
                  mouseX - (Game1.viewport.X + Game1.viewport.Width) >= -64) ||
                 Game1.input.GetGamePadState().ThumbSticks.Right.X > 0f)
        {
            Game1.panScreen(4, 0);
            if (Game1.input.GetGamePadState().ThumbSticks.Right.X > 0f)
            {
                snapCursorToCurrentMuseumSpot();
            }
        }

        if ((!Game1.options.SnappyMenus && Game1.lastCursorMotionWasMouse && mouseY - Game1.viewport.Y < 64) ||
            Game1.input.GetGamePadState().ThumbSticks.Right.Y > 0f)
        {
            Game1.panScreen(0, -4);
            if (Game1.input.GetGamePadState().ThumbSticks.Right.Y > 0f)
            {
                snapCursorToCurrentMuseumSpot();
            }
        }
        else if ((!Game1.options.SnappyMenus && Game1.lastCursorMotionWasMouse &&
                  mouseY - (Game1.viewport.Y + Game1.viewport.Height) >= -64) ||
                 Game1.input.GetGamePadState().ThumbSticks.Right.Y < 0f)
        {
            Game1.panScreen(0, 4);
            if (Game1.input.GetGamePadState().ThumbSticks.Right.Y < 0f)
            {
                snapCursorToCurrentMuseumSpot();
            }
        }

        Keys[] pressedKeys = Game1.oldKBState.GetPressedKeys();
        foreach (Keys key in pressedKeys)
        {
            receiveKeyPress(key);
        }
    }

    private void snapCursorToCurrentMuseumSpot()
    {
        if (menuMovingDown)
        {
            // ReSharper disable once PossibleLossOfFraction
            Vector2 newCursorPositionTile = new Vector2((Game1.getMouseX(ui_scale: false) + Game1.viewport.X) / 64,
                // ReSharper disable once PossibleLossOfFraction
                (Game1.getMouseY(ui_scale: false) + Game1.viewport.Y) / 64);
            Game1.setMousePosition((int)newCursorPositionTile.X * 64 - Game1.viewport.X + 32,
                (int)newCursorPositionTile.Y * 64 - Game1.viewport.Y + 32, ui_scale: false);
        }
    }

    public override void gameWindowSizeChanged(Microsoft.Xna.Framework.Rectangle oldBounds,
        Microsoft.Xna.Framework.Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        movePosition(0, Game1.viewport.Height - yPositionOnScreen - height);
        Game1.player.forceCanMove();
    }

    public override void draw(SpriteBatch b)
    {
        if ((fadeTimer <= 0 || !fadeIntoBlack) && state != 3)
        {
            if (heldItem != null)
            {
                Game1.StartWorldDrawInUI(b);
                for (int y = Game1.viewport.Y / 64 - 1; y < (Game1.viewport.Y + Game1.viewport.Height) / 64 + 2; y++)
                {
                    for (int x = Game1.viewport.X / 64 - 1; x < (Game1.viewport.X + Game1.viewport.Width) / 64 + 1; x++)
                    {
                        if (MuseumManager.IsTileSuitableForMuseumItem(x, y))
                        {
                            b.Draw(Game1.mouseCursors, Game1.GlobalToLocal(Game1.viewport, new Vector2(x, y) * 64f),
                                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 29), Color.LightGreen);
                        }
                    }
                }

                Game1.EndWorldDrawInUI(b);
            }

            if (!holdingMuseumItem)
            {
                base.draw(b, drawUpperPortion: false, drawDescriptionArea: false);
            }

            if (!hoverText.Equals(""))
            {
                drawHoverText(b, hoverText, Game1.smallFont);
            }

            heldItem?.drawInMenu(b, new Vector2(Game1.getOldMouseX() + 8, Game1.getOldMouseY() + 8), 1f);
            drawMouse(b);
            sparkleText?.draw(b,
                Utility.ModifyCoordinatesForUIScale(Game1.GlobalToLocal(Game1.viewport,
                    globalLocationOfSparklingItem)));
        }

        b.Draw(Game1.fadeToBlackRect,
            new Microsoft.Xna.Framework.Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
            Color.Black * blackFadeAlpha);
    }
}