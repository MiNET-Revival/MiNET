#region LICENSE

// The contents of this file are subject to the Common Public Attribution
// License Version 1.0. (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// https://github.com/NiclasOlofsson/MiNET/blob/master/LICENSE.
// The License is based on the Mozilla Public License Version 1.1, but Sections 14
// and 15 have been added to cover use of software over a computer network and
// provide for limited attribution for the Original Developer. In addition, Exhibit A has
// been modified to be consistent with Exhibit B.
// 
// Software distributed under the License is distributed on an "AS IS" basis,
// WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
// the specific language governing rights and limitations under the License.
// 
// The Original Code is MiNET.
// 
// The Original Developer is the Initial Developer.  The Initial Developer of
// the Original Code is Niclas Olofsson.
// 
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2020 Niclas Olofsson.
// All Rights Reserved.

#endregion

using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using fNbt;
using log4net;
using MiNET.BlockEntities;
using MiNET.Blocks;
using MiNET.Blocks.States;
using MiNET.Crafting;
using MiNET.Effects;
using MiNET.Entities;
using MiNET.Entities.Passive;
using MiNET.Entities.World;
using MiNET.Inventories;
using MiNET.Items;
using MiNET.Net;
using MiNET.Particles;
using MiNET.UI;
using MiNET.Utils;
using MiNET.Utils.Metadata;
using MiNET.Utils.Nbt;
using MiNET.Utils.Skins;
using MiNET.Utils.Vectors;
using MiNET.Worlds;
using Newtonsoft.Json;

namespace MiNET.Players
{
	public partial class Player
	{
		private object _inventorySync = new object();

		public virtual void SetOpenInventory(IInventory inventory)
		{
			if (_openInventory is ContainerInventory inv)
			{
				inv.InventoryChanged -= OnInventoryChanged;
			}

			if (inventory is ContainerInventory newInv)
			{
				newInv.InventoryChanged += OnInventoryChanged;
			}

			_openInventory = inventory;
		}

		public virtual IInventory GetOpenInventory()
		{
			return _openInventory;
		}

		public virtual void CloseOpenedInventory()
		{
			if (_openInventory == null) return;

			HandleMcpeContainerClose(null);
		}

		public void OpenInventory(BlockCoordinates inventoryCoord)
		{
			lock (_inventorySync)
			{
				var blockEntity = Level.GetBlockEntity(inventoryCoord) as ContainerBlockEntityBase;
				if (blockEntity == null)
				{
					Log.Warn($"No inventory found at {inventoryCoord}");
					return;
				}

				blockEntity.Open(this);
			}
		}

		protected virtual void OnInventoryChanged(object sender, InventoryChangeEventArgs args)
		{
			
		}

		public void HandleMcpeInventorySlot(McpeInventorySlot message)
		{
		}

