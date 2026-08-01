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
		public Block GetBlock(PlayerLocation location)
		{
			return GetBlock((BlockCoordinates) location);
		}

		public Block GetBlock(int x, int y, int z)
		{
			return GetBlock(new BlockCoordinates(x, y, z));
		}

		public Block GetBlock(BlockCoordinates blockCoordinates, ChunkColumn tryChunk = null)
		{
			ChunkColumn chunk = null;

			var chunkCoordinates = new ChunkCoordinates(blockCoordinates.X >> 4, blockCoordinates.Z >> 4);
			if (tryChunk != null && tryChunk.X == chunkCoordinates.X && tryChunk.Z == chunkCoordinates.Z)
			{
				chunk = tryChunk;
			}
			else
			{
				chunk = GetChunk(chunkCoordinates);
			}
			if (chunk == null)
				return new Air
				{
					Coordinates = blockCoordinates,
					SkyLight = 15
				};

			var block = chunk.GetBlockObject(blockCoordinates.X & 0x0f, blockCoordinates.Y, blockCoordinates.Z & 0x0f);
			byte blockLight = chunk.GetBlocklight(blockCoordinates.X & 0x0f, blockCoordinates.Y, blockCoordinates.Z & 0x0f);
			byte skyLight = chunk.GetSkylight(blockCoordinates.X & 0x0f, blockCoordinates.Y, blockCoordinates.Z & 0x0f);
			byte biomeId = chunk.GetBiome(blockCoordinates.X & 0x0f, blockCoordinates.Y, blockCoordinates.Z & 0x0f);

			//Block block = BlockFactory.GetBlockById(bid);
			block.Coordinates = blockCoordinates;
			//block.Metadata = metadata;
			block.BlockLight = blockLight;
			block.SkyLight = skyLight;
			block.BiomeId = biomeId;

			return block;
		}

		public bool IsAir(BlockCoordinates blockCoordinates)
		{
			return IsBlock<Air>(blockCoordinates);
		}

		public bool IsBlock<T>(BlockCoordinates blockCoordinates) where T : Block
		{
			return IsBlock(blockCoordinates, typeof(T));
		}

		public bool IsBlock(BlockCoordinates blockCoordinates, Type blockType)
		{
			ChunkColumn chunk = GetChunk(blockCoordinates);
			if (chunk == null) return false;

			return BlockFactory.IsBlock(chunk.GetBlockRuntimeId(blockCoordinates.X & 0x0f, blockCoordinates.Y, blockCoordinates.Z & 0x0f), blockType);
		}

		public bool IsBlock(BlockCoordinates blockCoordinates, string blockId)
		{
			ChunkColumn chunk = GetChunk(blockCoordinates);
			if (chunk == null) return false;

			return BlockFactory.GetIdByRuntimeId(chunk.GetBlockRuntimeId(blockCoordinates.X & 0x0f, blockCoordinates.Y, blockCoordinates.Z & 0x0f)) == blockId;
		}

		public bool IsNotBlockingSkylight(BlockCoordinates blockCoordinates)
		{
			ChunkColumn chunk = GetChunk(blockCoordinates);
			if (chunk == null) return true;

			int bid = chunk.GetBlockRuntimeId(blockCoordinates.X & 0x0f, blockCoordinates.Y, blockCoordinates.Z & 0x0f);
			return BlockFactory.IsBlock<Air>(bid) || BlockFactory.IsBlock<Glass>(bid) || BlockFactory.IsBlock<StainedGlassBase>(bid); // Need this for skylight calculations. Revise!
		}

		public bool IsTransparent(BlockCoordinates blockCoordinates)
		{
			ChunkColumn chunk = GetChunk(blockCoordinates);
			if (chunk == null) return true;

			int bid = chunk.GetBlockRuntimeId(blockCoordinates.X & 0x0f, blockCoordinates.Y, blockCoordinates.Z & 0x0f);
			return BlockFactory.IsTransparent(bid);
		}

		public int GetHeight(BlockCoordinates blockCoordinates)
		{
			ChunkColumn chunk = GetChunk(blockCoordinates);
			if (chunk == null) return ChunkColumn.WorldMaxY;

			return chunk.GetHeight(blockCoordinates.X & 0x0f, blockCoordinates.Z & 0x0f);
		}

		public byte GetSkyLight(BlockCoordinates blockCoordinates)
		{
			ChunkColumn chunk = GetChunk(blockCoordinates);

			if (chunk == null) return 15;

			return chunk.GetSkylight(blockCoordinates.X & 0x0f, blockCoordinates.Y, blockCoordinates.Z & 0x0f);
		}

		public byte GetBlockLight(BlockCoordinates blockCoordinates)
		{
			ChunkColumn chunk = GetChunk(blockCoordinates);

			if (chunk == null) return 15;

			return chunk.GetBlocklight(blockCoordinates.X & 0x0f, blockCoordinates.Y, blockCoordinates.Z & 0x0f);
		}

		public byte GetBiomeId(BlockCoordinates blockCoordinates)
		{
			ChunkColumn chunk = GetChunk(blockCoordinates);

			if (chunk == null) return 0;

			return chunk.GetBiome(blockCoordinates.X & 0x0f, blockCoordinates.Y, blockCoordinates.Z & 0x0f);
		}

		public ChunkColumn GetChunk(BlockCoordinates blockCoordinates, bool cacheOnly = false)
		{
			return GetChunk((ChunkCoordinates) blockCoordinates, cacheOnly);
		}

		public ChunkColumn GetChunk(ChunkCoordinates chunkCoordinates, bool cacheOnly = false)
		{
			var chunk = WorldProvider.GenerateChunkColumn(chunkCoordinates, cacheOnly);
			if (!cacheOnly && chunk == null) Log.Debug($"Got <null> chunk at {chunkCoordinates}");
			return chunk;
		}

		public void SetBlock(Block block, bool broadcast = true, bool applyPhysics = true, bool calculateLight = true, ChunkColumn possibleChunk = null)
		{
			if (block.Coordinates.Y < ChunkColumn.WorldMinY) return;

			var chunkCoordinates = new ChunkCoordinates(block.Coordinates.X >> 4, block.Coordinates.Z >> 4);
			ChunkColumn chunk = possibleChunk != null && possibleChunk.X == chunkCoordinates.X && possibleChunk.Z == chunkCoordinates.Z ? possibleChunk : GetChunk(chunkCoordinates);


			if (!OnBlockPlace(new BlockPlaceEventArgs(null, this, block, null)))
			{
				return;
			}

			chunk.SetBlock(block.Coordinates.X & 0x0f, block.Coordinates.Y, block.Coordinates.Z & 0x0f, block);
			if (calculateLight && chunk.GetHeight(block.Coordinates.X & 0x0f, block.Coordinates.Z & 0x0f) <= block.Coordinates.Y + 1)
			{
				chunk.RecalcHeight(block.Coordinates.X & 0x0f, block.Coordinates.Z & 0x0f, Math.Min(ChunkColumn.WorldHeight, block.Coordinates.Y + 1));
			}

			if (applyPhysics) ApplyPhysics(block.Coordinates.X, block.Coordinates.Y, block.Coordinates.Z);

			// !!!now the client is calculating the light!!!
			//// We should not ignore creative. Need to investigate.
			//if (GameMode != GameMode.Creative && calculateLight /* && block.LightLevel > 0*/)
			//{
			//	if (Dimension == Dimension.Overworld) new SkyLightCalculations().Calculate(this, block.Coordinates);

			//	block.BlockLight = (byte) block.LightLevel;
			//	chunk.SetBlocklight(block.Coordinates.X & 0x0f, block.Coordinates.Y, block.Coordinates.Z & 0x0f, (byte) block.LightLevel);
			//	BlockLightCalculations.Calculate(this, block.Coordinates);
			//}

			if (broadcast)
			{
				var message = McpeUpdateBlock.CreateObject();
				message.blockRuntimeId = (uint) block.RuntimeId;
				message.coordinates = block.Coordinates;
				message.blockPriority = 0x13;
				RelayBroadcast(message);
			}

			block.BlockAdded(this);
		}

		private void CalculateSkyLight(int x, int y, int z)
		{
			DoLight(x, y, z);
			DoLight(x - 1, y, z);
			DoLight(x + 1, y, z);
			DoLight(x, y, z - 1);
			DoLight(x, y, z + 1);
			DoLight(x - 1, y, z - 1);
			DoLight(x - 1, y, z + 1);
			DoLight(x + 1, y, z - 1);
			DoLight(x + 1, y, z + 1);
		}

		private void DoLight(int x, int y, int z)
		{
			//Block block = GetBlock(x, y, z);
			//if (block is Air) return;
			//new SkyLightCalculations().Calculate(this, block);
		}

		public void SetBlockLight(Block block)
		{
			ChunkColumn chunk = GetChunk(new ChunkCoordinates(block.Coordinates.X >> 4, block.Coordinates.Z >> 4));
			chunk.SetBlocklight(block.Coordinates.X & 0x0f, block.Coordinates.Y, block.Coordinates.Z & 0x0f, block.BlockLight);
		}

		public void SetBlockLight(BlockCoordinates coordinates, byte blockLight)
		{
			ChunkColumn chunk = GetChunk(coordinates);
			chunk?.SetBlocklight(coordinates.X & 0x0f, coordinates.Y, coordinates.Z & 0x0f, blockLight);
		}

		public void SetBiomeId(BlockCoordinates coordinates, byte biomeId)
		{
			ChunkColumn chunk = GetChunk(coordinates);
			chunk?.SetBiome(coordinates.X & 0x0f, coordinates.Y, coordinates.Z & 0x0f, biomeId);
		}

		public void SetSkyLight(Block block)
		{
			ChunkColumn chunk = GetChunk(new ChunkCoordinates(block.Coordinates.X >> 4, block.Coordinates.Z >> 4));
			chunk.SetSkyLight(block.Coordinates.X & 0x0f, block.Coordinates.Y, block.Coordinates.Z & 0x0f, block.SkyLight);
		}

		public void SetSkyLight(BlockCoordinates coordinates, byte skyLight)
		{
			ChunkColumn chunk = GetChunk(coordinates);
			chunk?.SetSkyLight(coordinates.X & 0x0f, coordinates.Y, coordinates.Z & 0x0f, skyLight);
		}

		public void SetAir(BlockCoordinates blockCoordinates, bool broadcast = true)
		{
			SetAir(blockCoordinates.X, blockCoordinates.Y, blockCoordinates.Z, broadcast);
		}

		public void SetAir(int x, int y, int z, bool broadcast = true)
		{
			Block air = new Air();
			air.Coordinates = new BlockCoordinates(x, y, z);
			SetBlock(air, broadcast);
		}
	}
}
