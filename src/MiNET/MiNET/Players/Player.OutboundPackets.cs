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
		public override void BroadcastSetEntityData(MetadataDictionary metadata)
		{
			McpeSetEntityData mcpeSetEntityData = McpeSetEntityData.CreateObject();
			mcpeSetEntityData.runtimeEntityId = EntityManager.EntityIdSelf;
			mcpeSetEntityData.metadata = metadata;
			SendPacket(mcpeSetEntityData);

			base.BroadcastSetEntityData(metadata);
		}

		public void SendSetEntityData()
		{
			McpeSetEntityData mcpeSetEntityData = McpeSetEntityData.CreateObject();
			mcpeSetEntityData.runtimeEntityId = EntityManager.EntityIdSelf;
			mcpeSetEntityData.metadata = GetMetadata();
			SendPacket(mcpeSetEntityData);
		}

		public void SendSetDificulty()
		{
			McpeSetDifficulty mcpeSetDifficulty = McpeSetDifficulty.CreateObject();
			mcpeSetDifficulty.difficulty = (uint) Level.Difficulty;
			SendPacket(mcpeSetDifficulty);
		}

		public virtual void SendPlayerInventory()
		{
			//McpeInventoryContent strangeContent = McpeInventoryContent.CreateObject();
			//strangeContent.inventoryId = (byte) 0x7b;
			//strangeContent.input = new ItemStacks();
			//SendPacket(strangeContent);

			var inventoryContent = McpeInventoryContent.CreateObject();
			inventoryContent.inventoryId = (byte) WindowId.Inventory;
			inventoryContent.input = Inventory.GetSlots();
			inventoryContent.containerName = new FullContainerName { ContainerId = ContainerId.AnvilInput };
			SendPacket(inventoryContent);
			Log.Warn("Protocol v1001 inventory checkpoint: sent main InventoryContent");
			Thread.Sleep(500);

			SendPlayerArmor();

			var uiContent = McpeInventoryContent.CreateObject();
			uiContent.inventoryId = (byte) WindowId.UI;
			uiContent.input = Inventory.UiInventory.GetSlots();
			uiContent.containerName = new FullContainerName { ContainerId = ContainerId.AnvilInput };
			SendPacket(uiContent);
			Log.Warn("Protocol v1001 inventory checkpoint: sent UI InventoryContent");
			Thread.Sleep(500);

			var offHandContent = McpeInventoryContent.CreateObject();
			offHandContent.inventoryId = (byte) WindowId.Offhand;
			offHandContent.input = Inventory.GetOffHand();
			offHandContent.containerName = new FullContainerName { ContainerId = ContainerId.AnvilInput };
			SendPacket(offHandContent);
			Log.Warn("Protocol v1001 inventory checkpoint: sent offhand InventoryContent");
			Thread.Sleep(500);

			var mobEquipment = McpeMobEquipment.CreateObject();
			mobEquipment.runtimeEntityId = EntityManager.EntityIdSelf;
			mobEquipment.item = Inventory.GetItemInHand();
			mobEquipment.slot = (byte) Inventory.InHandSlot;
			mobEquipment.selectedSlot = (byte) Inventory.InHandSlot;
			SendPacket(mobEquipment);
			Log.Warn("Protocol v1001 inventory checkpoint: sent MobEquipment");
			Thread.Sleep(500);
		}

		public virtual void SendPlayerArmor()
		{
			var armorContent = McpeInventoryContent.CreateObject();
			armorContent.inventoryId = (byte) WindowId.Armor;
			armorContent.input = Inventory.GetArmor();
			armorContent.containerName = new FullContainerName { ContainerId = ContainerId.AnvilInput };
			SendPacket(armorContent);
			Log.Warn("Protocol v1001 inventory checkpoint: sent armor InventoryContent");
			Thread.Sleep(500);
		}

		public virtual void SendCraftingRecipes()
		{
			SendPacket(RecipeManager.GetCraftingData());
		}

		public virtual void SendCreativeInventory()
		{
			if (!UseCreativeInventory) return;

			SendPacket(InventoryUtils.GetCreativeInventoryData());
		}

		public virtual void SendItemRegistry()
		{
			SendPacket(InventoryUtils.GetItemRegistryData());
		}

		private void SendChunkRadiusUpdate()
		{
			McpeChunkRadiusUpdate packet = McpeChunkRadiusUpdate.CreateObject();
			packet.chunkRadius = ChunkRadius;

			SendPacket(packet);
		}

		public void SendPlayerStatus(int status)
		{
			McpePlayStatus mcpePlayerStatus = McpePlayStatus.CreateObject();
			mcpePlayerStatus.status = status;
			SendPacket(mcpePlayerStatus);
		}

		[Wired]
		public void SetGameMode(GameMode gameMode)
		{
			GameMode = gameMode;

			SendSetPlayerGameType();
			SendAbilities();
		}


		public void SendSetPlayerGameType()
		{
			McpeSetPlayerGameType gametype = McpeSetPlayerGameType.CreateObject();
			gametype.gamemode = (int) GameMode;
			SendPacket(gametype);
		}

		[Wired]
		public void StrikeLightning()
		{
			Lightning lightning = new Lightning(Level) {KnownPosition = KnownPosition};

			if (lightning.Level == null) return;

			lightning.SpawnEntity();
		}
	}
}
