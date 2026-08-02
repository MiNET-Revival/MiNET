using System;
using System.Collections.Generic;
using System.Numerics;
using MiNET.Blocks;
using MiNET.Entities;
using MiNET.Utils.Vectors;
using MiNET.Worlds;

namespace MiNET.Items
{
	public readonly record struct SpearAttackResult(
		int Damage,
		bool IsCharge,
		bool ShouldDismount,
		bool ShouldKnockback);

	/// <summary>
	/// Shared server-side behaviour for the seven vanilla spear tiers.
	/// The client owns the spear animation and targeting; the server validates
	/// charge timing, calculates kinetic damage and applies durability loss.
	/// </summary>
	public abstract class ItemSpearBase : Item
	{
		private const int ContactCooldownTicks = 10;
		private const float MinimumRelativeChargeSpeed = 4.6f;

		private long _chargeStartedTick = -1;
		private Dictionary<long, long> _lastChargeContactByEntity = new();

		protected ItemSpearBase()
		{
			MaxStackSize = 1;
			ItemType = ItemType.Spear;
		}

		public override void UseItem(Level world, Player player, BlockCoordinates blockCoordinates)
		{
			if (_chargeStartedTick >= 0)
			{
				return;
			}

			_chargeStartedTick = world.TickTime;
			world.BroadcastSound(player.GetEyesPosition(), GetUseSound(), runtimeEntityId: player.EntityId);
		}

		public override void Release(Level world, Player player, BlockCoordinates blockCoordinates)
		{
			ResetCharge();
		}

		public bool TryResolveAttack(Player attacker, Entity target, out SpearAttackResult result)
		{
			result = default;

			if (_chargeStartedTick < 0)
			{
				result = new SpearAttackResult(GetDamage(), false, false, true);
				return true;
			}

			var profile = GetCombatProfile();
			var currentTick = attacker.Level.TickTime;
			var activeTicks = currentTick - _chargeStartedTick - profile.DelayTicks;
			if (activeTicks < 0 || activeTicks > profile.DamageMaxDurationTicks)
			{
				return false;
			}

			if (_lastChargeContactByEntity.TryGetValue(target.EntityId, out var lastContact)
				&& currentTick - lastContact < ContactCooldownTicks)
			{
				return false;
			}

			var forwardSpeed = GetForwardSpeed(attacker);
			var targetForwardSpeed = GetTargetForwardSpeed(attacker, target);
			var relativeSpeed = Math.Max(0d, forwardSpeed - targetForwardSpeed);
			if (relativeSpeed < MinimumRelativeChargeSpeed)
			{
				return false;
			}

			_lastChargeContactByEntity[target.EntityId] = currentTick;
			var kineticDamage = (int) Math.Ceiling(relativeSpeed * profile.DamageMultiplier);
			var shouldDismount = activeTicks <= profile.DismountMaxDurationTicks
				&& forwardSpeed >= profile.DismountMinimumSpeed;
			var shouldKnockback = activeTicks <= profile.KnockbackMaxDurationTicks
				&& forwardSpeed >= profile.KnockbackMinimumSpeed;

			result = new SpearAttackResult(
				GetDamage() + kineticDamage,
				true,
				shouldDismount,
				shouldKnockback);
			return true;
		}

		public void PlayAttackSound(Player attacker, bool hit)
		{
			attacker.Level.BroadcastSound(
				attacker.GetEyesPosition(),
				hit ? GetHitSound() : GetMissSound(),
				runtimeEntityId: attacker.EntityId);
		}

		public override bool DamageItem(Player player, ItemDamageReason reason, Entity target, Block block)
		{
			if (reason != ItemDamageReason.EntityAttack && reason != ItemDamageReason.ItemUse)
			{
				return false;
			}

			Metadata++;
			return Metadata >= GetMaxUses();
		}

		public override object Clone()
		{
			var clone = (ItemSpearBase) base.Clone();
			clone._chargeStartedTick = -1;
			clone._lastChargeContactByEntity = new Dictionary<long, long>();
			return clone;
		}

		private void ResetCharge()
		{
			_chargeStartedTick = -1;
			_lastChargeContactByEntity.Clear();
		}

		private static double GetForwardSpeed(Player attacker)
		{
			// Player.CurrentSpeed is expressed in blocks per second. Movement validation
			// already maintains it from authoritative input packets.
			return Math.Clamp(attacker.CurrentSpeed, 0d, 100d);
		}

		private static double GetTargetForwardSpeed(Player attacker, Entity target)
		{
			var attackDirection = attacker.KnownPosition.GetHeadDirectionVector().Normalize();
			Vector3 targetMotion;

			if (target is Player targetPlayer)
			{
				targetMotion = targetPlayer.KnownPosition.GetHeadDirectionVector().Normalize()
					* (float) Math.Clamp(targetPlayer.CurrentSpeed, 0d, 100d);
			}
			else
			{
				// Entity velocity is stored in blocks per tick.
				targetMotion = target.Velocity * 20f;
			}

			return Vector3.Dot(attackDirection, targetMotion);
		}

