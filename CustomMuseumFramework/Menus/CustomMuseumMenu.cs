using System;
using CustomMuseumFramework.Helpers;
using HarmonyLib;
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

public class CustomMuseumMenu : MenuWithInventory
{
    public const int startingState = 0;
    public const int placingInMuseumState = 1;
    public const int exitingState = 2;

    public int fadeTimer;
    public int state;
    public int menuPositionOffset;
    public bool fadeIntoBlack;
    public bool menuMovingDown;
    public float blackFadeAlpha;
    public SparklingText? sparkleText;
    public Vector2 globalLocationOfSparklingItem;

    public CustomMuseum Museum;

    private bool holdingMuseumItem;
    public bool reorganizing;

    public CustomMuseumMenu(InventoryMenu.highlightThisItem highlighterMethod) : base(highlighterMethod, okButton: true)
    {
        fadeTimer = 800;
        fadeIntoBlack = true;
        base.movePosition(0, Game1.uiViewport.Height - base.yPositionOnScreen - base.height);
        Game1.player.forceCanMove();
        Museum = Game1.currentLocation as CustomMuseum ??
                 throw new InvalidOperationException(
                     "The custom museum donation menu must be used from within a custom museum.");
        if (Game1.options.SnappyMenus)
        {
            if (base.okButton is not null)
            {
                base.okButton.myID = 106;
            }
            this.populateClickableComponentList();
            base.currentlySnappedComponent = base.getComponentWithID(0);
            this.snapCursorToCurrentSnappedComponent();
        }

        Game1.displayHUD = false;
    }

    public override bool shouldClampGamePadCursor()
    {
        return true;
    }
    
