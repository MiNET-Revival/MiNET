using System;
using System.IO;
using System.Text;
using fNbt;
using MiNET.Items;
using MiNET.Utils.Nbt;

namespace MiNET.Net;

public abstract partial class Packet
{
	public void WriteNetworkItemStackDescriptor(Item stack)
	{
		var air = stack == null || stack is ItemAir;

		Write((short) (air ? 0 : stack.RuntimeId));
		Write((ushort) (air ? 0 : stack.Count));
		WriteUnsignedVarInt((uint) (air ? 0 : stack.Metadata));

		var hasNetId = !air && stack.UniqueId != 0;
		Write(hasNetId);
		if (hasNetId)
		{
			WriteUnsignedVarInt(0); // ItemStackNetId variant.
			WriteSignedVarInt(stack.UniqueId);
		}

		WriteUnsignedVarInt((uint) (air ? 0 : stack.BlockRuntimeId));
		if (air)
		{
			WriteUnsignedVarInt(0);
			return;
		}

		using var stream = new MemoryStream();
		using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
		{
			if (stack.ExtraData != null)
			{
				writer.Write(ushort.MaxValue);
				writer.Write((byte) 1);
				NbtExtensions.Write(writer.BaseStream, stack.ExtraData, NbtFlavor.BedrockNoVarInt);
			}
			else
			{
				writer.Write((short) 0);
			}

			writer.Write(0); // CanPlaceOn count.
			writer.Write(0); // CanDestroy count.

			if (stack is ItemShield)
			{
				writer.Write((long) 0);
			}
		}

		var userData = stream.ToArray();
		WriteUnsignedVarInt((uint) userData.Length);
		Write(userData);
	}

	public Item ReadNetworkItemStackDescriptor()
	{
		var runtimeId = ReadShort();
		var count = ReadUshort();
		var metadata = ReadUnsignedVarInt();

		var uniqueId = 0;
		if (ReadBool())
		{
			var variant = ReadUnsignedVarInt();
			if (variant > 2)
			{
				throw new InvalidDataException($"Unsupported network item stack ID variant {variant}.");
			}

			uniqueId = ReadSignedVarInt();
		}

		var blockRuntimeId = ReadUnsignedVarInt();
		var userDataLength = ReadUnsignedVarInt();
		var userData = ReadBytes((int) userDataLength);

		if (runtimeId == 0)
		{
			return new ItemAir();
		}

		var stack = ItemFactory.GetItem(runtimeId, (int) blockRuntimeId, (short) metadata, (byte) count);
		stack.UniqueId = uniqueId;

		using var stream = new MemoryStream(userData);
		using var reader = new BinaryReader(stream);
		if (stream.Length == 0) return stack;

		var nbtLength = reader.ReadUInt16();
		if (nbtLength == ushort.MaxValue)
		{
			var version = reader.ReadByte();
			if (version != 1)
			{
				throw new InvalidDataException($"Unsupported item NBT version {version}.");
			}

			stack.ExtraData = NbtExtensions.ReadNbtCompound(stream, NbtFlavor.BedrockNoVarInt);
		}
		else if (nbtLength > 0)
		{
			throw new InvalidDataException($"Unsupported fixed item NBT length {nbtLength}.");
		}

		SkipDescriptorStringList(reader);
		SkipDescriptorStringList(reader);

		if (stack is ItemShield && stream.Position + sizeof(long) <= stream.Length)
		{
			reader.ReadInt64();
		}

		return stack;
	}

	private static void SkipDescriptorStringList(BinaryReader reader)
	{
		var count = reader.ReadInt32();
		for (var i = 0; i < count; i++)
		{
			var length = reader.ReadUInt16();
			reader.ReadBytes(length);
		}
	}
}
