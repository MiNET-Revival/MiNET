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
		public void ApplyPhysics(int x, int y, int z)
		{
			DoPhysics(x - 1, y, z);
			DoPhysics(x + 1, y, z);
			DoPhysics(x, y - 1, z);
			DoPhysics(x, y + 1, z);
			DoPhysics(x, y, z - 1);
			DoPhysics(x, y, z + 1);
		}

		private void DoPhysics(int x, int y, int z)
		{
			Block block = GetBlock(x, y, z);
			if (block is Air) return;
			block.DoPhysics(this);
		}

		public void ScheduleBlockTick(Block block, int tickRate)
		{
			if (BlockWithTicks.ContainsKey(block.Coordinates)) return;
			BlockWithTicks[block.Coordinates] = TickTime + tickRate;
		}

		public void CancelBlockTick(Block block)
		{
			BlockWithTicks.TryRemove(block.Coordinates, out _);
		}

		public bool TryGetEntity<T>(long targetEntityId, out T entity) where T : class
		{
			entity = null;

			if (Players.TryGetValue(targetEntityId, out var player))
			{
				entity = player as T;
			}
			else if (Entities.TryGetValue(targetEntityId, out var ent))
			{
				entity = ent as T;
			}

			return entity != null;
		}


		public ChunkColumn[] GetLoadedChunks()
		{
			var cacheProvider = WorldProvider as ICachingWorldProvider;
			if (cacheProvider != null)
			{
				return cacheProvider.GetCachedChunks();
			}

			return new ChunkColumn[0];
		}

		public void ClearLoadedChunks()
		{
			var cacheProvider = WorldProvider as ICachingWorldProvider;
			cacheProvider?.ClearCachedChunks();
		}
	}
}