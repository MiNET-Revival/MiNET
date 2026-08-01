using System.Text;

namespace MiNET
{
	public static class LevelSoundEventTypeExtensions
	{
		public static string GetSerializedName(this LevelSoundEventType sound)
		{
			return sound switch
			{
				LevelSoundEventType.ItemUseOn => "item.use.on",
				LevelSoundEventType.ShulkerBoxOpen => "shulkerbox.open",
				LevelSoundEventType.ShulkerBoxClosed => "shulkerbox.closed",
				LevelSoundEventType.EnderChestOpen => "enderchest.open",
				LevelSoundEventType.EnderChestClosed => "enderchest.closed",
				LevelSoundEventType.EquipChain => "armor.equip_chain",
				LevelSoundEventType.EquipDiamond => "armor.equip_diamond",
				LevelSoundEventType.EquipGeneric => "armor.equip_generic",
				LevelSoundEventType.EquipGold => "armor.equip_gold",
				LevelSoundEventType.EquipIron => "armor.equip_iron",
				LevelSoundEventType.EquipLeather => "armor.equip_leather",
				LevelSoundEventType.EquipElytra => "armor.equip_elytra",
				LevelSoundEventType.EquipNetherite => "armor.equip_netherite",
				_ => ToDottedLowerName(sound.ToString())
			};
		}

		private static string ToDottedLowerName(string name)
		{
			var result = new StringBuilder(name.Length + 4);
			for (var index = 0; index < name.Length; index++)
			{
				var character = name[index];
				if (index > 0 && char.IsUpper(character) && char.IsLower(name[index - 1]))
				{
					result.Append('.');
				}

				result.Append(char.ToLowerInvariant(character));
			}

			return result.ToString();
		}
	}
}
