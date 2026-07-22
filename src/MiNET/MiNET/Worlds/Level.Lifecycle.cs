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
		public void Initialize()
		{
			//IsWorldTimeStarted = false;
			WorldProvider.Initialize();

			SpawnPoint = SpawnPoint ?? new PlayerLocation(WorldProvider.GetSpawnPoint());
			TickTime = WorldProvider.GetTime();
			WorldTime = WorldProvider.GetDayTime();
			LevelName = WorldProvider.GetName();

			if (WorldProvider.IsCaching)
			{
				Stopwatch chunkLoading = Stopwatch.StartNew();

				// Pre-cache chunks for spawn coordinates
				int i = 0;

				var chunkCoordinates = new ChunkCoordinates(SpawnPoint);
				if (Dimension == Dimension.Nether)
				{
					chunkCoordinates /= 8;
				}

				foreach (var chunk in GenerateChunks(chunkCoordinates, new Dictionary<ChunkCoordinates, McpeWrapper>(), ViewDistance))
				{
					if (chunk != null) i++;
				}

				Log.Info($"World pre-cache {i} chunks completed in {chunkLoading.ElapsedMilliseconds}ms");
			}

			if (Dimension == Dimension.Overworld)
			{
				if (Config.GetProperty("CheckForSafeSpawn", false))
				{
					var height = GetHeight((BlockCoordinates) SpawnPoint);
					if (height > SpawnPoint.Y) SpawnPoint.Y = height;
					Log.Debug("Checking for safe spawn");
				}

				if (LevelManager != null && WorldProvider.HaveNether())
				{
					NetherLevel = LevelManager.GetDimension(this, Dimension.Nether);
				}
				if (LevelManager != null && WorldProvider.HaveTheEnd())
				{
					TheEndLevel = LevelManager.GetDimension(this, Dimension.TheEnd);
				}
			}

			//SpawnPoint.Y = 20;

			StartTimeInTicks = DateTime.UtcNow.Ticks;

			_tickTimer = new Stopwatch();
			_tickTimer.Restart();
			_tickerHighPrecisionTimer = new HighPrecisionTimer(50, WorldTick, false, false, Config.GetProperty("EnableHighPrecision", true));
		}

		private void _tickerHighPrecisionTimer_Tick()
		{
			WorldTick(null);
		}

		private HighPrecisionTimer _tickerHighPrecisionTimer;

		public virtual void Close()
		{
			WorldProvider?.SaveChunks();

			NetherLevel?.Close();
			TheEndLevel?.Close();

			_tickerHighPrecisionTimer?.Dispose();
			_tickerHighPrecisionTimer = null;

			foreach (var entity in Entities.Values.ToArray())
			{
				entity.DespawnEntity();
			}

			Entities.Clear();

			foreach (Player player in Players.Values.ToArray())
			{
				player.Disconnect("Unexpected player lingering on close of level: " + player.Username);
			}

			Players.Clear();

			BlockEntities.Clear();

			BlockWithTicks.Clear();
			BlockWithTicks = null;
			BlockEntities = null;
			Players = null;
			Entities = null;

			if (WorldProvider is AnvilWorldProvider provider)
			{
				foreach (var chunk in provider._chunkCache)
				{
					provider._chunkCache.TryRemove(chunk.Key, out var waste);
					if (waste == null) continue;

					foreach (var c in waste)
					{
						c.Dispose();
					}

					waste.ClearCache();
				}
			}

			WorldProvider = null;

			Log.Info("Closed level: " + LevelId);
		}

		internal static McpeWrapper CreateMcpeBatch(byte[] bytes)
		{
			return BatchUtils.CreateBatchPacket(new Memory<byte>(bytes, 0, (int) bytes.Length), CompressionLevel.Optimal, true);
		}
	}
}
