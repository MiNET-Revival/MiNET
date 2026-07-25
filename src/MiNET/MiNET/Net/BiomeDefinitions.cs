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
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2026 Niclas Olofsson.
// All Rights Reserved.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using MiNET.Worlds;

namespace MiNET.Net
{
	public class BiomeStringList : List<string>, IPacketDataObject
	{
		private readonly Dictionary<string, ushort> _indexes = new Dictionary<string, ushort>();

		public ushort GetOrAdd(string value)
		{
			if (_indexes.TryGetValue(value, out var index)) return index;

			index = checked((ushort) Count);
			_indexes[value] = index;
			Add(value);
			return index;
		}

		public void Write(Packet packet)
		{
			packet.WriteLength(Count);

			foreach (var value in this)
			{
				packet.Write(value);
			}
		}

		public static BiomeStringList Read(Packet packet)
		{
			var list = new BiomeStringList();
			var count = packet.ReadLength();
			for (var i = 0; i < count; i++)
			{
				list.GetOrAdd(packet.ReadString());
			}

			return list;
		}
	}

	public class BiomeDefinitions : List<BiomeDefinition>, IPacketDataObject
	{
		public static BiomeDefinitions FromBiomes(IEnumerable<Biome> biomes, BiomeStringList stringList)
		{
			var definitions = new BiomeDefinitions();

			foreach (var biome in biomes.OrderBy(b => b.Id))
			{
				definitions.Add(BiomeDefinition.FromBiome(biome, stringList));
			}

			return definitions;
		}

		public void Write(Packet packet)
		{
			packet.WriteLength(Count);

			foreach (var definition in this)
			{
				definition.Write(packet);
			}
		}

		public static BiomeDefinitions Read(Packet packet)
		{
			var definitions = new BiomeDefinitions();
			var count = packet.ReadLength();
			for (var i = 0; i < count; i++)
			{
				definitions.Add(BiomeDefinition.Read(packet));
			}

			return definitions;
		}
	}

	public class BiomeDefinition : IPacketDataObject
	{
		public ushort NameIndex { get; set; }
		public ushort? BiomeId { get; set; }
		public float Temperature { get; set; }
		public float Downfall { get; set; }
		public float RedSporeDensity { get; set; }
		public float BlueSporeDensity { get; set; }
		public float AshDensity { get; set; }
		public float WhiteAshDensity { get; set; }
		public float Depth { get; set; }
		public float Scale { get; set; }
		public int MapWaterColorArgb { get; set; }
		public bool Rain { get; set; }

		public static BiomeDefinition FromBiome(Biome biome, BiomeStringList stringList)
		{
			return new BiomeDefinition
			{
				NameIndex = stringList.GetOrAdd(biome.Name),
				BiomeId = null,
				Temperature = biome.Temperature,
				Downfall = biome.Downfall,
				RedSporeDensity = biome.RedSpores,
				BlueSporeDensity = biome.BlueSpores,
				AshDensity = biome.Ash,
				WhiteAshDensity = biome.WhiteAsh,
				Depth = biome.Depth,
				Scale = biome.Height,
				MapWaterColorArgb = ToArgb(biome),
				Rain = biome.Rain
			};
		}

		public void Write(Packet packet)
		{
			packet.Write(NameIndex);
			packet.Write(BiomeId.HasValue);
			if (BiomeId.HasValue)
			{
				packet.Write(BiomeId.Value);
			}

			packet.Write(Temperature);
			packet.Write(Downfall);
			packet.Write(RedSporeDensity);
			packet.Write(BlueSporeDensity);
			packet.Write(AshDensity);
			packet.Write(WhiteAshDensity);
			packet.Write(Depth);
			packet.Write(Scale);
			packet.Write(MapWaterColorArgb);
			packet.Write(Rain);

			packet.Write(false); // tags
			packet.Write(false); // chunk_generation
		}

		public static BiomeDefinition Read(Packet packet)
		{
			var definition = new BiomeDefinition
			{
				NameIndex = packet.ReadUshort()
			};

			if (packet.ReadBool())
			{
				definition.BiomeId = packet.ReadUshort();
			}

			definition.Temperature = packet.ReadFloat();
			definition.Downfall = packet.ReadFloat();
			definition.RedSporeDensity = packet.ReadFloat();
			definition.BlueSporeDensity = packet.ReadFloat();
			definition.AshDensity = packet.ReadFloat();
			definition.WhiteAshDensity = packet.ReadFloat();
			definition.Depth = packet.ReadFloat();
			definition.Scale = packet.ReadFloat();
			definition.MapWaterColorArgb = packet.ReadInt();
			definition.Rain = packet.ReadBool();

			if (packet.ReadBool())
			{
				var tagCount = packet.ReadLength();
				for (var i = 0; i < tagCount; i++) packet.ReadUshort();
			}

			if (packet.ReadBool())
			{
				throw new NotSupportedException("Reading v800 biome chunk generation data is not implemented.");
			}

			return definition;
		}

		private static int ToArgb(Biome biome)
		{
			var a = ToByte(biome.WaterColor.A);
			var r = ToByte(biome.WaterColor.R);
			var g = ToByte(biome.WaterColor.G);
			var b = ToByte(biome.WaterColor.B);
			return (a << 24) | (r << 16) | (g << 8) | b;
		}

		private static int ToByte(float value)
		{
			if (value <= 0) return 0;
			if (value >= 1) return 255;
			return (int) MathF.Round(value * 255);
		}
	}
}