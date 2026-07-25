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
		public void SendRespawn()
		{
			McpeRespawn mcpeRespawn = McpeRespawn.CreateObject();
			mcpeRespawn.x = SpawnPosition.X;
			mcpeRespawn.y = SpawnPosition.Y;
			mcpeRespawn.z = SpawnPosition.Z;
			SendPacket(mcpeRespawn);
		}

		public void SendStartGame()
		{
			var levelSettings = new LevelSettings();
			levelSettings.SpawnSettings = new SpawnSettings()
			{
				Dimension = (int)(Level?.Dimension ?? 0),
				BiomeName = "",
				BiomeType = 0
			};
			levelSettings.Seed = 12345;
			levelSettings.Generator = 1;
			levelSettings.GameMode = (int) GameMode;
			levelSettings.X = (int) SpawnPosition.X;
			levelSettings.Y = (int) (SpawnPosition.Y + Height);
			levelSettings.Z = (int) SpawnPosition.Z;
			levelSettings.HasAchievementsDisabled = true;
			levelSettings.Time = (int) Level.WorldTime;
			levelSettings.EduOffer = PlayerInfo.Edition == 1 ? 1 : 0;
			levelSettings.RainLevel = 0;
			levelSettings.LightningLevel = 0;
			levelSettings.IsMultiplayer = true;
			levelSettings.BroadcastToLan = true;
			levelSettings.EnableCommands = EnableCommands;
			levelSettings.IsTexturepacksRequired = false;
			levelSettings.GameRules = Level.GetGameRules();
			levelSettings.BonusChest = false;
			levelSettings.MapEnabled = false;
			levelSettings.PermissionLevel = (byte) PermissionLevel;
			levelSettings.ServerChunkTickRange = 4;
			levelSettings.GameVersion = McpeProtocolInfo.GameVersion;
			levelSettings.HasEduFeaturesEnabled = false;
			levelSettings.IsNewNether = true;
			
			var startGame = McpeStartGame.CreateObject();
			startGame.levelSettings = levelSettings;
			startGame.entityIdSelf = EntityId;
			startGame.runtimeEntityId = EntityManager.EntityIdSelf;
			startGame.playerGamemode = (int) GameMode;
			startGame.spawn = SpawnPosition;
			startGame.rotation = new Vector2(KnownPosition.HeadYaw, KnownPosition.Pitch);
			
			startGame.levelId = "1m0AAMIFIgA=";
			startGame.worldName = Level.LevelName;
			startGame.premiumWorldTemplateId = "";
			startGame.isTrial = false;
			startGame.currentTick = Level.TickTime;
			startGame.enchantmentSeed = 123456;
			startGame.movementType = (int) McpeStartGame.ServerAuthMovementMode.ServerAuthoritativeV2;
			startGame.enableNewBlockBreakSystem = false;

			startGame.blockNetworkIdsAreHashes = BlockFactory.FactoryProfile.BlockRuntimeIdsAreHashes;
			startGame.blockPalette = startGame.blockNetworkIdsAreHashes
				? new ListBlockPalette()
				: BlockFactory.BlockPalette;

			startGame.enableNewInventorySystem = true;
			startGame.blockPaletteChecksum = 0;
			startGame.serverVersion = McpeProtocolInfo.GameVersion;
			startGame.propertyData = new Nbt
			{
				NbtFile = new NbtFile
				{
					Flavor = NbtFlavor.Bedrock,
					RootTag = new NbtCompound("")
				}
			};
			startGame.worldTemplateId = WorldTemplateId;

			SendPacket(startGame);
		}

		/// <summary>
		///     Sends the set spawn position packet.
		/// </summary>
		public void SendSetSpawnPosition()
		{
			McpeSetSpawnPosition mcpeSetSpawnPosition = McpeSetSpawnPosition.CreateObject();
			mcpeSetSpawnPosition.spawnType = 1;
			mcpeSetSpawnPosition.coordinates = (BlockCoordinates) SpawnPosition;
			SendPacket(mcpeSetSpawnPosition);
		}

		private object _sendChunkSync = new object();
		private PlayerLocation _lastClientAuthInputPosition;

		private void ForcedSendChunk(PlayerLocation position, bool cache = true)
		{
			lock (_sendChunkSync)
			{
				var chunkPosition = new ChunkCoordinates(position);

				McpeWrapper chunk = Level.GetChunk(chunkPosition)?.GetBatch();
				if (cache && !_chunksUsed.ContainsKey(chunkPosition))
				{
					_chunksUsed.Add(chunkPosition, chunk);
				}

				if (chunk != null)
				{
					SendPacket(chunk);
				}
			}
		}

		private void ForcedSendEmptyChunks()
		{
			Monitor.Enter(_sendChunkSync);
			try
			{
				var chunkPosition = new ChunkCoordinates(KnownPosition);

				_currentChunkPosition = chunkPosition;

				if (Level == null) return;

				for (int x = -1; x <= 1; x++)
				{
					for (int z = -1; z <= 1; z++)
					{
						var chunk = new McpeLevelChunk();
						chunk.chunkX = chunkPosition.X + x;
						chunk.chunkZ = chunkPosition.Z + z;
						chunk.chunkData = new byte[0];
						SendPacket(chunk);
					}
				}
			}
			finally
			{
				Monitor.Exit(_sendChunkSync);
			}
		}

		public void SendNetworkChunkPublisherUpdate()
		{
			SendNetworkChunkPublisherUpdate(KnownPosition.GetCoordinates3D());
		}

		public void SendNetworkChunkPublisherUpdate(BlockCoordinates coordinates)
		{
			var pk = McpeNetworkChunkPublisherUpdate.CreateObject();
			pk.coordinates = coordinates;
			pk.radius = (uint) (MaxViewDistance * 16);
			SendPacket(pk);
		}

		public void ForcedSendChunks(Action postAction = null)
		{
			Monitor.Enter(_sendChunkSync);
			try
			{
				var chunkPosition = new ChunkCoordinates(KnownPosition);

				_currentChunkPosition = chunkPosition;

				if (Level == null) return;

				SendNetworkChunkPublisherUpdate();
				int packetCount = 0;
				foreach (McpeWrapper chunk in Level.GenerateChunks(_currentChunkPosition, _chunksUsed, ChunkRadius))
				{
					if (chunk != null) SendPacket(chunk);

					if (++packetCount % 16 == 0) Thread.Sleep(12);
				}
			}
			finally
			{
				Monitor.Exit(_sendChunkSync);
			}

			if (postAction != null)
			{
				postAction();
			}
		}

		private void SendChunksForKnownPosition()
		{
			if (!Monitor.TryEnter(_sendChunkSync)) return;

			try
			{
				if (ChunkRadius <= 0) return;


				var chunkPosition = new ChunkCoordinates(KnownPosition);
				if (IsSpawned && _currentChunkPosition == chunkPosition) return;

				if (IsSpawned && _currentChunkPosition.DistanceTo(chunkPosition) < MoveRenderDistance)
				{
					return;
				}

				_currentChunkPosition = chunkPosition;

				int packetCount = 0;

				if (Level == null) return;

				SendNetworkChunkPublisherUpdate();

				foreach (McpeWrapper chunk in Level.GenerateChunks(_currentChunkPosition, _chunksUsed, ChunkRadius, () => KnownPosition))
				{
					if (chunk != null) SendPacket(chunk);

					if (++packetCount % 16 == 0) Thread.Sleep(12);

					if (!IsSpawned && packetCount == 56)
					{
						InitializePlayer();
					}
				}
				// Player was never initialized.
				if (!IsSpawned && packetCount > 0)
				{
					InitializePlayer();
				}

				Log.Debug($"Sent {packetCount} chunks for {chunkPosition} with view distance {MaxViewDistance}");
			}
			catch (Exception e)
			{
				Log.Error($"Failed sending chunks for {KnownPosition}", e);
			}
			finally
			{
				Monitor.Exit(_sendChunkSync);
			}
		}

		public virtual void SendUpdateAttributes()
		{
			var attributes = new PlayerAttributes();
			attributes["minecraft:attack_damage"] = new PlayerAttribute
			{
				Name = "minecraft:attack_damage",
				MinValue = 1,
				MaxValue = 1,
				Value = 1,
				MinDefault = 1,
				MaxDefault = 1,
				Default = 1,
				Modifiers = new AttributeModifiers()
			};
			attributes["minecraft:absorption"] = new PlayerAttribute
			{
				Name = "minecraft:absorption",
				MinValue = 0,
				MaxValue = float.MaxValue,
				Value = HealthManager.Absorption,
				MinDefault = 0,
				MaxDefault = float.MaxValue,
				Default = 0,
				Modifiers = new AttributeModifiers()
			};
			attributes["minecraft:health"] = new PlayerAttribute
			{
				Name = "minecraft:health",
				MinValue = 0,
				MaxValue = HealthManager.MaxHearts,
				Value = HealthManager.Hearts,
				MinDefault = 0,
				MaxDefault = 20,
				Default = HealthManager.MaxHearts,
				Modifiers = new AttributeModifiers()
			};
			attributes["minecraft:movement"] = new PlayerAttribute
			{
				Name = "minecraft:movement",
				MinValue = 0,
				MaxValue = 0.5f,
				Value = MovementSpeed,
				MinDefault = 0,
				MaxDefault = 0.5f,
				Default = MovementSpeed,
				Modifiers = new AttributeModifiers()
			};
			attributes["minecraft:knockback_resistance"] = new PlayerAttribute
			{
				Name = "minecraft:knockback_resistance",
				MinValue = 0,
				MaxValue = 1,
				Value = 0,
				MinDefault = 0,
				MaxDefault = 1,
				Default = 0,
				Modifiers = new AttributeModifiers()
			};
			attributes["minecraft:luck"] = new PlayerAttribute
			{
				Name = "minecraft:luck",
				MinValue = -1025,
				MaxValue = 1024,
				Value = 0,
				MinDefault = -1025,
				MaxDefault = 1024,
				Default = 0,
				Modifiers = new AttributeModifiers()
			};
			attributes["minecraft:follow_range"] = new PlayerAttribute
			{
				Name = "minecraft:follow_range",
				MinValue = 0,
				MaxValue = 2048,
				Value = 16,
				MinDefault = 0,
				MaxDefault = 2048,
				Default = 16,
				Modifiers = new AttributeModifiers()
			};
			// Workaround, bad design.
			attributes = HungerManager.AddHungerAttributes(attributes);
			attributes = ExperienceManager.AddExperienceAttributes(attributes);

			McpeUpdateAttributes attributesPackate = McpeUpdateAttributes.CreateObject();
			attributesPackate.runtimeEntityId = EntityManager.EntityIdSelf;
			attributesPackate.attributes = attributes;
			SendPacket(attributesPackate);
		}

		public virtual void SendSetTime()
		{
			SendSetTime((int) Level.WorldTime);
		}

		public virtual void SendSetTime(int time)
		{
			McpeSetTime message = McpeSetTime.CreateObject();
			message.time = time;
			SendPacket(message);
		}

		public void SendSound(BlockCoordinates position, LevelSoundEventType sound, int blockId = 0)
		{
			var packet = McpeLevelSoundEvent.CreateObject();
			packet.position = position;
			packet.soundId = (uint) sound;
			packet.blockId = blockId;
			SendPacket(packet);
		}

		public virtual void SendSetDownfall(int downfall)
		{
			McpeLevelEvent levelEvent = McpeLevelEvent.CreateObject();
			levelEvent.eventId = 3001;
			levelEvent.data = downfall;
			SendPacket(levelEvent);
		}

		public virtual void SendMovePlayer(bool teleport = false)
		{
			var packet = McpeMovePlayer.CreateObject();
			packet.runtimeEntityId = EntityManager.EntityIdSelf;
			packet.x = KnownPosition.X;
			packet.y = KnownPosition.Y + 1.62f;
			packet.z = KnownPosition.Z;
			packet.yaw = KnownPosition.Yaw;
			packet.headYaw = KnownPosition.HeadYaw;
			packet.pitch = KnownPosition.Pitch;
			packet.mode = (byte) (teleport ? 1 : 0);

			SendPacket(packet);
		}
	}
}