		public virtual void HandleMcpeInventoryTransaction(McpeInventoryTransaction message)
		{
			switch (message.transaction)
			{
				case InventoryMismatchTransaction inventoryMismatchTransaction:
					HandleInventoryMismatchTransaction(inventoryMismatchTransaction);
					break;
				case ItemReleaseTransaction itemReleaseTransaction:
					HandleItemReleaseTransaction(itemReleaseTransaction);
					break;
				case ItemUseOnEntityTransaction itemUseOnEntityTransaction:
					HandleItemUseOnEntityTransaction(itemUseOnEntityTransaction);
					break;
				case ItemUseTransaction itemUseTransaction:
					HandleItemUseTransaction(itemUseTransaction);
					break;
				case NormalTransaction normalTransaction:
					HandleNormalTransaction(normalTransaction);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected virtual void HandleItemUseOnEntityTransaction(ItemUseOnEntityTransaction transaction)
		{
			switch ((McpeInventoryTransaction.ItemUseOnEntityAction) transaction.ActionType)
			{
				case McpeInventoryTransaction.ItemUseOnEntityAction.Interact: // Right click
					EntityInteract(transaction);
					break;
				case McpeInventoryTransaction.ItemUseOnEntityAction.Attack: // Left click
					EntityAttack(transaction);
					break;
				case McpeInventoryTransaction.ItemUseOnEntityAction.ItemInteract:
					Log.Warn($"Got Entity ItemInteract. Was't sure it existed, but obviously it does :-o");
					EntityItemInteract(transaction);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void EntityItemInteract(ItemUseOnEntityTransaction transaction)
		{
			Item itemInHand = Inventory.GetItemInHand();
			if (itemInHand.Id != transaction.Item.Id || itemInHand.Metadata != transaction.Item.Metadata)
			{
				Log.Warn($"Attack item mismatch. Expected {itemInHand}, but client reported {transaction.Item}");
			}

			if (!Level.TryGetEntity(transaction.RuntimeEntityId, out Entity target)) return;
			target.DoItemInteraction(this, itemInHand);
		}

		protected virtual void EntityInteract(ItemUseOnEntityTransaction transaction)
		{
			DoInteraction((int) transaction.ActionType, this);

			if (!Level.TryGetEntity(transaction.RuntimeEntityId, out Entity target)) return;
			target.DoInteraction((int) transaction.ActionType, this);
		}

		protected virtual void EntityAttack(ItemUseOnEntityTransaction transaction)
		{
			Item itemInHand = Inventory.GetItemInHand();
			if (itemInHand.Id != transaction.Item.Id || itemInHand.Metadata != transaction.Item.Metadata)
			{
				Log.Warn($"Attack item mismatch. Expected {itemInHand}, but client reported {transaction.Item}");
			}

			if (!Level.TryGetEntity(transaction.RuntimeEntityId, out Entity target)) return;


			LastAttackTarget = target;

			Player player = target as Player;
			if (player != null)
			{
				double damage = DamageCalculator.CalculateItemDamage(this, itemInHand, player);

				if (IsFalling)
				{
					damage += DamageCalculator.CalculateFallDamage(this, damage, player);
				}

				damage += DamageCalculator.CalculateEffectDamage(this, damage, player);

				if (damage < 0) damage = 0;

				damage += DamageCalculator.CalculateDamageIncreaseFromEnchantments(this, itemInHand, player);
				var reducedDamage = (int) DamageCalculator.CalculatePlayerDamage(this, player, itemInHand, damage, DamageCause.EntityAttack);
				player.HealthManager.TakeHit(this, itemInHand, reducedDamage, DamageCause.EntityAttack);
				if (reducedDamage < damage)
				{
					player.Inventory.DamageArmor();
				}
				var fireAspectLevel = itemInHand.GetEnchantingLevel(EnchantingType.FireAspect);
				if (fireAspectLevel > 0)
				{
					player.HealthManager.Ignite(fireAspectLevel * 80);
				}
			}
			else
			{
				// This is totally wrong. Need to merge with the above damage calculation
				target.HealthManager.TakeHit(this, itemInHand, CalculateDamage(target), DamageCause.EntityAttack);
			}

			Inventory.DamageItemInHand(ItemDamageReason.EntityAttack, target, null);
			HungerManager.IncreaseExhaustion(0.3f);
		}

		protected virtual void HandleInventoryMismatchTransaction(InventoryMismatchTransaction transaction)
		{
			Log.Warn($"Transaction mismatch");
		}

		protected virtual void HandleItemReleaseTransaction(ItemReleaseTransaction transaction)
		{
			Item itemInHand = Inventory.GetItemInHand();

			switch (transaction.ActionType)
			{
				case McpeInventoryTransaction.ItemReleaseAction.Release:
				{
					itemInHand.Release(Level, this, transaction.FromPosition);
					break;
				}
				case McpeInventoryTransaction.ItemReleaseAction.Use:
				{
					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}

			HandleTransactionRecords(transaction.TransactionRecords);
		}

		protected virtual void HandleItemUseTransaction(ItemUseTransaction transaction)
		{
			var itemInHand = Inventory.GetItemInHand();

			switch (transaction.ActionType)
			{
				case McpeInventoryTransaction.ItemUseAction.Place:
				{
					Level.Interact(this, itemInHand, transaction.Position, (BlockFace) transaction.Face, transaction.ClickPosition);
					break;
				}
				case McpeInventoryTransaction.ItemUseAction.Use:
				{
					itemInHand.UseItem(Level, this, transaction.Position);
					if (itemInHand.Count == 0)
					{
						Inventory.SetInventorySlot(Inventory.InHandSlot, null, true);
					}
					break;
				}
				case McpeInventoryTransaction.ItemUseAction.Destroy:
				{
					//TODO: Add face and other parameters to break. For logic in break block.
					Level.BreakBlock(this, transaction.Position, (BlockFace) transaction.Face);
					break;
				}
			}

			HandleTransactionRecords(transaction.TransactionRecords);
		}

		protected virtual void HandleNormalTransaction(NormalTransaction transaction)
		{
			HandleTransactionRecords(transaction.TransactionRecords);
		}

		protected virtual void HandleTransactionRecords(List<TransactionRecord> records)
		{
			if (records.Count == 0) return;

			foreach (TransactionRecord record in records)
			{
				//Item oldItem = record.OldItem;
				Item newItem = record.NewItem;
				//int slot = record.Slot;

				switch (record)
				{
					//case ContainerTransactionRecord rec:
					//{
					//	int inventoryId = rec.InventoryId;

					//	switch (inventoryId)
					//	{
					//		case 0: // Player inventory
					//		{
					//			Item existingItem = Inventory.Slots[slot];

					//			if (!newItem.Equals(existingItem)) Log.Warn($"Inventory mismatch. Client reported new item as {oldItem} and it did not match existing item {existingItem}");
					//			else Log.Debug($"Verified new inventory slot {slot} to {Inventory.Slots[slot]}");
					//			break;
					//		}
					//		case 119: // Armor inventory
					//		case 120: // Armor inventory
					//		case 121: // Creative inventory
					//		case 124: // Cursor inventory
					//			throw new Exception($"This should never happen with new inventory transactions");
					//		default:
					//		{
					//			throw new Exception($"This should never happen with new inventory transactions");
					//			////TODO Handle custom items, like player inventory and cursor

					//			//if (_openInventory != null)
					//			//{
					//			//	if (_openInventory is Inventory inventory && inventory.WindowsId == inventoryId)
					//			//	{
					//			//		//if (!oldItem.Equals(inventory.CraftRemoveIngredient((byte)slot))) Log.Warn($"Cursor mismatch. Client reported old item as {oldItem} and it did not match existing the item {inventory.CraftRemoveIngredient((byte)slot)}");

					//			//		// block inventories of various kinds (chests, furnace, etc)
					//			//		inventory.SetSlot(this, (byte) slot, newItem);
					//			//	}
					//			//	else if (_openInventory is HorseInventory horseInventory)
					//			//	{
					//			//		//if (!oldItem.Equals(horseInventory.CraftRemoveIngredient((byte)slot))) Log.Warn($"Cursor mismatch. Client reported old item as {oldItem} and it did not match existing the item {horseInventory.CraftRemoveIngredient((byte)slot)}");
					//			//		horseInventory.SetSlot(slot, newItem);
					//			//	}
					//			//}
					//			//break;
					//		}
					//	}
					//	break;
					//}

					//TODO Handle custom items, like player inventory and cursor. Not entirely sure how to handle this for crafting and similar inventories.
					case CraftTransactionRecord _:
					{
						throw new Exception($"This should never happen with new inventory transactions");
					}
					case CreativeTransactionRecord _:
					{
						throw new Exception($"This should never happen with new inventory transactions");
					}
					case WorldInteractionTransactionRecord _:
					{
						// Drop
						Item sourceItem = Inventory.GetItemInHand();

						if (newItem.Id != sourceItem.Id) Log.Warn($"Inventory mismatch. Client reported drop item as {newItem} and it did not match existing item {sourceItem}");

						byte count = newItem.Count;

						Item dropItem;
						if (sourceItem.Count == count)
						{
							dropItem = sourceItem;
							Inventory.ClearInventorySlot((byte) Inventory.InHandSlot);
						}
						else
						{
							dropItem = (Item) sourceItem.Clone();
							sourceItem.Count -= count;
							dropItem.Count = count;
							dropItem.UniqueId = Item.GetUniqueId();
						}

						DropItem(dropItem);
						break;
					}
				}
			}
		}

		public virtual ItemEntity DropItem(Item item)
		{
			var itemEntity = new ItemEntity(Level, item)
			{
				Velocity = KnownPosition.GetDirectionVector().Normalize() * 0.3f,
				KnownPosition = KnownPosition + new Vector3(0f, 1.62f, 0f)
			};
			itemEntity.SpawnEntity();

			return itemEntity;
		}

		public virtual bool PickUpItem(ItemEntity item)
		{
			return Inventory.SetFirstEmptySlot(item.Item, true);
		}

		public virtual void HandleMcpeContainerClose(McpeContainerClose message)
		{
			lock (_inventorySync)
			{
				if (_openInventory != null)
				{
					if (message != null && message.windowId != (byte) _openInventory.WindowId) return;

					_openInventory.Close(this, message != null);
				}
				else
				{
					var closePacket = McpeContainerClose.CreateObject();
					closePacket.windowId = 0;
					closePacket.windowType = (sbyte) WindowType.Inventory;
					closePacket.server = message == null ? true : false;
					SendPacket(closePacket);

					Inventory.CloseUiInventory();
				}
			}
		}

		public void HandleMcpePlayerHotbar(McpePlayerHotbar message)
		{
		}

		public void HandleMcpeInventoryContent(McpeInventoryContent message)
		{
		}

		/// <summary>
		///     Handles the interact.
		/// </summary>
		/// <param name="message">The message.</param>
		public virtual void HandleMcpeInteract(McpeInteract message)
		{
			//Log.Info($"Interact. Target={message.targetRuntimeEntityId} Action={message.actionId} Position={message.Position}");
			Entity target = null;
			long runtimeEntityId = message.targetRuntimeEntityId;
			if (runtimeEntityId == EntityManager.EntityIdSelf)
			{
				target = this;
			}
			else if (!Level.TryGetEntity(runtimeEntityId, out target))
			{
				return;
			}

			if (message.actionId != 4)
			{
				Log.Debug($"Interact Action ID: {message.actionId}");
				Log.Debug($"Interact Target Entity ID: {runtimeEntityId}");
			}

			if (target == null) return;
			switch ((McpeInteract.Actions)message.actionId)
			{
				case McpeInteract.Actions.LeaveVehicle:
				{
					if (Level.TryGetEntity(Vehicle, out Mob mob))
					{
						mob.Unmount(this);
					}

					break;
				}
				case McpeInteract.Actions.MouseOver:
				{
					// Mouse over
					DoMouseOverInteraction(message.actionId, this);
					target.DoMouseOverInteraction(message.actionId, this);
					break;
				}
				case McpeInteract.Actions.OpenInventory:
				{
					if (target == this)
					{
						Inventory.Open();
					}
					else if (IsRiding) // Riding; Open inventory
					{
						if (Level.TryGetEntity(Vehicle, out Mob mob) && mob is Horse horse)
						{
							horse.Inventory.Open(this);
						}
					}

					break;
				}
			}
		}
	}
}
