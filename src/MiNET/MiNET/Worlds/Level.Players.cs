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
		private object _playerWriteLock = new object();

		public virtual void AddPlayer(Player newPlayer, bool spawn)
		{
			if (newPlayer.Username == null) throw new ArgumentNullException(nameof(newPlayer.Username));

			EntityManager.AddEntity(newPlayer);

			lock (_playerWriteLock)
			{
				if (!newPlayer.IsConnected)
				{
					Log.Error("Tried to add player that was already disconnected.");
					return;
				}

				if (Players.TryAdd(newPlayer.EntityId, newPlayer))
				{
					foreach (Entity entity in Entities.Values.ToArray())
					{
						entity.SpawnToPlayers(new[] {newPlayer});
					}

					SpawnToAll(newPlayer);
				}

				newPlayer.IsSpawned = spawn;
			}

			OnPlayerAdded(new LevelEventArgs(newPlayer, this));
		}

		public event EventHandler<LevelEventArgs> PlayerAdded;

		protected virtual void OnPlayerAdded(LevelEventArgs e)
		{
			PlayerAdded?.Invoke(this, e);
		}

		public event EventHandler<LevelEventArgs> PlayerRemoved;

		protected virtual void OnPlayerRemoved(LevelEventArgs e)
		{
			PlayerRemoved?.Invoke(this, e);
		}

		public void SpawnToAll(Player newPlayer)
		{
			lock (_playerWriteLock)
			{
				// The player list keeps us from moving this completely to player.
				// It's simply to slow and bad.

				Player[] players = GetAllPlayers();
				var spawnedPlayers = players.ToList();
				spawnedPlayers.Add(newPlayer);

				Player[] sendList = spawnedPlayers.ToArray();

				var playerListMessage = McpePlayerList.CreateObject();
				playerListMessage.records = new PlayerAddRecords(spawnedPlayers);
				newPlayer.SendPacket(CreateMcpeBatch(playerListMessage.Encode()));
				playerListMessage.PutPool();

				var playerList = McpePlayerList.CreateObject();
				playerList.records = new PlayerAddRecords(newPlayer);

				RelayBroadcast(newPlayer, sendList, CreateMcpeBatch(playerList.Encode()));
				playerList.PutPool();

				newPlayer.SpawnToPlayers(players);

				foreach (Player spawnedPlayer in players)
				{
					spawnedPlayer.SpawnToPlayers(new[] {newPlayer});
				}
			}
		}

		public virtual void RemovePlayer(Player player, bool despawn = true)
		{
			if (Players == null) return; // Closing down the level sets players to null;
			if (Entities == null) return; // Closing down the level sets players to null;

			lock (_playerWriteLock)
			{
				Player removed;
				if (Players.TryRemove(player.EntityId, out removed))
				{
					player.IsSpawned = false;
					if (despawn) DespawnFromAll(player);

					foreach (Entity entity in Entities.Values.ToArray())
					{
						entity.DespawnFromPlayers(new[] {removed});
					}
				}
			}

			OnPlayerRemoved(new LevelEventArgs(player, this));
		}

		public void DespawnFromAll(Player player)
		{
			lock (_playerWriteLock)
			{
				var spawnedPlayers = GetAllPlayers();

				foreach (Player spawnedPlayer in spawnedPlayers)
				{
					spawnedPlayer.DespawnFromPlayers(new[] {player});
				}

				player.DespawnFromPlayers(spawnedPlayers);

				McpePlayerList playerListMessage = McpePlayerList.CreateObject();
				playerListMessage.records = new PlayerRemoveRecords(spawnedPlayers);
				player.SendPacket(CreateMcpeBatch(playerListMessage.Encode()));
				playerListMessage.records = null;
				playerListMessage.PutPool();

				var playerList = McpePlayerList.CreateObject();
				playerList.records = new PlayerRemoveRecords(player);

				RelayBroadcast(player, CreateMcpeBatch(playerList.Encode()));
				playerList.records = null;
				playerList.PutPool();
			}
		}

		public void AddEntity(Entity entity)
		{
			lock (Entities)
			{
				EntityManager.AddEntity(entity);

				if (Entities.TryAdd(entity.EntityId, entity))
				{
					entity.SpawnToPlayers(GetAllPlayers());
					OnEntityAdded(new LevelEntityEventArgs(this, entity));
				}
				else
				{
					throw new Exception("Entity existed in the players list when it should not");
				}
			}
		}

		public void RemoveEntity(Entity entity)
		{
			lock (Entities)
			{
				if (!Entities.TryRemove(entity.EntityId, out entity)) return; // It's ok. Holograms destroy this play..
				entity.DespawnFromPlayers(GetAllPlayers());
				OnEntityRemoved(new LevelEntityEventArgs(this, entity));
			}
		}

		public event EventHandler<LevelEntityEventArgs> EntityAdded;

		protected virtual void OnEntityAdded(LevelEntityEventArgs e)
		{
			EntityAdded?.Invoke(this, e);
		}

		public event EventHandler<LevelEntityEventArgs> EntityRemoved;

		protected virtual void OnEntityRemoved(LevelEntityEventArgs e)
		{
			EntityRemoved?.Invoke(this, e);
		}


		public void RemoveDuplicatePlayers(string username, long clientId)
		{
			//var existingPlayers = Players.Where(player => player.Value.ClientId == clientId && player.Value.Username.Equals(username, StringComparison.InvariantCultureIgnoreCase));

			//foreach (var existingPlayer in existingPlayers)
			//{
			//	Log.InfoFormat("Removing staled players on login {0}", username);
			//	existingPlayer.Value.Disconnect("Duplicate player. Crashed.", false);
			//}
		}
	}
}
