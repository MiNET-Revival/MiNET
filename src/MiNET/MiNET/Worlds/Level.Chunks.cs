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
		public List<EntitySpawnManager.SpawnState> GetChunkCoordinatesForTick(ChunkCoordinates chunkPosition, List<EntitySpawnManager.SpawnState> chunksUsed, double radius, Random random)
		{
			{
				List<EntitySpawnManager.SpawnState> newOrders = new List<EntitySpawnManager.SpawnState>();

				int centerX = chunkPosition.X;
				int centerZ = chunkPosition.Z;

				int halfRadius = (int) Math.Floor(radius / 2f);

				for (double x = -halfRadius; x <= halfRadius; ++x)
				{
					for (double z = -halfRadius; z <= halfRadius; ++z)
					{
						int chunkX = (int) (x + centerX);
						int chunkZ = (int) (z + centerZ);
						EntitySpawnManager.SpawnState index = new EntitySpawnManager.SpawnState(chunkX, chunkZ, random.Next());
						newOrders.Add(index);
					}
				}

				return newOrders.Union(chunksUsed).ToList();
			}
		}

		public IEnumerable<McpeWrapper> GenerateChunks(ChunkCoordinates chunkPosition, Dictionary<ChunkCoordinates, McpeWrapper> chunksUsed, double radius, Func<Vector3> getCurrentPositionAction = null)
		{
			lock (chunksUsed)
			{
				var newOrders = new Dictionary<ChunkCoordinates, double>();

				double radiusSquared = Math.Pow(radius, 2);

				int centerX = chunkPosition.X;
				int centerZ = chunkPosition.Z;

				for (double x = -radius; x <= radius; ++x)
				{
					for (double z = -radius; z <= radius; ++z)
					{
						var distance = (x * x) + (z * z);
						if (distance > radiusSquared)
						{
							continue;
						}
						int chunkX = (int) (x + centerX);
						int chunkZ = (int) (z + centerZ);
						var index = new ChunkCoordinates(chunkX, chunkZ);
						newOrders[index] = distance;
					}
				}

				foreach (var chunkKey in chunksUsed.Keys.ToArray())
				{
					if (!newOrders.ContainsKey(chunkKey))
					{
						chunksUsed.Remove(chunkKey);
					}
				}

				foreach (var pair in newOrders.OrderBy(pair => pair.Value))
				{
					if (chunksUsed.ContainsKey(pair.Key)) continue;

					if (WorldProvider == null) continue;

					if (getCurrentPositionAction != null)
					{
						var currentPos = getCurrentPositionAction();
						var coords = new ChunkCoordinates(currentPos);
						if(coords.DistanceTo(pair.Key) > radius) continue;
					}
					ChunkColumn chunkColumn = GetChunk(pair.Key);
					McpeWrapper chunk = null;
					if (chunkColumn != null)
					{
						chunk = chunkColumn.GetBatch();
						chunksUsed.Add(pair.Key, chunk);
					}

					yield return chunk;
				}
			}
		}
	}
}