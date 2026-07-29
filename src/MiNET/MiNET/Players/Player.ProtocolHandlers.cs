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
		public virtual void HandleMcpeLevelSoundEvent(McpeLevelSoundEvent message)
		{
			//TODO: This will require that sounds are sent by the server.

			//var sound = McpeLevelSoundEvent.CreateObject();
			//sound.soundId = message.soundId;
			//sound.position = message.position;
			//sound.blockId = message.blockId;
			//sound.entityType = message.entityType;
			//sound.isBabyMob = message.isBabyMob;
			//sound.isGlobal = message.isGlobal;
			//Level.RelayBroadcast(sound);
		}

		public void HandleMcpeClientCacheStatus(McpeClientCacheStatus message)
		{
			Log.Warn($"Cache status: {(message.enabled ? "Enabled" : "Disabled")}");
		}

		public void HandleMcpeNetworkSettings(McpeNetworkSettings message)
		{
		}

		/// <inheritdoc />
		public void HandleMcpePlayerAuthInput(McpePlayerAuthInput message)
		{
			_lastClientAuthInputPosition = new PlayerLocation
			{
				X = message.Position.X,
				Y = message.Position.Y - 1.62f,
				Z = message.Position.Z,
				Pitch = message.Pitch,
				Yaw = message.Yaw,
				HeadYaw = message.HeadYaw
			};

			if (!IsSpawned && Level?.Players.ContainsKey(EntityId) == true)
			{
				InitializePlayer();
			}

			HandlePlayerAuthInputMovement(message);

			if (message.ItemInteraction != null)
			{
				HandleItemUseTransaction((ItemUseTransaction) message.ItemInteraction);
			}

			if (message.ItemStackRequests != null)
			{
				var itemStackRequest = McpeItemStackRequest.CreateObject();
				itemStackRequest.requests = message.ItemStackRequests;
				HandleMcpeItemStackRequest(itemStackRequest);
			}

			foreach (var blockAction in message.BlockActions)
			{
				if (!blockAction.HasBlockPosition && blockAction.Action != PlayerAction.StopBreak) continue;

				var playerAction = McpePlayerAction.CreateObject();
				playerAction.runtimeEntityId = EntityId;
				playerAction.actionId = (int) blockAction.Action;
				playerAction.coordinates = blockAction.Coordinates;
				playerAction.face = blockAction.Face;

				HandleMcpePlayerAction(playerAction);
			}
		}

		public void HandleMcpeItemStackRequest(McpeItemStackRequest message)
		{
			var response = McpeItemStackResponse.CreateObject();
			response.responses = new ItemStackResponses();
			foreach (ItemStackActionList request in message.requests)
			{
				var stackResponse = new ItemStackResponse()
				{
					Result = StackResponseStatus.Ok,
					RequestId = request.RequestId,
					ResponseContainerInfos = new List<StackResponseContainerInfo>()
				};

				response.responses.Add(stackResponse);

				try
				{
					stackResponse.Result = ItemStackInventoryManager.HandleItemStackActions(request.RequestId, request, out var stackResponses);
					stackResponse.ResponseContainerInfos.AddRange(stackResponses);
				}
				catch (Exception e)
				{
					Log.Warn($"Failed to process inventory actions", e);
					stackResponse.Result = StackResponseStatus.Error;
					stackResponse.ResponseContainerInfos.Clear();
				}
			}

			SendPacket(response);
		}

		public void HandleMcpeUpdatePlayerGameType(McpeUpdatePlayerGameType message)
		{
		}

		public void HandleMcpePacketViolationWarning(McpePacketViolationWarning message)
		{
			Log.Error($"Client reported a level {message.severity} packet violation of type {message.violationType} for packet 0x{message.packetId:X2}: {message.reason}");
		}

		/// <inheritdoc />
		public void HandleMcpeUpdateSubChunkBlocksPacket(McpeUpdateSubChunkBlocksPacket message)
		{
			
		}

		/// <inheritdoc />
		public void HandleMcpeSubChunkRequestPacket(McpeSubChunkRequestPacket message)
		{
			/*McpeSubChunkPacket response = McpeSubChunkPacket.CreateObject();
			if (message.dimension != (int) Level.Dimension)
			{
				response.requestResult = (int) SubChunkRequestResult.WrongDimension;
			}
			else
			{
				var chunk = Level.GetChunk(message.subchunkCoordinates);

				if (chunk == null)
				{
					response.requestResult = (int) SubChunkRequestResult.NoSuchChunk;
				}
				else
				{
					try
					{
						var subChunk = chunk.GetSubChunk(message.subchunkCoordinates.Y);

						using (MemoryStream ms = new MemoryStream())
						{
							subChunk.Write(ms);
							response.data = ms.ToArray();
						}
						//subChunk.Write();

						response.dimension = message.dimension;
						response.heightmapData = new HeightMapData(chunk.height);
						
						response.requestResult = (int) SubChunkRequestResult.Success;
					}
					catch (IndexOutOfRangeException)
					{
						response.requestResult = (int) SubChunkRequestResult.YIndexOutOfBounds;
					}
				}
			}
			
			SendPacket(response);*/
		}

		public virtual void HandleMcpeRequestAbility(McpeRequestAbility message)
		{
			Log.Debug($"Request abilities ability=[{message.ability}], value=[{message.Value}]");
		}

		public virtual void HandleMcpeMobArmorEquipment(McpeMobArmorEquipment message)
		{
		}

		public virtual void HandleMcpeMobEquipment(McpeMobEquipment message)
		{
			if (HealthManager.IsDead) return;

			if (message.windowsId == 0)
			{
				byte selectedHotbarSlot = message.selectedSlot;
				if (selectedHotbarSlot > 8)
				{
					Log.Error($"Player {Username} called set equipment with held hotbar slot {message.selectedSlot} with item {message.item}");
					return;
				}

				if (Log.IsDebugEnabled) Log.Debug($"Player {Username} called set equipment with held hotbar slot {message.selectedSlot} with item {message.item}");

				Inventory.SetHeldItemSlot(selectedHotbarSlot, false);
				if (Log.IsDebugEnabled)
					Log.Debug($"Player {Username} now holding {Inventory.GetItemInHand()}");
			}
			else if (message.windowsId == (byte) WindowId.Offhand)
			{
				if (message.slot != 1)
				{
					Log.Error($"Player {Username} called set equipment with offhand slot {message.slot} with item {message.item}");
					return;
				}

				if (Log.IsDebugEnabled) Log.Debug($"Player {Username} called set equipment with offhand slot {message.slot} with item {message.item}");

				var offHandItem = Inventory.OffHand;
			}
		}

		public virtual void HandleMcpeServerboundPackSettingChange(McpeServerboundPackSettingChange message)
		{
			// Pack settings do not currently have a gameplay consumer in MiNET.
		}

		public virtual void HandleMcpeServerboundDataStore(McpeServerboundDataStore message)
		{
			// Data store updates do not currently have a gameplay consumer in MiNET.
		}
	}
}
