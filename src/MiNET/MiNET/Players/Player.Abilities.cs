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
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using fNbt;
using log4net;
using MiNET.BlockEntities;
using MiNET.Blocks;
using MiNET.Blocks.States;
using MiNET.Crafting;
using MiNET.Effects;
using MiNET.Entities;
using MiNET.Entities.Passive;
using MiNET.Entities.World;
using MiNET.Inventories;
using MiNET.Items;
using MiNET.Net;
using MiNET.Particles;
using MiNET.UI;
using MiNET.Utils;
using MiNET.Utils.Metadata;
using MiNET.Utils.Nbt;
using MiNET.Utils.Skins;
using MiNET.Utils.Vectors;
using MiNET.Worlds;
using Newtonsoft.Json;

namespace MiNET.Players
{
	public partial class Player
	{
		public bool IsWorldImmutable { get; set; }
		public bool IsWorldBuilder { get; set; }
		public bool IsMuted { get; set; }
		public bool IsNoPvp { get; set; }
		public bool IsNoPvm { get; set; }
		public bool IsNoMvp { get; set; }
		public bool IsNoClip { get; set; }
		public bool IsFlying { get; set; }

		public virtual void HandleMcpeAdventureSettings(McpeAdventureSettings message)
		{
			var flags = message.flags;
			IsAutoJump = (flags & 0x20) == 0x20;
			IsFlying = (flags & 0x200) == 0x200;
		}

		public virtual void SendGameRules()
		{
			McpeGameRulesChanged gameRulesChanged = McpeGameRulesChanged.CreateObject();
			gameRulesChanged.rules = Level.GetGameRules();
			SendPacket(gameRulesChanged);
		}

		public virtual void SendAdventureSettings()
		{
			McpeUpdateAdventureSettings settings = McpeUpdateAdventureSettings.CreateObject();
			settings.noPvm = IsNoPvm;
			settings.noMvp = IsNoMvp;
			settings.autoJump = IsAutoJump;
			settings.immutableWorld = IsWorldImmutable;
			settings.showNametags = true;
			SendPacket(settings);
		}

		public virtual void SendAbilities()
		{
			McpeUpdateAbilities packet = McpeUpdateAbilities.CreateObject();
			packet.layers = GetAbilities();
			packet.commandPermissions = (byte) CommandPermission;
			packet.playerPermissions = (byte) PermissionLevel;
			packet.entityUniqueId = (ulong) EntityId;
			SendPacket(packet);
		}

		protected virtual AbilityLayers GetAbilities()
		{
			var abilities = new Dictionary<PlayerAbility, bool>();

			var mayFly = AllowFly || GameMode.AllowsFlying();
			abilities.Add(PlayerAbility.MayFly, mayFly);
			abilities.Add(PlayerAbility.Flying, mayFly && IsFlying);

			abilities.Add(PlayerAbility.NoClip, IsNoClip || IsSpectator || !GameMode.HasCollision());
			abilities.Add(PlayerAbility.Invulnerable, !GameMode.AllowsTakingDamage());
			abilities.Add(PlayerAbility.InstantBuild, GameMode.HasCreativeInventory());

			var mayEditWorld = IsWorldBuilder || GameMode.AllowsEditing();
			abilities.Add(PlayerAbility.Build, mayEditWorld);
			abilities.Add(PlayerAbility.Mine, mayEditWorld);

			var mayInteract = GameMode.AllowsInteraction() || IsSpectator;
			abilities.Add(PlayerAbility.DoorsAndSwitches, mayInteract);
			abilities.Add(PlayerAbility.OpenContainers, mayInteract);
			abilities.Add(PlayerAbility.AttackPlayers, mayInteract);
			abilities.Add(PlayerAbility.AttackMobs, mayInteract);

			abilities.Add(PlayerAbility.OperatorCommands, PermissionLevel == PermissionLevel.Operator);
			abilities.Add(PlayerAbility.Muted, IsMuted);

			var layers = new AbilityLayers()
			{
				new AbilityLayer()
				{
					Type = AbilityLayerType.Base,
					Abilities = abilities,
					FlySpeed = 0.05f,
					VerticalFlySpeed = 1f,
					WalkSpeed = 0.1f
				}
			};

			return layers;
		}

		public PermissionLevel PermissionLevel { get; set; } = PermissionLevel.Operator;

		public int CommandPermission { get; set; } = (int) Net.CommandPermission.Normal;

		public ActionPermissions ActionPermissions { get; set; } = ActionPermissions.Default;

		public bool IsSpectator { get; set; }

		[Wired]
		public void SetSpectator(bool isSpectator)
		{
			IsSpectator = isSpectator;

			SendAbilities();
		}

		public bool IsAutoJump { get; set; }

		[Wired]
		public void SetAutoJump(bool isAutoJump)
		{
			IsAutoJump = isAutoJump;
			SendAdventureSettings();
		}

		public bool AllowFly { get; set; }

		[Wired]
		public void SetAllowFly(bool allowFly)
		{
			AllowFly = allowFly;
			SendAbilities();
		}
	}
}
