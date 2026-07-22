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
		private DateTime _lastSendTime = DateTime.UtcNow;
		private DateTime _lastBroadcast = DateTime.UtcNow;

		protected virtual void BroadCastMovement(Player[] players, Entity[] entities)
		{
			DateTime now = DateTime.UtcNow;

			if (players.Length == 0) return;

			if (players.Length <= 1 && entities.Length == 0) return;

			//if (now - _lastBroadcast < TimeSpan.FromMilliseconds(50)) return;

			DateTime lastSendTime = _lastSendTime;
			_lastSendTime = DateTime.UtcNow;

			//using (MemoryStream stream = new MemoryStream())
			{
				int playerMoveCount = 0;
				int entiyMoveCount = 0;

				List<Packet> movePackets = new List<Packet>();
				
				if (players.Length == 1 && entiyMoveCount == 0) return;

				foreach (var player in players)
				{
					if (now - player.LastUpdatedTime <= now - lastSendTime)
					{
						var knownPosition = (PlayerLocation) player.KnownPosition.Clone();

						var move = McpeMovePlayer.CreateObject();
						move.runtimeEntityId = player.EntityId;
						move.x = knownPosition.X;
						move.y = knownPosition.Y + 1.62f;
						move.z = knownPosition.Z;
						move.pitch = knownPosition.Pitch;
						move.yaw = knownPosition.Yaw;
						move.headYaw = knownPosition.HeadYaw;
						move.mode = (byte) (player.Vehicle == 0 ? 0 : 3);
						move.onGround = !player.IsGliding && player.IsOnGround;
						move.otherRuntimeEntityId = player.Vehicle;
						movePackets.Add(move);
						playerMoveCount++;
					}
				}

				//foreach (var entity in entities)
				//{
				//	if (entity.LastUpdatedTime >= lastSendTime)
				//	{
				//		{
				//			McpeMoveEntity moveEntity = McpeMoveEntity.CreateObject();
				//			moveEntity.entityId = entity.EntityId;
				//			moveEntity.position = (PlayerLocation)entity.KnownPosition.Clone();
				//			moveEntity.position.Y += entity.PositionOffset;
				//			byte[] bytes = moveEntity.Encode();
				//			BatchUtils.WriteLength(stream, bytes.Length);
				//			stream.Write(bytes, 0, bytes.Length);
				//			moveEntity.PutPool();
				//		}
				//		{
				//			McpeSetEntityMotion entityMotion = McpeSetEntityMotion.CreateObject();
				//			entityMotion.entityId = entity.EntityId;
				//			entityMotion.velocity = entity.Velocity;
				//			byte[] bytes = entityMotion.Encode();
				//			BatchUtils.WriteLength(stream, bytes.Length);
				//			stream.Write(bytes, 0, bytes.Length);
				//			entityMotion.PutPool();
				//		}
				//		entiyMoveCount++;
				//	}
				//}

				if (playerMoveCount == 0 && entiyMoveCount == 0) return;

				if (players.Length == 1 && entiyMoveCount == 0) return;

				if (movePackets.Count == 0) return;

				////McpeWrapper batch = BatchUtils.CreateBatchPacket(new Memory<byte>(stream.GetBuffer(), 0, (int) stream.Length), CompressionLevel.Optimal, false);
				var batch = McpeWrapper.CreateObject(players.Length);
				batch.ReliabilityHeader.Reliability = Reliability.ReliableOrdered;
				batch.payload = CompressionManager.ZLibCompressionManager.CompressPacketsForWrapper(movePackets);
				batch.Encode();
				foreach (Player player in players) MiNetServer.FastThreadPool.QueueUserWorkItem(() => player.SendPacket(batch));
				_lastBroadcast = DateTime.UtcNow;
			}
		}

		public void RelayBroadcast<T>(T message) where T : Packet<T>, new()
		{
			RelayBroadcast(null, GetAllPlayers(), message);
		}

		public void RelayBroadcast<T>(Player source, T message) where T : Packet<T>, new()
		{
			RelayBroadcast(source, GetAllPlayers(), message);
		}

		public void RelayBroadcast<T>(Player[] sendList, T message) where T : Packet<T>, new()
		{
			RelayBroadcast(null, sendList ?? GetAllPlayers(), message);
		}

		public void RelayBroadcast<T>(Player source, Player[] sendList, T message) where T : Packet<T>, new()
		{
			if (message == null) return;

			if (!message.IsPooled)
			{
				try
				{
					throw new ArgumentException($"Trying to broadcast a message of type {message.GetType().Name} that isn't pooled. Please use CreateObject and not the constructor.");
				}
				catch (Exception e)
				{
					Log.Fatal("Broadcast", e);
					throw;
				}
			}

			if (sendList == null || sendList.Length == 0)
			{
				message.PutPool();
				return;
			}

			if (message.ReferenceCounter == 1 && sendList.Length > 1)
			{
				message.AddReferences(sendList.Length - 1);
			}

			if (sendList.Length == 1)
			{
				Player player = sendList.First();

				if (source != null && player == source)
				{
					message.PutPool();
					return;
				}

				player.SendPacket(message);
			}
			else
			{
				Parallel.ForEach(sendList, player =>
				{
					if (source != null && player == source)
					{
						message.PutPool();
						return;
					}

					player.SendPacket(message);
				});
			}
		}
	}
}