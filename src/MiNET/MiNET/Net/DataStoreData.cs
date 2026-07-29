using System;
using System.Collections.Generic;

namespace MiNET.Net
{
	public enum DataStoreEntryType
	{
		Update = 0,
		Change = 1,
		Removal = 2
	}

	public enum DataStoreValueType
	{
		Double = 0,
		Bool = 1,
		String = 2
	}

	public sealed class DataStoreEntry
	{
		public DataStoreEntryType EntryType { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Property { get; set; } = string.Empty;
		public string Path { get; set; } = string.Empty;
		public DataStoreValueType ValueType { get; set; }
		public object Value { get; set; }
		public uint UpdateCount { get; set; }
		public uint PathUpdateCount { get; set; }
	}

	public sealed class DataStoreEntries : IPacketDataObject
	{
		public List<DataStoreEntry> Entries { get; } = new();

		public void Write(Packet packet)
		{
			packet.WriteUnsignedVarInt((uint) Entries.Count);
			foreach (var entry in Entries)
			{
				packet.WriteUnsignedVarInt((uint) entry.EntryType);
				packet.Write(entry.Name);
				if (entry.EntryType == DataStoreEntryType.Removal) continue;

				packet.Write(entry.Property);
				if (entry.EntryType == DataStoreEntryType.Update) packet.Write(entry.Path);
				if (entry.EntryType == DataStoreEntryType.Change) packet.WriteUnsignedVarInt(entry.UpdateCount);
				WriteValue(packet, entry.ValueType, entry.Value);
				if (entry.EntryType == DataStoreEntryType.Update)
				{
					packet.Write(entry.UpdateCount);
					packet.Write(entry.PathUpdateCount);
				}
			}
		}

		public static DataStoreEntries Read(Packet packet)
		{
			var result = new DataStoreEntries();
			var count = packet.ReadUnsignedVarInt();
			for (var i = 0u; i < count; i++)
			{
				var entry = new DataStoreEntry
				{
					EntryType = (DataStoreEntryType) packet.ReadUnsignedVarInt(),
					Name = packet.ReadString()
				};
				if (entry.EntryType != DataStoreEntryType.Removal)
				{
					entry.Property = packet.ReadString();
					if (entry.EntryType == DataStoreEntryType.Update) entry.Path = packet.ReadString();
					if (entry.EntryType == DataStoreEntryType.Change) entry.UpdateCount = packet.ReadUnsignedVarInt();
					ReadValue(packet, entry);
					if (entry.EntryType == DataStoreEntryType.Update)
					{
						entry.UpdateCount = packet.ReadUint();
						entry.PathUpdateCount = packet.ReadUint();
					}
				}
				result.Entries.Add(entry);
			}
			return result;
		}

		internal static void WriteValue(Packet packet, DataStoreValueType type, object value)
		{
			packet.WriteUnsignedVarInt((uint) type);
			switch (type)
			{
				case DataStoreValueType.Double: packet.Write(Convert.ToDouble(value)); break;
				case DataStoreValueType.Bool: packet.Write(Convert.ToBoolean(value)); break;
				case DataStoreValueType.String: packet.Write(Convert.ToString(value) ?? string.Empty); break;
				default: throw new ArgumentOutOfRangeException(nameof(type));
			}
		}

		internal static void ReadValue(Packet packet, DataStoreEntry entry)
		{
			entry.ValueType = (DataStoreValueType) packet.ReadUnsignedVarInt();
			entry.Value = entry.ValueType switch
			{
				DataStoreValueType.Double => packet.ReadDouble(),
				DataStoreValueType.Bool => packet.ReadBool(),
				DataStoreValueType.String => packet.ReadString(),
				_ => throw new ArgumentOutOfRangeException(nameof(entry.ValueType))
			};
		}
	}

	public sealed class ServerboundDataStoreData : IPacketDataObject
	{
		public DataStoreEntry Entry { get; set; } = new();

		public void Write(Packet packet)
		{
			packet.Write(Entry.Name);
			packet.Write(Entry.Property);
			packet.Write(Entry.Path);
			DataStoreEntries.WriteValue(packet, Entry.ValueType, Entry.Value);
			packet.WriteUnsignedVarInt(Entry.UpdateCount);
			packet.Write(Entry.PathUpdateCount);
		}

		public static ServerboundDataStoreData Read(Packet packet)
		{
			var entry = new DataStoreEntry
			{
				Name = packet.ReadString(),
				Property = packet.ReadString(),
				Path = packet.ReadString()
			};
			DataStoreEntries.ReadValue(packet, entry);
			entry.UpdateCount = packet.ReadUnsignedVarInt();
			entry.PathUpdateCount = packet.ReadUint();
			return new ServerboundDataStoreData { Entry = entry };
		}
	}
}
