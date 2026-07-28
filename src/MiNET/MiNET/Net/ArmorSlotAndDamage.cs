using System;
using System.Collections.Generic;

namespace MiNET.Net
{
	public enum ArmorSlot
	{
		Helmet = 0,
		Chestplate = 1,
		Leggings = 2,
		Boots = 3,
		Body = 4
	}

	public readonly record struct ArmorSlotAndDamage(ArmorSlot Slot, short Damage);

	public class ArmorSlotAndDamagePairs : List<ArmorSlotAndDamage>, IPacketDataObject
	{
		public void Write(Packet packet)
		{
			packet.WriteUnsignedVarInt((uint) Count);
			foreach (var pair in this)
			{
				packet.WriteVarInt((int) pair.Slot);
				packet.Write(pair.Damage);
			}
		}

		public static ArmorSlotAndDamagePairs Read(Packet packet)
		{
			var pairs = new ArmorSlotAndDamagePairs();
			var count = packet.ReadUnsignedVarInt();
			for (var i = 0u; i < count; i++)
			{
				var slot = packet.ReadVarInt();
				if (slot < (int) ArmorSlot.Helmet || slot > (int) ArmorSlot.Body)
				{
					throw new InvalidOperationException($"Invalid armor slot {slot}.");
				}

				pairs.Add(new ArmorSlotAndDamage((ArmorSlot) slot, packet.ReadShort()));
			}

			return pairs;
		}
	}
}
