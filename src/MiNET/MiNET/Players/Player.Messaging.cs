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
		public override void DespawnEntity()
		{
			IsSpawned = false;
			Level.DespawnFromAll(this);
		}

		public virtual void SendTitle(string text, TitleType type = TitleType.Title, int fadeIn = 6, int fadeOut = 6, int stayTime = 20, Player sender = null)
		{
			Level.BroadcastTitle(text, type, fadeIn, fadeOut, stayTime, sender, new[] {this});
		}

		public virtual void SendMessage(string text, MessageType type = MessageType.Chat, Player sender = null, bool needsTranslation = false, string[] parameters = null)
		{
			Level.BroadcastMessage(text, type, sender, new[] {this}, needsTranslation, parameters);
		}

		public override void BroadcastEntityEvent()
		{
			BroadcastEntityEvent(HealthManager.Health <= 0 ? 3 : 2);

			if (HealthManager.IsDead)
			{
				Player player = HealthManager.LastDamageSource as Player;
				BroadcastDeathMessage(player, HealthManager.LastDamageCause);
			}
		}

		public void BroadcastEntityEvent(int eventId, int data = 0)
		{
			{
				var entityEvent = McpeEntityEvent.CreateObject();
				entityEvent.runtimeEntityId = EntityManager.EntityIdSelf;
				entityEvent.eventId = (byte) eventId;
				entityEvent.data = data;
				SendPacket(entityEvent);
			}
			{
				var entityEvent = McpeEntityEvent.CreateObject();
				entityEvent.runtimeEntityId = EntityId;
				entityEvent.eventId = (byte) eventId;
				entityEvent.data = data;
				Level.RelayBroadcast(this, entityEvent);
			}
		}

		public virtual void BroadcastDeathMessage(Player player, DamageCause lastDamageCause)
		{
			string deathMessage = string.Format(HealthManager.GetDescription(lastDamageCause), Username, player == null ? "" : player.Username);
			Level.BroadcastMessage(deathMessage, type: MessageType.Raw);
			Log.Debug(deathMessage);
		}

		public virtual void SendDeathInfo(DamageCause damageCause, Entity damageSource)
		{
			string translationKey = damageCause switch
			{
				DamageCause.Contact => "death.attack.cactus",
				DamageCause.EntityAttack => damageSource is Player ? "death.attack.player" : "death.attack.mob",
				DamageCause.Projectile => "death.attack.arrow",
				DamageCause.Suffocation => "death.attack.inWall",
				DamageCause.Fall => "death.attack.fall",
				DamageCause.Fire => "death.attack.inFire",
				DamageCause.FireTick => "death.attack.onFire",
				DamageCause.Lava => "death.attack.lava",
				DamageCause.Drowning => "death.attack.drown",
				DamageCause.BlockExplosion or DamageCause.EntityExplosion => "death.attack.explosion",
				DamageCause.Void => "death.attack.outOfWorld",
				DamageCause.Magic => "death.attack.magic",
				DamageCause.Starving => "death.attack.starve",
				_ => "death.attack.generic"
			};

			bool includesDamageSource = damageCause is DamageCause.EntityAttack or DamageCause.Projectile;
			string damageSourceName = damageSource switch
			{
				Player sourcePlayer => sourcePlayer.Username,
				{ NameTag.Length: > 0 } => damageSource.NameTag,
				not null => damageSource.EntityTypeId.ToString(),
				_ => string.Empty
			};

			var deathInfo = McpeDeathInfo.CreateObject();
			deathInfo.messageTranslationKey = translationKey;
			deathInfo.messageParameters = includesDamageSource
				? new[] { Username, damageSourceName }
				: new[] { Username };
			SendPacket(deathInfo);
		}

		/// <summary>
		///     Very important litle method. This does all the sending of packets for
		///     the player class. Treat with respect!
		/// </summary>
		public virtual void SendPacket(Packet packet)
		{
			if (NetworkHandler == null)
			{
				packet.PutPool();
			}
			else
			{
				NetworkHandler?.SendPacket(packet);
			}
		}

		private object _sendMoveListSync = new object();
		private DateTime _lastMoveListSendTime = DateTime.UtcNow;

		public void SendMoveList(McpeWrapper batch, DateTime sendTime)
		{
			if (sendTime < _lastMoveListSendTime || !Monitor.TryEnter(_sendMoveListSync))
			{
				batch.PutPool();
				return;
			}

			_lastMoveListSendTime = sendTime;

			try
			{
				SendPacket(batch);
			}
			finally
			{
				Monitor.Exit(_sendMoveListSync);
			}
		}

		public void CleanCache()
		{
			lock (_sendChunkSync)
			{
				_chunksUsed.Clear();
			}
		}

		public void CleanCache(ChunkColumn chunk)
		{
			lock (_sendChunkSync)
			{
				_chunksUsed.Remove(new ChunkCoordinates(chunk.X, chunk.Z));
			}
		}

		public virtual void DropInventory()
		{
			var slots = Inventory.Slots;
			var uiSlots = Inventory.UiInventory.Slots;

			Vector3 coordinates = KnownPosition.ToVector3();
			coordinates.Y += 0.5f;

			foreach (var stack in slots.ToArray())
			{
				Level.DropItem(coordinates, stack);
			}

			foreach (var stack in uiSlots.ToArray())
			{
				Level.DropItem(coordinates, stack);
			}

			if (Inventory.Helmet is not ItemAir)
			{
				Level.DropItem(coordinates, Inventory.Helmet);
				Inventory.Helmet = new ItemAir();
			}

			if (Inventory.Chest is not ItemAir)
			{
				Level.DropItem(coordinates, Inventory.Chest);
				Inventory.Chest = new ItemAir();
			}

			if (Inventory.Leggings is not ItemAir)
			{
				Level.DropItem(coordinates, Inventory.Leggings);
				Inventory.Leggings = new ItemAir();
			}

			if (Inventory.Boots is not ItemAir)
			{
				Level.DropItem(coordinates, Inventory.Boots);
				Inventory.Boots = new ItemAir();
			}

			Inventory.Clear();
		}

		public override void SpawnToPlayers(Player[] players)
		{
			McpeAddPlayer mcpeAddPlayer = McpeAddPlayer.CreateObject();
			mcpeAddPlayer.uuid = ClientUuid;
			mcpeAddPlayer.username = Username;
			mcpeAddPlayer.entityIdSelf = (ulong) EntityId;
			mcpeAddPlayer.runtimeEntityId = EntityId;
			mcpeAddPlayer.x = KnownPosition.X;
			mcpeAddPlayer.y = KnownPosition.Y;
			mcpeAddPlayer.z = KnownPosition.Z;
			mcpeAddPlayer.speedX = Velocity.X;
			mcpeAddPlayer.speedY = Velocity.Y;
			mcpeAddPlayer.speedZ = Velocity.Z;
			mcpeAddPlayer.yaw = KnownPosition.Yaw;
			mcpeAddPlayer.headYaw = KnownPosition.HeadYaw;
			mcpeAddPlayer.pitch = KnownPosition.Pitch;
			mcpeAddPlayer.metadata = GetMetadata();
			/*mcpeAddPlayer.flags = GetAdventureFlags();
			mcpeAddPlayer.commandPermission = (uint) CommandPermission;
			mcpeAddPlayer.actionPermissions = (uint) ActionPermissions;
			mcpeAddPlayer.permissionLevel = (uint) PermissionLevel;
			mcpeAddPlayer.userId = -1;*/
			mcpeAddPlayer.deviceId = PlayerInfo.DeviceId;
			mcpeAddPlayer.deviceOs = PlayerInfo.DeviceOS;
			mcpeAddPlayer.gameType = (uint) GameMode;
			mcpeAddPlayer.layers = GetAbilities();

			int[] a = new int[5];

			//NOT WORKING: Reported to Mojang
			//if (IsRiding)
			//{
			//	mcpeAddPlayer.links = new Links()
			//	{
			//		new Tuple<long, long>(Vehicle, EntityId)
			//	};
			//}

			Level.RelayBroadcast(this, players, mcpeAddPlayer);

			if (IsRiding)
			{
				// This works if entities are spawned before players.

				McpeSetEntityLink link = McpeSetEntityLink.CreateObject();
				link.linkType = (byte) McpeSetEntityLink.LinkActions.Ride;
				link.riderId = EntityId;
				link.riddenId = Vehicle;
				Level.RelayBroadcast(players, link);
			}

			SendEquipmentForPlayer(players);
			SendArmorEquipmentForPlayer(players);
		}

		public virtual void SendEquipmentForPlayer(Player[] receivers = null)
		{
			SendEquipmentForPlayer(WindowId.Inventory, Inventory.GetItemInHand(), receivers);
			SendEquipmentForPlayer(WindowId.Offhand, Inventory.OffHand, receivers);
		}

		protected virtual void SendEquipmentForPlayer(WindowId windowsId, Item item, Player[] receivers = null)
		{
			var mcpePlayerEquipment = McpeMobEquipment.CreateObject();
			mcpePlayerEquipment.runtimeEntityId = EntityId;
			mcpePlayerEquipment.windowsId = (byte) windowsId;
			mcpePlayerEquipment.item = item;
			mcpePlayerEquipment.slot = 0;
			if (receivers == null)
			{
				Level.RelayBroadcast(this, mcpePlayerEquipment);
			}
			else
			{
				Level.RelayBroadcast(this, receivers, mcpePlayerEquipment);
			}
		}

		public virtual void SendArmorEquipmentForPlayer(Player[] receivers = null)
		{
			McpeMobArmorEquipment mcpePlayerArmorEquipment = McpeMobArmorEquipment.CreateObject();
			mcpePlayerArmorEquipment.runtimeEntityId = EntityId;
			mcpePlayerArmorEquipment.helmet = Inventory.Helmet;
			mcpePlayerArmorEquipment.chestplate = Inventory.Chest;
			mcpePlayerArmorEquipment.leggings = Inventory.Leggings;
			mcpePlayerArmorEquipment.boots = Inventory.Boots;
			if (receivers == null)
			{
				Level.RelayBroadcast(this, mcpePlayerArmorEquipment);
			}
			else
			{
				Level.RelayBroadcast(this, receivers, mcpePlayerArmorEquipment);
			}
		}

		public override void DespawnFromPlayers(Player[] players)
		{
			McpeRemoveEntity mcpeRemovePlayer = McpeRemoveEntity.CreateObject();
			mcpeRemovePlayer.entityIdSelf = EntityId;
			Level.RelayBroadcast(this, players, mcpeRemovePlayer);
		}


		// Events

		public virtual void HandleMcpeNetworkStackLatency(McpeNetworkStackLatency message)
		{
			var packet = McpeNetworkStackLatency.CreateObject();
			packet.timestamp = message.timestamp; // don't know what is it
			packet.unknownFlag = 1;
			SendPacket(packet);
		}

		public virtual void HandleMcpePlayerToggleCrafterSlotRequest(McpePlayerToggleCrafterSlotRequest message)
		{
		}

		public virtual void HandleMcpeSetPlayerInventoryOptions(McpeSetPlayerInventoryOptions message)
		{
			Log.Debug($"InvOpt: leftTab={(McpeSetPlayerInventoryOptions.InventoryLeftTab) message.leftTab}; " +
				$"rightTab={(McpeSetPlayerInventoryOptions.InventoryRightTab) message.rightTab}; " +
				$"filtering={message.filtering}; " +
				$"inventoryLayout={(McpeSetPlayerInventoryOptions.InventoryLayout) message.inventoryLayout}; " +
				$"craftingLayout={(McpeSetPlayerInventoryOptions.InventoryLayout) message.craftingLayout}");
		}

		public virtual void HandleMcpeServerPlayerPostMovePosition(McpeServerPlayerPostMovePosition message)
		{
		}

		public virtual void HandleMcpeBossEvent(McpeBossEvent message)
		{
			Log.Debug($"BossEvent: bossEntityId={message.bossEntityId}, eventType={message.eventType}, " +
				$"playerId={message.playerId}, title={message.title}, " +
				$"healthPercent={message.healthPercent}, " +
				$"overlay={message.overlay}, color={message.color}");
		}

		public void HandleMcpeServerboundLoadingScreen(McpeServerboundLoadingScreen message)
		{
			
		}

		public void HandleMcpeContainerRegistryCleanup(McpeContainerRegistryCleanup message)
		{

		}

		public void HandleMcpeEmoteList(McpeEmoteList message)
		{
			
		}
	}
}
