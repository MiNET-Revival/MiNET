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
		private object _tickSync = new object();
		private Stopwatch _tickTimer = new Stopwatch();
		public long LastTickProcessingTime = 0;
		public long AvarageTickProcessingTime = 50;
		public int PlayerCount { get; private set; }

		public Profiler _profiler = new Profiler();

		private void WorldTick(object sender)
		{
			//if (_tickTimer.ElapsedMilliseconds < 40 && LastTickProcessingTime < 50)
			//{
			//	if (Log.IsDebugEnabled) Log.Warn($"World tick came too fast: {_tickTimer.ElapsedMilliseconds} ms");
			//	return;
			//}

			if (Log.IsDebugEnabled && _tickTimer.ElapsedMilliseconds >= 65) Log.Warn($"Time between world tick too long: {_tickTimer.ElapsedMilliseconds} ms. Last processing time={LastTickProcessingTime}, Avarage={AvarageTickProcessingTime}");

			Measurement worldTickMeasurement = _profiler.Begin("World tick");

			_tickTimer.Restart();

			try
			{
				TickTime++;

				Player[] players = GetSpawnedPlayers();

				if (DoDaylightcycle)
				{
					WorldTime++;
				}

				CurrentWorldCycleTime = WorldTime % _worldDayCycleTime;

				if (DoDaylightcycle && TickTime % 100 == 0)
				{
					McpeSetTime message = McpeSetTime.CreateObject();
					message.time = (int) WorldTime;
					RelayBroadcast(message);
				}

				SkylightSubtracted = CalculateSkylightSubtracted(WorldTime);

				// Save dirty chunks
				if (TickTime % (SaveInterval * 20) == 0)
				{
					WorldProvider.SaveChunks();
				}

				// Unload chunks not needed
				if (UnloadInterval > 0 && TickTime % (UnloadInterval * 20) == 0)
				{
					var cacheProvider = WorldProvider as ICachingWorldProvider;
					int removed = cacheProvider?.UnloadChunks(players, (ChunkCoordinates) (BlockCoordinates) SpawnPoint, ViewDistance) ?? 0;
					if (removed > 0) Log.Warn($"Unloaded {removed} chunks, {cacheProvider?.GetCachedChunks().Length} chunks remain cached");
				}

				var blockAndChunkTickMeasurement = worldTickMeasurement?.Begin("Block and chunk tick");

				Entity[] entities = Entities.Values.OrderBy(e => e.EntityId).ToArray();
				if (EnableChunkTicking || EnableBlockTicking)
				{
					if (EnableChunkTicking) EntitySpawnManager.DespawnMobs(TickTime);

					List<EntitySpawnManager.SpawnState> chunksWithinRadiusOfPlayer = new List<EntitySpawnManager.SpawnState>();
					foreach (var player in players)
					{
						BlockCoordinates bCoord = (BlockCoordinates) player.KnownPosition;

						chunksWithinRadiusOfPlayer = GetChunkCoordinatesForTick(new ChunkCoordinates(bCoord), chunksWithinRadiusOfPlayer, 17, Random); // Should actually be 15
					}

					if (chunksWithinRadiusOfPlayer.Count > 0)
					{
						bool canSpawnPassive = false;
						bool canSpawnHostile = false;

						if (DoMobspawning)
						{
							canSpawnPassive = TickTime % 400 == 0;

							var effectiveChunkCount = Math.Max(17 * 17, chunksWithinRadiusOfPlayer.Count);
							int entityPassiveCount = 0;
							int entityHostileCount = 0;
							foreach (var entity in entities)
							{
								if (entity is PassiveMob)
								{
									entityPassiveCount++;
								}
								else if (entity is HostileMob)
								{
									entityHostileCount++;
								}
							}


							var passiveCap = EntitySpawnManager.CapPassive * (effectiveChunkCount / 289f);
							canSpawnPassive = canSpawnPassive && entityPassiveCount < passiveCap;
							canSpawnPassive = canSpawnPassive || entityPassiveCount < passiveCap * 0.20; // Custom to get instant spawn when no mobs
							canSpawnHostile = entityHostileCount < EntitySpawnManager.CapHostile * (effectiveChunkCount / 289f);
						}

						var state = chunksWithinRadiusOfPlayer;

						Parallel.ForEach(state, spawnState =>
						{
							Random random = new Random(spawnState.Seed);

							ChunkColumn chunk = GetChunk(new ChunkCoordinates(spawnState.ChunkX, spawnState.ChunkZ), true);
							if (chunk == null) return; // Not loaded

							if (DoMobspawning)
							{
								int x = random.Next(16);
								int z = random.Next(16);

								var height = chunk.GetHeight(x, z);

								var chunkTickMeasurement = blockAndChunkTickMeasurement?.Begin("Chunk tick");

								var maxValue = (((height + 1) >> 4) + 1) * 16 - 1;
								var ySpawn = random.Next(Math.Abs(maxValue));
								var spawnCoordinates = new BlockCoordinates(x + spawnState.ChunkX * 16, ySpawn, z + spawnState.ChunkZ * 16);
								var spawnBlock = GetBlock(spawnCoordinates, chunk);
								if (spawnBlock.IsTransparent)
								{
									// Entity spawning, only one attempt per chunk
									EntitySpawnManager.AttemptMobSpawn(spawnCoordinates, random, canSpawnPassive, canSpawnHostile);
								}

								chunkTickMeasurement?.End();
							}

							if (EnableBlockTicking && RandomTickSpeed > 0)
							{
								for (int s = 0; s < 16; s++)
								{
									for (int i = 0; i < RandomTickSpeed; i++)
									{
										int x = random.Next(16);
										int y = random.Next(16);
										int z = random.Next(16);

										var blockTickMeasurement = blockAndChunkTickMeasurement?.Begin("Block tick");

										var blockCoordinates = new BlockCoordinates(x + spawnState.ChunkX * 16, y + s * 16, z + spawnState.ChunkZ * 16);
										var block = GetBlock(blockCoordinates, chunk);
										//Stopwatch sw = Stopwatch.StartNew();
										block.OnTick(this, true);
										//if(sw.ElapsedMilliseconds > 50)
										//{
										//	if (Log.IsDebugEnabled) Log.Warn($"Took a long time ({sw.ElapsedMilliseconds}) with block tick on {block}");
										//}
										blockTickMeasurement?.End();
									}
								}
							}
						});
					}
				}

				blockAndChunkTickMeasurement?.End();

				var blockUpdateMeasurement = worldTickMeasurement?.Begin("Block update tick");

				// Block updates
				foreach (KeyValuePair<BlockCoordinates, long> blockEvent in BlockWithTicks)
				{
					try
					{
						if (blockEvent.Value <= TickTime)
						{
							if (BlockWithTicks.TryRemove(blockEvent.Key, out _)) GetBlock(blockEvent.Key).OnTick(this, false);
						}
					}
					catch (Exception e)
					{
						Log.Warn("Block ticking", e);
					}
				}

				blockUpdateMeasurement?.End();

				var blockEntityMeasurement = worldTickMeasurement?.Begin("Block entity tick");
				// Block entity updates
				foreach (BlockEntity blockEntity in BlockEntities.ToArray())
				{
					blockEntity.OnTick(this);
				}

				blockEntityMeasurement?.End();

				var entityMeasurement = worldTickMeasurement?.Begin("Entity tick");

				// Entity updates
				foreach (Entity entity in entities)
				{
					entity.OnTick(entities);
				}

				entityMeasurement?.End();

				PlayerCount = players.Length;

				// Player tick
				var playerMeasurement = worldTickMeasurement?.Begin("Player tick");

				foreach (var player in players)
				{
					if (player.IsSpawned) player.OnTick(entities);
				}

				playerMeasurement?.End();

				// Send player movements
				BroadCastMovement(players, entities);

				//TODO: We don't want to trigger sending here. But right now
				// it seems better for performance since the send-tick is one for all
				// sessions, so we need to refactor that first.
				var tasks = new List<Task>();
				foreach (Player player in players)
				{
					if (player.NetworkHandler is RakSession session) tasks.Add(session.SendQueueAsync());
				}
				Task.WhenAll(tasks).Wait();

				if (Log.IsDebugEnabled && _tickTimer.ElapsedMilliseconds >= 50) Log.Error($"World tick too too long: {_tickTimer.ElapsedMilliseconds} ms");
			}
			catch (Exception e)
			{
				Log.Error("World ticking", e);
			}
			finally
			{
				LastTickProcessingTime = _tickTimer.ElapsedMilliseconds;
				AvarageTickProcessingTime = (AvarageTickProcessingTime * 9 + _tickTimer.ElapsedMilliseconds) / 10L;

				worldTickMeasurement?.End();
			}
		}

		public int GetSubtractedLight(BlockCoordinates coordinates)
		{
			return GetSubtractedLight(coordinates, SkylightSubtracted);
		}

		public int GetSubtractedLight(BlockCoordinates coordinates, int amount)
		{
			var skyLight = GetSkyLight(coordinates) - amount;
			var blockLight = GetBlockLight(coordinates);

			return (int) Math.Max(skyLight, blockLight);
		}

		public int CalculateSkylightSubtracted(long worldTime)
		{
			float f = CalculateCelestialAngle(worldTime);
			double f1 = 1.0F - (Math.Cos(f * ((float) Math.PI * 2F)) * 2.0F + 0.5F);
			f1 = BiomeUtils.Clamp((float) f1, 0.0F, 1.0F);
			f1 = 1.0F - f1;
			//f1 = (float)((double)f1 * (1.0D - (double)(this.getRainStrength(p_72967_1_) * 5.0F) / 16.0D));
			//f1 = (float)((double)f1 * (1.0D - (double)(this.getThunderStrength(p_72967_1_) * 5.0F) / 16.0D));
			f1 = 1.0F - f1;
			return (int) (f1 * 11.0F);
		}

		public float CalculateCelestialAngle(long worldTime)
		{
			int i = (int) (worldTime % 24000L);
			float f = ((float) i) / 24000.0F - 0.25F;

			if (f < 0.0F)
			{
				++f;
			}

			if (f > 1.0F)
			{
				--f;
			}

			float f1 = 1.0F - (float) ((Math.Cos((double) f * Math.PI) + 1.0D) / 2.0D);
			f = f + (f1 - f) / 3.0F;
			return f;
		}

		public Player[] GetSpawnedPlayers()
		{
			if (Players == null) return new Player[0]; // HACK

			return Players.Values.Where(player => player.IsSpawned).ToArray();
		}

		public Player[] GetAllPlayers()
		{
			if (Players == null) return new Player[0]; // HACK

			return Players.Values.ToArray();
		}

		public Entity[] GetEntites()
		{
			lock (Entities)
			{
				return Entities.Values.ToArray();
			}
		}

		private IEnumerable<Player> GetStaledPlayers(Player[] players)
		{
			DateTime now = DateTime.UtcNow;
			TimeSpan span = TimeSpan.FromSeconds(300);
			return players.Where(player => (now - player.LastUpdatedTime) > span);
		}
	}
}