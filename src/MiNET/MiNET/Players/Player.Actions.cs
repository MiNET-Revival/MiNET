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
		public virtual void HandleMcpePlayerInput(McpePlayerInput message)
		{
			Log.Debug($"Player input: x={message.motionX}, z={message.motionZ}, jumping={message.jumping}, sneaking={message.sneaking}");
		}

		public virtual void HandleMcpeRiderJump(McpeRiderJump message)
		{
			if (IsRiding && Vehicle > 0)
			{
				if (Level.TryGetEntity(Vehicle, out Mob mob))
				{
					mob.IsRearing = true;
					mob.BroadcastSetEntityData();
				}
			}
		}

		public virtual void HandleMcpeSetEntityData(McpeSetEntityData message)
		{
			// Only used by EDU NPC so far.
			if (Level.TryGetEntity(message.runtimeEntityId, out Entity entity))
			{
				entity.SetEntityData(message.metadata);
			}
		}

		public void HandleMcpeNpcRequest(McpeNpcRequest message)
		{
			// Only used by EDU NPC.

			if (Level.TryGetEntity(message.runtimeEntityId, out Entity entity))
			{
				// 0 is edit
				// 0 is exec command
				// 2 is exec link

				if (message.unknown0 == 0)
				{
					MetadataDictionary metadata = new MetadataDictionary();
					metadata[42] = new MetadataString(message.unknown1);
					entity.SetEntityData(metadata);
				}
			}
		}

		private object _mapInfoSync = new object();

		public virtual void HandleMcpeMapInfoRequest(McpeMapInfoRequest message)
		{
			lock (_mapInfoSync)
			{
				//if(_mapSender == null)
				//{
				//	_mapSender = new Timer(Callback);
				//}

				long mapId = message.mapId;

				Log.Trace($"Requested map with ID: {mapId} 0x{mapId:X2}");

				if (mapId == 0)
				{
					// 2016-02-26 02:53:01,895 [17] INFO  MiNET.Player - Requested map with ID: 0xFFFFFFFFFFFFFFFF
					// Should not happen.
				}
				else
				{
					if (!Level.TryGetEntity(mapId, out MapEntity mapEntity))
					{
						// Create new map entity
						// send map for that entity
						mapEntity = new MapEntity(Level, mapId);
						mapEntity.SpawnEntity();
					}
					else
					{
						mapEntity?.AddToMapListeners(this, mapId);
					}
				}
			}
		}

		public virtual void SendMapInfo(MapInfo mapInfo)
		{
			McpeClientboundMapItemData packet = McpeClientboundMapItemData.CreateObject();
			packet.mapinfo = mapInfo;
			SendPacket(packet);
		}

		public int ChunkRadius { get; private set; } = -1;

		public void SetChunkRadius(int radius)
		{
			ChunkRadius = Math.Max(5, Math.Min(radius, MaxViewDistance));
		}
		
		public virtual void HandleMcpeRequestChunkRadius(McpeRequestChunkRadius message)
		{
			Log.Debug($"Requested chunk radius of: {message.chunkRadius}");

			SetChunkRadius(message.chunkRadius);
			SendChunkRadiusUpdate();

			//if (_completedStartSequence)
			{
				MiNetServer.FastThreadPool.QueueUserWorkItem(SendChunksForKnownPosition);
			}
		}

		public virtual void HandleMcpeSetEntityMotion(McpeSetEntityMotion message)
		{
			//Level.RelayBroadcast((McpeSetEntityMotion) message.Clone());
		}

		public void HandleMcpeMoveEntity(McpeMoveEntity message)
		{
			//Level.RelayBroadcast((McpeMoveEntity) message.Clone());
			if (Vehicle == message.runtimeEntityId && Level.TryGetEntity(message.runtimeEntityId, out Entity entity))
			{
				entity.KnownPosition = message.position;
				entity.IsOnGround = (message.flags & 1) == 1;
				if (entity.IsOnGround) Log.Debug("Horse is on ground");
			}
		}

		/// <summary>
		///     Handles an animate packet.
		/// </summary>
		/// <param name="message">The message.</param>
		public virtual void HandleMcpeAnimate(McpeAnimate message)
		{
			if (Level == null) return;

			var itemInHand = Inventory.GetItemInHand();
			if (itemInHand != null)
			{
				bool isHandled = itemInHand.Animate(Level, this);
				if (isHandled) return; // Handled, return
			}

			McpeAnimate msg = McpeAnimate.CreateObject();
			msg.runtimeEntityId = EntityId;
			msg.actionId = message.actionId;
			msg.unknownFloat = message.unknownFloat;

			Level.RelayBroadcast(this, msg);
		}

		Action _dimensionFunc;

		/// <summary>
		///     Handles the player action.
		/// </summary>
		/// <param name="message">The message.</param>
		public virtual void HandleMcpePlayerAction(McpePlayerAction message)
		{
			switch ((PlayerAction) message.actionId)
			{
				case PlayerAction.StartBreak:
				{
					if (message.face == (int) BlockFace.Up)
					{
						Block block = Level.GetBlock(message.coordinates.BlockUp());
						if (block is Fire)
						{
							Level.BreakBlock(this, message.coordinates.BlockUp());
							break;
						}
					}


					if (GameMode == GameMode.Survival)
					{
						Block target = Level.GetBlock(message.coordinates);
						var drops = target.GetDrops(Level, Inventory.GetItemInHand());
						float tooltypeFactor = drops == null || drops.Length == 0 ? 5f : 1.5f; // 1.5 if proper tool
						double breakTime = Math.Ceiling(target.Hardness * tooltypeFactor * 20);

						if (breakTime > 0)
						{
							McpeLevelEvent breakEvent = McpeLevelEvent.CreateObject();
							breakEvent.eventId = 3600;
							breakEvent.position = message.coordinates;
							breakEvent.data = (int) (65535 / breakTime);
							Log.Debug("Break speed: " + breakEvent.data);
							Level.RelayBroadcast(breakEvent);
						}
					}

					break;
				}
				case PlayerAction.Breaking:
				{
					Block target = Level.GetBlock(message.coordinates);
					int data = ((int) target.RuntimeId) | ((byte) (message.face << 24));

					McpeLevelEvent breakEvent = McpeLevelEvent.CreateObject();
					breakEvent.eventId = 2014;
					breakEvent.position = message.coordinates;
					breakEvent.data = data;
					Level.RelayBroadcast(breakEvent);
					break;
				}
				case PlayerAction.AbortBreak:
				case PlayerAction.StopBreak:
				{
					McpeLevelEvent breakEvent = McpeLevelEvent.CreateObject();
					breakEvent.eventId = 3601;
					breakEvent.position = message.coordinates;
					Level.RelayBroadcast(breakEvent);
					break;
				}
				case PlayerAction.StartSleeping:
				{
					break;
				}
				case PlayerAction.StopSleeping:
				{
					IsSleeping = false;
					Bed bed = Level.GetBlock(SpawnPosition) as Bed;
					if (bed != null)
					{
						bed.SetOccupied(Level, false);
					}
					else
					{
						Log.Warn($"Did not find a bed at {SpawnPosition}");
					}

					break;
				}
				//case PlayerAction.Respawn:
				//{
				//	MiNetServer.FastThreadPool.QueueUserWorkItem(HandleMcpeRespawn);
				//	break;
				//}
				case PlayerAction.Jump:
				{
					HungerManager.IncreaseExhaustion(IsSprinting ? 0.8f : 0.2f);
					break;
				}
				case PlayerAction.StartSprint:
				{
					SetSprinting(true);
					break;
				}
				case PlayerAction.StopSprint:
				{
					SetSprinting(false);
					break;
				}
				case PlayerAction.StartSneak:
				{
					SetSprinting(false);
					IsSneaking = true;
					break;
				}
				case PlayerAction.StopSneak:
				{
					SetSprinting(false);
					IsSneaking = false;
					break;
				}
				case PlayerAction.CreativeDestroy:
				{
					break;
				}
				case PlayerAction.DimensionChangeAck:
				{
					if (_dimensionFunc != null)
					{
						_dimensionFunc();
						_dimensionFunc = null;
					}

					break;
				}
				case PlayerAction.WorldImmutable:
				{
					break;
				}
				case PlayerAction.StartGlide:
				{
					IsGliding = true;
					Height = 0.6;

					var particle = new WhiteSmokeParticle(Level);
					particle.Position = KnownPosition.ToVector3();
					particle.Spawn();

					break;
				}
				case PlayerAction.StopGlide:
				{
					IsGliding = false;
					Height = 1.8;
					break;
				}
				case PlayerAction.SetEnchantmentSeed:
				{
					Log.Debug($"Got PlayerAction.SetEnchantmentSeed with data={message.face} at {message.coordinates}");
					break;
				}
				case PlayerAction.InteractBlock:
				{
					break;
				}
				case PlayerAction.StartItemUse:
				{
					Level.UseItem(this, Inventory.GetItemInHand(), message.coordinates, (BlockFace) message.face);
					break;
				}
				case PlayerAction.StartFlying:
				{
					if (!AllowFly && !GameMode.AllowsFlying())
					{
						SendAbilities();
						return;
					}

					IsFlying = true;
					break;
				}
				case PlayerAction.StopFlying:
				{
					IsFlying = false;
					break;
				}
				case PlayerAction.GetUpdatedBlock:
				case PlayerAction.DropItem:
				case PlayerAction.Respawn:
				case PlayerAction.ChangeSkin:
				case PlayerAction.StartSwimming:
				case PlayerAction.StopSwimming:
				case PlayerAction.StartSpinAttack:
				case PlayerAction.StopSpinAttack:
				case PlayerAction.PredictDestroyBlock:
				case PlayerAction.ContinueDestroyBlock:
				case PlayerAction.StopItemUse:
				case PlayerAction.HandledTeleport:
				case PlayerAction.MissedSwing:
				case PlayerAction.StartCrawling:
				case PlayerAction.StopCrawling:
				case PlayerAction.AckEntityData:
				case PlayerAction.StartUsingItem:
				{
					break;
				}
				default:
				{
					Log.Warn($"Unhandled action ID={message.actionId}");
					throw new ArgumentOutOfRangeException(nameof(message.actionId));
				}
			}

			IsUsingItem = false;

			BroadcastSetEntityData();
		}

		private float _baseSpeed;
		private object _sprintLock = new object();

		public void SetSprinting(bool isSprinting)
		{
			lock (_sprintLock)
			{
				if (isSprinting == IsSprinting) return;

				if (isSprinting)
				{
					IsSprinting = true;
					_baseSpeed = MovementSpeed;
					MovementSpeed += MovementSpeed * 0.3f;
				}
				else
				{
					IsSprinting = false;
					MovementSpeed = _baseSpeed;
				}

				SendUpdateAttributes();
			}
		}

		public virtual void HandleMcpeBlockEntityData(McpeBlockEntityData message)
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("x:  {0}", message.coordinates.X);
				Log.DebugFormat("y:  {0}", message.coordinates.Y);
				Log.DebugFormat("z:  {0}", message.coordinates.Z);
				Log.DebugFormat("NBT {0}", message.namedtag.NbtFile);
			}

			Level.UpdateBlockEntity(message.coordinates, message.namedtag.NbtFile.RootTag);
		}
	}
}
