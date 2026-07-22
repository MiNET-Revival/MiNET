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
		public long Vehicle { get; set; }

		public virtual void HandleMcpeBlockPickRequest(McpeBlockPickRequest message)
		{
			var block = Level.GetBlock(message.x, message.y, message.z);
			Log.Debug($"Picked block {block.Id} from blockstate {block.RuntimeId}. Expected block to be in slot {message.selectedSlot}");

			var item = block.GetItem(Level);
			if (item is ItemBlock blockItem)
			{
				Log.Debug($"Have BlockItem with block state {blockItem.Block?.RuntimeId}");
			}

			if (item == null) return;

			for (var i = 0; i < PlayerInventory.HotbarSize; i++)
			{
				if (Inventory.Slots[i].Equals(item))
				{
					Inventory.SetHeldItemSlot(i);
					return;
				}
			}

			if (GameMode == GameMode.Creative)
			{
				Inventory.SetInventorySlot(Inventory.InHandSlot, item, true);
			}
		}

		public virtual void HandleMcpeEntityPickRequest(McpeEntityPickRequest message)
		{
			if (GameMode != GameMode.Creative)
			{
				return;
			}

			if (Level.Entities.TryGetValue((long) message.runtimeEntityId, out var entity))
			{
				Item item = new ItemSpawnEgg(EntityHelpers.ToEntityType(entity.EntityTypeId));

				Inventory.SetInventorySlot(Inventory.InHandSlot, item);
			}
		}

		protected virtual int CalculateDamage(Entity target)
		{
			int damage = Inventory.GetItemInHand().GetDamage(); //Item Damage.

			damage = (int) Math.Floor(damage * (1.0));

			return damage;
		}


		public virtual void HandleMcpeEntityEvent(McpeEntityEvent message)
		{
			if (Log.IsDebugEnabled)
			{
				Log.Debug("Entity Id:" + message.runtimeEntityId);
				Log.Debug("Entity Event Id:" + message.eventId);
				Log.Debug("Entity Event unknown:" + message.data);
			}

			switch (message.eventId)
			{
				case 34:
				{
					ExperienceManager.RemoveExperienceLevels(message.data);
					break;
				}
				case 57:
				{
					int data = message.data;
					if (data != 0) BroadcastEntityEvent(57, data);
					break;
				}
			}
		}
	}
}
