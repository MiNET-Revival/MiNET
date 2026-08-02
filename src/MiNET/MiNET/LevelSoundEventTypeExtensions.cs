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
				LevelSoundEventType.EquipCopper => "armor.equip_copper",
				LevelSoundEventType.SpearAttackHit => "item.spear.attack_hit",
				LevelSoundEventType.SpearAttackMiss => "item.spear.attack_miss",
				LevelSoundEventType.SpearUse => "item.spear.use",
				LevelSoundEventType.WoodenSpearAttackHit => "item.wooden_spear.attack_hit",
				LevelSoundEventType.WoodenSpearAttackMiss => "item.wooden_spear.attack_miss",
				LevelSoundEventType.WoodenSpearUse => "item.wooden_spear.use",
				LevelSoundEventType.StoneSpearAttackHit => "item.stone_spear.attack_hit",
				LevelSoundEventType.StoneSpearAttackMiss => "item.stone_spear.attack_miss",
				LevelSoundEventType.StoneSpearUse => "item.stone_spear.use",
				LevelSoundEventType.CopperSpearAttackHit => "item.copper_spear.attack_hit",
				LevelSoundEventType.CopperSpearAttackMiss => "item.copper_spear.attack_miss",
				LevelSoundEventType.CopperSpearUse => "item.copper_spear.use",
				LevelSoundEventType.IronSpearAttackHit => "item.iron_spear.attack_hit",
				LevelSoundEventType.IronSpearAttackMiss => "item.iron_spear.attack_miss",
				LevelSoundEventType.IronSpearUse => "item.iron_spear.use",
				LevelSoundEventType.GoldenSpearAttackHit => "item.golden_spear.attack_hit",
				LevelSoundEventType.GoldenSpearAttackMiss => "item.golden_spear.attack_miss",
				LevelSoundEventType.GoldenSpearUse => "item.golden_spear.use",
				LevelSoundEventType.DiamondSpearAttackHit => "item.diamond_spear.attack_hit",
				LevelSoundEventType.DiamondSpearAttackMiss => "item.diamond_spear.attack_miss",
				LevelSoundEventType.DiamondSpearUse => "item.diamond_spear.use",
				LevelSoundEventType.NetheriteSpearAttackHit => "item.netherite_spear.attack_hit",
				LevelSoundEventType.NetheriteSpearAttackMiss => "item.netherite_spear.attack_miss",
				LevelSoundEventType.NetheriteSpearUse => "item.netherite_spear.use",
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