		private SpearCombatProfile GetCombatProfile()
		{
			return ItemMaterial switch
			{
				ItemMaterial.Wood => new SpearCombatProfile(0.65f, 0.70f, 0.75f, 5.00f, 14.00f, 10.00f, 5.10f, 15.00f),
				ItemMaterial.Stone => new SpearCombatProfile(0.75f, 0.82f, 0.70f, 4.50f, 10.00f, 9.00f, 5.10f, 13.75f),
				ItemMaterial.Copper => new SpearCombatProfile(0.85f, 0.82f, 0.65f, 4.00f, 9.00f, 8.25f, 5.10f, 12.50f),
				ItemMaterial.Iron => new SpearCombatProfile(0.95f, 0.95f, 0.60f, 2.50f, 8.00f, 6.75f, 5.10f, 11.25f),
				ItemMaterial.Gold => new SpearCombatProfile(0.95f, 0.70f, 0.70f, 3.50f, 10.00f, 8.50f, 5.10f, 13.75f),
				ItemMaterial.Diamond => new SpearCombatProfile(1.05f, 1.075f, 0.50f, 3.00f, 7.50f, 6.50f, 5.10f, 10.00f),
				ItemMaterial.Netherite => new SpearCombatProfile(1.15f, 1.20f, 0.40f, 2.50f, 7.00f, 5.50f, 5.10f, 8.75f),
				_ => new SpearCombatProfile(0.65f, 0.70f, 0.75f, 5.00f, 14.00f, 10.00f, 5.10f, 15.00f)
			};
		}

		private LevelSoundEventType GetUseSound()
		{
			return ItemMaterial switch
			{
				ItemMaterial.Wood => LevelSoundEventType.WoodenSpearUse,
				ItemMaterial.Stone => LevelSoundEventType.StoneSpearUse,
				ItemMaterial.Copper => LevelSoundEventType.CopperSpearUse,
				ItemMaterial.Iron => LevelSoundEventType.IronSpearUse,
				ItemMaterial.Gold => LevelSoundEventType.GoldenSpearUse,
				ItemMaterial.Diamond => LevelSoundEventType.DiamondSpearUse,
				ItemMaterial.Netherite => LevelSoundEventType.NetheriteSpearUse,
				_ => LevelSoundEventType.SpearUse
			};
		}

		private LevelSoundEventType GetHitSound()
		{
			return ItemMaterial switch
			{
				ItemMaterial.Wood => LevelSoundEventType.WoodenSpearAttackHit,
				ItemMaterial.Stone => LevelSoundEventType.StoneSpearAttackHit,
				ItemMaterial.Copper => LevelSoundEventType.CopperSpearAttackHit,
				ItemMaterial.Iron => LevelSoundEventType.IronSpearAttackHit,
				ItemMaterial.Gold => LevelSoundEventType.GoldenSpearAttackHit,
				ItemMaterial.Diamond => LevelSoundEventType.DiamondSpearAttackHit,
				ItemMaterial.Netherite => LevelSoundEventType.NetheriteSpearAttackHit,
				_ => LevelSoundEventType.SpearAttackHit
			};
		}

		private LevelSoundEventType GetMissSound()
		{
			return ItemMaterial switch
			{
				ItemMaterial.Wood => LevelSoundEventType.WoodenSpearAttackMiss,
				ItemMaterial.Stone => LevelSoundEventType.StoneSpearAttackMiss,
				ItemMaterial.Copper => LevelSoundEventType.CopperSpearAttackMiss,
				ItemMaterial.Iron => LevelSoundEventType.IronSpearAttackMiss,
				ItemMaterial.Gold => LevelSoundEventType.GoldenSpearAttackMiss,
				ItemMaterial.Diamond => LevelSoundEventType.DiamondSpearAttackMiss,
				ItemMaterial.Netherite => LevelSoundEventType.NetheriteSpearAttackMiss,
				_ => LevelSoundEventType.SpearAttackMiss
			};
		}

		private readonly record struct SpearCombatProfile(
			float AttackIntervalSeconds,
			float DamageMultiplier,
			float DelaySeconds,
			float DismountMaxDurationSeconds,
			float DismountMinimumSpeed,
			float KnockbackMaxDurationSeconds,
			float KnockbackMinimumSpeed,
			float DamageMaxDurationSeconds)
		{
			public int DelayTicks => (int) (DelaySeconds * 20f);
			public int DismountMaxDurationTicks => (int) (DismountMaxDurationSeconds * 20f);
			public int KnockbackMaxDurationTicks => (int) (KnockbackMaxDurationSeconds * 20f);
			public int DamageMaxDurationTicks => (int) (DamageMaxDurationSeconds * 20f);
		}
	}
}
