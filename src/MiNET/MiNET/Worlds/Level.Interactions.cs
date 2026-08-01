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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Transactions;
using fNbt;
using log4net;
using MiNET.BlockEntities;
using MiNET.Blocks;
using MiNET.Entities;
using MiNET.Entities.Hostile;
using MiNET.Entities.Passive;
using MiNET.Entities.World;
using MiNET.Inventories;
using MiNET.Items;
using MiNET.Net;
using MiNET.Net.RakNet;
using MiNET.Sounds;
using MiNET.Utils;
using MiNET.Utils.Diagnostics;
using MiNET.Utils.IO;
using MiNET.Utils.Nbt;
using MiNET.Utils.Vectors;
using MiNET.Worlds.Anvil;

namespace MiNET.Worlds
{
	public partial class Level
	{
		public event EventHandler<BlockPlaceEventArgs> BlockPlace;

		public virtual bool OnBlockPlace(BlockPlaceEventArgs e)
		{
			BlockPlace?.Invoke(this, e);

			return !e.Cancel;
		}

		public void Interact(Player player, Item itemInHand, BlockCoordinates blockCoordinates, BlockFace face, Vector3 faceCoords)
		{
			Block target = GetBlock(blockCoordinates);
			if (!player.IsSneaking && target.Interact(this, player, blockCoordinates, face, faceCoords)) return; // Handled in block interaction

			Log.Debug($"Item in hand: {itemInHand}");
			if (itemInHand is ItemBlock)
			{
				Block block = GetBlock(blockCoordinates);
				if (!block.IsReplaceable)
				{
					block = GetBlock(itemInHand.GetNewCoordinatesFromFace(blockCoordinates, face));
				}

				if (!AllowBuild || player.GameMode == GameMode.Spectator || !OnBlockPlace(new BlockPlaceEventArgs(player, this, target, block)))
				{
					// Revert

					player.SendPlayerInventory();

					var message = McpeUpdateBlock.CreateObject();
					message.blockRuntimeId = (uint) block.RuntimeId;
					message.coordinates = block.Coordinates;
					message.blockPriority = 0x13;
					player.SendPacket(message);

					return;
				}
			}

			itemInHand.PlaceBlock(this, player, blockCoordinates, face, faceCoords);
		}

		public void UseItem(Player player, Item itemInHand, BlockCoordinates blockCoordinates, BlockFace face)
		{
			itemInHand.UseItem(this, player, blockCoordinates);
			if (itemInHand.Count == 0)
			{
				player.Inventory.SetInventorySlot(player.Inventory.InHandSlot, null, true);
			}

			if (!player.IsSneaking)
			{
				Block target = GetBlock(blockCoordinates);
				target.UseItem(this, player, blockCoordinates, face);
			}
		}

		public event EventHandler<BlockBreakEventArgs> BlockBreak;

		protected virtual bool OnBlockBreak(BlockBreakEventArgs e)
		{
			BlockBreak?.Invoke(this, e);

			return !e.Cancel;
		}

		public void BreakBlock(Player player, BlockCoordinates blockCoordinates, BlockFace face = BlockFace.None)
		{
			Block block = GetBlock(blockCoordinates);
			BlockEntity blockEntity = GetBlockEntity(blockCoordinates);

			Item inHand = player.Inventory.GetItemInHand();
			bool canBreak = inHand.BreakBlock(this, player, block, blockEntity);

			if (!canBreak || !AllowBreak || player.GameMode == GameMode.Spectator || !OnBlockBreak(new BlockBreakEventArgs(player, this, block, null)))
			{
				// Revert

				RevertBlockAction(player, block, blockEntity);
			}
			else
			{
				BreakBlock(player, block, blockEntity, inHand, face);
				SendSyncedBreakUpdate(player, blockCoordinates);

				player.Inventory.DamageItemInHand(ItemDamageReason.BlockBreak, null, block);
				player.HungerManager.IncreaseExhaustion(0.025f);
				player.ExperienceManager.AddExperience(block.GetExperiencePoints());
			}
		}

		private static void SendSyncedBreakUpdate(Player player, BlockCoordinates blockCoordinates)
		{
			if (player == null) return;

			var air = new Air {Coordinates = blockCoordinates};
			var updateBlock = McpeUpdateBlockSynced.CreateObject();
			updateBlock.coordinates = blockCoordinates;
			updateBlock.blockRuntimeId = (uint) air.RuntimeId;
			updateBlock.blockPriority = 0x13;
			updateBlock.dataLayerId = 0;
			updateBlock.unknown0 = player.EntityId;
			updateBlock.unknown1 = 1;

			player.SendPacket(updateBlock);
		}

		private static void RevertBlockAction(Player player, Block block, BlockEntity blockEntity)
		{
			var message = McpeUpdateBlock.CreateObject();
			message.blockRuntimeId = (uint) block.RuntimeId;
			message.coordinates = block.Coordinates;
			message.blockPriority = 0x13;
			player.SendPacket(message);

			// Revert block entity if exists
			if (blockEntity != null)
			{
				blockEntity.SendData(player);
			}
		}

		public void BreakBlock(Block block, BlockEntity blockEntity = null, Item tool = null, BlockFace face = BlockFace.None)
		{
			BreakBlock(null, block, blockEntity, tool, face);
		}

		public void BreakBlock(Player player, Block block, BlockEntity blockEntity = null, Item tool = null, BlockFace face = BlockFace.None)
		{
			block.BreakBlock(this, face);
			var drops = new List<Item>();
			drops.AddRange(block.GetDrops(this, tool ?? new ItemAir()));

			if (blockEntity != null)
			{
				RemoveBlockEntity(block.Coordinates);
				drops.AddRange(blockEntity.GetDrops());
			}

			if ((player != null && player.GameMode == GameMode.Survival) || (player == null && GameMode == GameMode.Survival))
			{
				foreach (Item drop in drops)
				{
					DropItem(block.Coordinates, drop);
				}
			}
		}


		public virtual void DropItem(Vector3 coordinates, Item drop)
		{
			if (GameMode == GameMode.Creative) return;

			if (drop == null) return;
			if (drop is ItemAir) return;
			if (drop.Count == 0) return;

			if (AutoSmelt) drop = drop.GetSmelt(BlockFactory.GetIdByType<Furnace>(false)) ?? drop;

			Random random = new Random();
			var itemEntity = new ItemEntity(this, drop)
			{
				KnownPosition =
				{
					X = (float) coordinates.X + 0.5f,
					Y = (float) coordinates.Y + 0.5f,
					Z = (float) coordinates.Z + 0.5f
				},
				Velocity = new Vector3((float) (random.NextDouble() * 0.005), (float) (random.NextDouble() * 0.20), (float) (random.NextDouble() * 0.005))
			};

			itemEntity.SpawnEntity();
		}
	}
}