    public override void receiveKeyPress(Keys key)
	{
		if (this.fadeTimer > 0)
		{
			return;
		}
		if (Game1.options.doesInputListContain(Game1.options.menuButton, key) && !Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.menuButton) && this.readyToClose())
		{
			this.state = 2;
			this.fadeTimer = 500;
			this.fadeIntoBlack = true;
		}
		else if (Game1.options.doesInputListContain(Game1.options.menuButton, key) && !Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.menuButton) && !this.holdingMuseumItem && this.menuMovingDown)
		{
			if (base.heldItem != null)
			{
				Game1.playSound("bigDeSelect");
				Utility.CollectOrDrop(base.heldItem);
				base.heldItem = null;
			}
			this.ReturnToDonatableItems();
		}
		else if (Game1.options.SnappyMenus && base.heldItem == null && !this.reorganizing)
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
			if (base.heldItem == null && !reorganizing)
			{
				return;
			}
			CustomMuseum museum = Museum;
			Vector2 newCursorPositionTile = new Vector2((int)((Utility.ModifyCoordinateFromUIScale(Game1.getMouseX()) + (float)Game1.viewport.X) / 64f), (int)((Utility.ModifyCoordinateFromUIScale(Game1.getMouseY()) + (float)Game1.viewport.Y) / 64f));
			if (!museum.IsTileSuitableForMuseumItem((int)newCursorPositionTile.X, (int)newCursorPositionTile.Y) && (!reorganizing || !LibraryMuseum.HasDonatedArtifactAt(newCursorPositionTile)))
			{
				newCursorPositionTile = museum.GetFreeDonationSpot();
				Game1.setMousePosition((int)Utility.ModifyCoordinateForUIScale(newCursorPositionTile.X * 64f - (float)Game1.viewport.X + 32f), (int)Utility.ModifyCoordinateForUIScale(newCursorPositionTile.Y * 64f - (float)Game1.viewport.Y + 32f));
				return;
			}
			if (key == Game1.options.getFirstKeyboardKeyFromInputButtonList(Game1.options.moveUpButton))
			{
				newCursorPositionTile = museum.FindMuseumPieceLocationInDirection(newCursorPositionTile, 0, 21, !reorganizing);
			}
			else if (key == Game1.options.getFirstKeyboardKeyFromInputButtonList(Game1.options.moveRightButton))
			{
				newCursorPositionTile = museum.FindMuseumPieceLocationInDirection(newCursorPositionTile, 1, 21, !reorganizing);
			}
			else if (key == Game1.options.getFirstKeyboardKeyFromInputButtonList(Game1.options.moveDownButton))
			{
				newCursorPositionTile = museum.FindMuseumPieceLocationInDirection(newCursorPositionTile, 2, 21, !reorganizing);
			}
			else if (key == Game1.options.getFirstKeyboardKeyFromInputButtonList(Game1.options.moveLeftButton))
			{
				newCursorPositionTile = museum.FindMuseumPieceLocationInDirection(newCursorPositionTile, 3, 21, !reorganizing);
			}
			if (!Game1.viewport.Contains(new Location((int)(newCursorPositionTile.X * 64f + 32f), Game1.viewport.Y + 1)))
			{
				Game1.panScreen((int)(newCursorPositionTile.X * 64f - (float)Game1.viewport.X), 0);
			}
			else if (!Game1.viewport.Contains(new Location(Game1.viewport.X + 1, (int)(newCursorPositionTile.Y * 64f + 32f))))
			{
				Game1.panScreen(0, (int)(newCursorPositionTile.Y * 64f - (float)Game1.viewport.Y));
			}
			Game1.setMousePosition((int)Utility.ModifyCoordinateForUIScale((int)newCursorPositionTile.X * 64 - Game1.viewport.X + 32), (int)Utility.ModifyCoordinateForUIScale((int)newCursorPositionTile.Y * 64 - Game1.viewport.Y + 32));
		}
	}

	public override bool overrideSnappyMenuCursorMovementBan()
	{
		return false;
	}

	public override void receiveGamePadButton(Buttons b)
	{
		if (!menuMovingDown && (b == Buttons.DPadUp || b == Buttons.LeftThumbstickUp) && Game1.options.SnappyMenus && currentlySnappedComponent != null && currentlySnappedComponent.myID < 12)
		{
			reorganizing = true;
			menuMovingDown = true;
			receiveKeyPress(Game1.options.moveUpButton[0].key);
		}
	}
	
	public override void receiveLeftClick(int x, int y, bool playSound = true)
	{
		if (fadeTimer > 0)
		{
			return;
		}
		Item oldItem = base.heldItem;
		if (!holdingMuseumItem)
		{
			int inventory_index = base.inventory.getInventoryPositionOfClick(x, y);
			if (base.heldItem == null)
			{
				if (inventory_index >= 0 && inventory_index < base.inventory.actualInventory.Count && base.inventory.highlightMethod(base.inventory.actualInventory[inventory_index]))
				{
					base.heldItem = base.inventory.actualInventory[inventory_index].getOne();
					base.inventory.actualInventory[inventory_index].Stack--;
					if (base.inventory.actualInventory[inventory_index].Stack <= 0)
					{
						base.inventory.actualInventory[inventory_index] = null;
					}
				}
			}
			else
			{
				base.heldItem = base.inventory.leftClick(x, y, base.heldItem);
			}
		}
		if (oldItem == null && base.heldItem != null && Game1.isAnyGamePadButtonBeingPressed())
		{
			this.receiveGamePadButton(Buttons.DPadUp);
		}
		if (oldItem != null && base.heldItem != null && (y < Game1.viewport.Height - (base.height - (IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 192)) || this.menuMovingDown))
		{
			CustomMuseum museum = Museum;
			int mapXTile2 = (int)(Utility.ModifyCoordinateFromUIScale(x) + (float)Game1.viewport.X) / 64;
			int mapYTile2 = (int)(Utility.ModifyCoordinateFromUIScale(y) + (float)Game1.viewport.Y) / 64;
			if (museum.IsTileSuitableForMuseumItem(mapXTile2, mapYTile2) && museum.IsItemSuitableForDonation(base.heldItem))
			{
				string itemId2 = base.heldItem.QualifiedItemId;
				int rewardsCount = museum.GetRewardsForPlayer(Game1.player).Count;
				museum.DonatedItems.Add(new Vector2(mapXTile2, mapYTile2), base.heldItem.QualifiedItemId);
				Game1.playSound("stoneStep");
				if (museum.GetRewardsForPlayer(Game1.player).Count > rewardsCount && !holdingMuseumItem)
				{
					this.sparkleText = new SparklingText(Game1.dialogueFont, Game1.content.LoadString("Strings\\StringsFromCSFiles:NewReward"), Color.MediumSpringGreen, Color.White);
					Game1.playSound("reward");
					this.globalLocationOfSparklingItem = new Vector2((float)(mapXTile2 * 64 + 32) - this.sparkleText.textWidth / 2f, mapYTile2 * 64 - 48);
				}
				else
				{
					Game1.playSound("newArtifact");
				}
				Game1.player.completeQuest("24");
				base.heldItem.Stack--;
				if (base.heldItem.Stack <= 0)
				{
					base.heldItem = null;
				}
				int pieces = museum.DonatedItems.Length;
				if (!this.holdingMuseumItem)
				{
					Game1.stats.checkForArchaeologyAchievements();
					if (pieces == museum.TotalPossibleDonations)
					{
						Game1.Multiplayer.globalChatInfoMessage("MuseumComplete", Game1.player.farmName.Value);
					}
					else if (pieces == 40) // TODO: Custom milestones.
					{
						Game1.Multiplayer.globalChatInfoMessage("Museum40", Game1.player.farmName.Value);
					}
					else
					{
						Game1.Multiplayer.globalChatInfoMessage("donation", Game1.player.Name, TokenStringBuilder.ItemName(itemId2));
					}
				}
				ReturnToDonatableItems();
			}
		}
		else if (base.heldItem == null && !base.inventory.isWithinBounds(x, y))
		{
			int mapXTile = (int)(Utility.ModifyCoordinateFromUIScale(x) + (float)Game1.viewport.X) / 64;
			int mapYTile = (int)(Utility.ModifyCoordinateFromUIScale(y) + (float)Game1.viewport.Y) / 64;
			Vector2 v = new Vector2(mapXTile, mapYTile);
			CustomMuseum location = Museum;
			if (location.DonatedItems.TryGetValue(v, out var itemId))
			{
				base.heldItem = ItemRegistry.Create(itemId, 1, 0, allowNull: true);
				location.DonatedItems.Remove(v);
				if (base.heldItem != null)
				{
					this.holdingMuseumItem = !Museum.HasDonatedItem(base.heldItem.QualifiedItemId);
				}
			}
		}
		if (base.heldItem != null && oldItem == null)
		{
			menuMovingDown = true;
			reorganizing = false;
		}
		if (base.okButton != null && base.okButton.containsPoint(x, y) && this.readyToClose())
		{
			if (this.fadeTimer <= 0)
			{
				Game1.playSound("bigDeSelect");
			}
			this.state = 2;
			this.fadeTimer = 800;
			this.fadeIntoBlack = true;
		}
	}

	public virtual void ReturnToDonatableItems()
	{
		menuMovingDown = false;
		holdingMuseumItem = false;
		reorganizing = false;
		if (Game1.options.SnappyMenus)
		{
			base.movePosition(0, -this.menuPositionOffset);
			this.menuPositionOffset = 0;
			base.snapCursorToCurrentSnappedComponent();
		}
	}

	public override void emergencyShutDown()
	{
		if (heldItem is not null && holdingMuseumItem)
		{
			Vector2 tile = Museum.GetFreeDonationSpot();
			if (Museum.DonatedItems.TryAdd(tile, heldItem.QualifiedItemId))
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
			base.receiveRightClick(x, y);
		}

		if (heldItem is not null && oldItem is null)
		{
			menuMovingDown = true;
		}
	}
	
	public override void update(GameTime time)
	{
		base.update(time);
		if (sparkleText != null && this.sparkleText.update(time))
		{
			this.sparkleText = null;
		}
		if (this.fadeTimer > 0)
		{
			this.fadeTimer -= time.ElapsedGameTime.Milliseconds;
			if (this.fadeIntoBlack)
			{
				this.blackFadeAlpha = 0f + (1500f - (float)this.fadeTimer) / 1500f;
			}
			else
			{
				this.blackFadeAlpha = 1f - (1500f - (float)this.fadeTimer) / 1500f;
			}
			if (this.fadeTimer <= 0)
			{
				switch (this.state)
				{
				case 0:
					this.state = 1;
					Game1.viewportFreeze = true;
					Game1.viewport.Location = new Location(1152, 128);
					Game1.clampViewportToGameMap();
					this.fadeTimer = 800;
					this.fadeIntoBlack = false;
					break;
				case 2:
					Game1.viewportFreeze = false;
					this.fadeIntoBlack = false;
					this.fadeTimer = 800;
					this.state = 3;
					break;
				case 3:
					base.exitThisMenuNoSound();
					break;
				}
			}
		}
		if (this.menuMovingDown && this.menuPositionOffset < base.height / 3)
		{
			this.menuPositionOffset += 8;
			base.movePosition(0, 8);
		}
		else if (!this.menuMovingDown && this.menuPositionOffset > 0)
		{
			this.menuPositionOffset -= 8;
			base.movePosition(0, -8);
		}
		int mouseX = Game1.getOldMouseX(ui_scale: false) + Game1.viewport.X;
		int mouseY = Game1.getOldMouseY(ui_scale: false) + Game1.viewport.Y;
		if ((!Game1.options.SnappyMenus && Game1.lastCursorMotionWasMouse && mouseX - Game1.viewport.X < 64) || Game1.input.GetGamePadState().ThumbSticks.Right.X < 0f)
		{
			Game1.panScreen(-4, 0);
			if (Game1.input.GetGamePadState().ThumbSticks.Right.X < 0f)
			{
				this.snapCursorToCurrentMuseumSpot();
			}
		}
		else if ((!Game1.options.SnappyMenus && Game1.lastCursorMotionWasMouse && mouseX - (Game1.viewport.X + Game1.viewport.Width) >= -64) || Game1.input.GetGamePadState().ThumbSticks.Right.X > 0f)
		{
			Game1.panScreen(4, 0);
			if (Game1.input.GetGamePadState().ThumbSticks.Right.X > 0f)
			{
				this.snapCursorToCurrentMuseumSpot();
			}
		}
		if ((!Game1.options.SnappyMenus && Game1.lastCursorMotionWasMouse && mouseY - Game1.viewport.Y < 64) || Game1.input.GetGamePadState().ThumbSticks.Right.Y > 0f)
		{
			Game1.panScreen(0, -4);
			if (Game1.input.GetGamePadState().ThumbSticks.Right.Y > 0f)
			{
				this.snapCursorToCurrentMuseumSpot();
			}
		}
		else if ((!Game1.options.SnappyMenus && Game1.lastCursorMotionWasMouse && mouseY - (Game1.viewport.Y + Game1.viewport.Height) >= -64) || Game1.input.GetGamePadState().ThumbSticks.Right.Y < 0f)
		{
			Game1.panScreen(0, 4);
			if (Game1.input.GetGamePadState().ThumbSticks.Right.Y < 0f)
			{
				this.snapCursorToCurrentMuseumSpot();
			}
		}
		Keys[] pressedKeys = Game1.oldKBState.GetPressedKeys();
		foreach (Keys key in pressedKeys)
		{
			this.receiveKeyPress(key);
		}
	}

	private void snapCursorToCurrentMuseumSpot()
	{
		if (this.menuMovingDown)
		{
			Vector2 newCursorPositionTile = new Vector2((Game1.getMouseX(ui_scale: false) + Game1.viewport.X) / 64, (Game1.getMouseY(ui_scale: false) + Game1.viewport.Y) / 64);
			Game1.setMousePosition((int)newCursorPositionTile.X * 64 - Game1.viewport.X + 32, (int)newCursorPositionTile.Y * 64 - Game1.viewport.Y + 32, ui_scale: false);
		}
	}
	
	public override void gameWindowSizeChanged(Microsoft.Xna.Framework.Rectangle oldBounds, Microsoft.Xna.Framework.Rectangle newBounds)
	{
		base.gameWindowSizeChanged(oldBounds, newBounds);
		base.movePosition(0, Game1.viewport.Height - base.yPositionOnScreen - base.height);
		Game1.player.forceCanMove();
	}
	
	public override void draw(SpriteBatch b)
	{
		if ((this.fadeTimer <= 0 || !this.fadeIntoBlack) && this.state != 3)
		{
			if (base.heldItem != null)
			{
				Game1.StartWorldDrawInUI(b);
				for (int y = Game1.viewport.Y / 64 - 1; y < (Game1.viewport.Y + Game1.viewport.Height) / 64 + 2; y++)
				{
					for (int x = Game1.viewport.X / 64 - 1; x < (Game1.viewport.X + Game1.viewport.Width) / 64 + 1; x++)
					{
						if (this.Museum.IsTileSuitableForMuseumItem(x, y))
						{
							b.Draw(Game1.mouseCursors, Game1.GlobalToLocal(Game1.viewport, new Vector2(x, y) * 64f), Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 29), Color.LightGreen);
						}
					}
				}
				Game1.EndWorldDrawInUI(b);
			}
			if (!this.holdingMuseumItem)
			{
				base.draw(b, drawUpperPortion: false, drawDescriptionArea: false);
			}
			if (!base.hoverText.Equals(""))
			{
				IClickableMenu.drawHoverText(b, base.hoverText, Game1.smallFont);
			}
			base.heldItem?.drawInMenu(b, new Vector2(Game1.getOldMouseX() + 8, Game1.getOldMouseY() + 8), 1f);
			base.drawMouse(b);
			this.sparkleText?.draw(b, Utility.ModifyCoordinatesForUIScale(Game1.GlobalToLocal(Game1.viewport, this.globalLocationOfSparklingItem)));
		}
		b.Draw(Game1.fadeToBlackRect, new Microsoft.Xna.Framework.Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * this.blackFadeAlpha);
	}
}