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
		public bool DrowningDamage { get; set; } = true;
		public bool CommandblockOutput { get; set; } = true;
		public bool DoTiledrops { get; set; } = true;
		public bool DoMobloot { get; set; } = true;
		public bool KeepInventory { get; set; } = true;
		public bool DoDaylightcycle { get; set; } = true;
		public bool DoMobspawning { get; set; } = true;
		public bool DoEntitydrops { get; set; } = true;
		public bool DoFiretick { get; set; } = true;
		public bool DoWeathercycle { get; set; } = true;
		public bool Pvp { get; set; } = true;
		public bool Falldamage { get; set; } = true;
		public bool Firedamage { get; set; } = true;
		public bool Mobgriefing { get; set; } = true;
		public bool ShowCoordinates { get; set; } = true;
		public bool NaturalRegeneration { get; set; } = true;
		public bool TntExplodes { get; set; } = true;
		public bool SendCommandfeedback { get; set; } = true;
		public int RandomTickSpeed { get; set; } = 3;

		public virtual void BroadcastGameRules()
		{
			McpeGameRulesChanged gameRulesChanged = McpeGameRulesChanged.CreateObject();
			gameRulesChanged.rules = GetGameRules();
			RelayBroadcast(gameRulesChanged);
		}

		public void SetGameRule(GameRulesEnum rule, bool value)
		{
			switch (rule)
			{
				case GameRulesEnum.DrowningDamage:
					DrowningDamage = value;
					break;
				case GameRulesEnum.CommandblockOutput:
					CommandblockOutput = value;
					break;
				case GameRulesEnum.DoTiledrops:
					DoTiledrops = value;
					break;
				case GameRulesEnum.DoMobloot:
					DoMobloot = value;
					break;
				case GameRulesEnum.KeepInventory:
					KeepInventory = value;
					break;
				case GameRulesEnum.DoDaylightcycle:
					DoDaylightcycle = value;
					break;
				case GameRulesEnum.DoMobspawning:
					DoMobspawning = value;
					break;
				case GameRulesEnum.DoEntitydrops:
					DoEntitydrops = value;
					break;
				case GameRulesEnum.DoFiretick:
					DoFiretick = value;
					break;
				case GameRulesEnum.DoWeathercycle:
					DoWeathercycle = value;
					break;
				case GameRulesEnum.Pvp:
					Pvp = value;
					break;
				case GameRulesEnum.Falldamage:
					Falldamage = value;
					break;
				case GameRulesEnum.Firedamage:
					Firedamage = value;
					break;
				case GameRulesEnum.Mobgriefing:
					Mobgriefing = value;
					break;
				case GameRulesEnum.ShowCoordinates:
					ShowCoordinates = value;
					break;
				case GameRulesEnum.NaturalRegeneration:
					NaturalRegeneration = value;
					break;
				case GameRulesEnum.TntExplodes:
					TntExplodes = value;
					break;
				case GameRulesEnum.SendCommandfeedback:
					SendCommandfeedback = value;
					break;
			}
		}

		public void SetGameRule(GameRulesEnum rule, int value)
		{
			switch (rule)
			{
				case GameRulesEnum.DrowningDamage:
					RandomTickSpeed = value;
					break;
			}
		}


		public bool GetGameRule(GameRulesEnum rule)
		{
			switch (rule)
			{
				case GameRulesEnum.DrowningDamage:
					return DrowningDamage;
				case GameRulesEnum.CommandblockOutput:
					return CommandblockOutput;
				case GameRulesEnum.DoTiledrops:
					return DoTiledrops;
				case GameRulesEnum.DoMobloot:
					return DoMobloot;
				case GameRulesEnum.KeepInventory:
					return KeepInventory;
				case GameRulesEnum.DoDaylightcycle:
					return DoDaylightcycle;
				case GameRulesEnum.DoMobspawning:
					return DoMobspawning;
				case GameRulesEnum.DoEntitydrops:
					return DoEntitydrops;
				case GameRulesEnum.DoFiretick:
					return DoFiretick;
				case GameRulesEnum.DoWeathercycle:
					return DoWeathercycle;
				case GameRulesEnum.Pvp:
					return Pvp;
				case GameRulesEnum.Falldamage:
					return Falldamage;
				case GameRulesEnum.Firedamage:
					return Firedamage;
				case GameRulesEnum.Mobgriefing:
					return Mobgriefing;
				case GameRulesEnum.ShowCoordinates:
					return ShowCoordinates;
				case GameRulesEnum.NaturalRegeneration:
					return NaturalRegeneration;
				case GameRulesEnum.TntExplodes:
					return TntExplodes;
				case GameRulesEnum.SendCommandfeedback:
					return SendCommandfeedback;
			}

			return false;
		}

		public virtual GameRules GetGameRules()
		{
			GameRules rules = new GameRules();
			rules.Add(new GameRule<bool>(GameRulesEnum.DrowningDamage, DrowningDamage));
			rules.Add(new GameRule<bool>(GameRulesEnum.CommandblockOutput, CommandblockOutput));
			rules.Add(new GameRule<bool>(GameRulesEnum.DoTiledrops, DoTiledrops));
			rules.Add(new GameRule<bool>(GameRulesEnum.DoMobloot, DoMobloot));
			rules.Add(new GameRule<bool>(GameRulesEnum.KeepInventory, KeepInventory));
			rules.Add(new GameRule<bool>(GameRulesEnum.DoDaylightcycle, DoDaylightcycle));
			rules.Add(new GameRule<bool>(GameRulesEnum.DoMobspawning, DoMobspawning));
			rules.Add(new GameRule<bool>(GameRulesEnum.DoEntitydrops, DoEntitydrops));
			rules.Add(new GameRule<bool>(GameRulesEnum.DoFiretick, DoFiretick));
			rules.Add(new GameRule<bool>(GameRulesEnum.DoWeathercycle, DoWeathercycle));
			rules.Add(new GameRule<bool>(GameRulesEnum.Pvp, Pvp));
			rules.Add(new GameRule<bool>(GameRulesEnum.Falldamage, Falldamage));
			rules.Add(new GameRule<bool>(GameRulesEnum.Firedamage, Firedamage));
			rules.Add(new GameRule<bool>(GameRulesEnum.Mobgriefing, Mobgriefing));
			rules.Add(new GameRule<bool>(GameRulesEnum.ShowCoordinates, ShowCoordinates));
			rules.Add(new GameRule<bool>(GameRulesEnum.NaturalRegeneration, NaturalRegeneration));
			rules.Add(new GameRule<bool>(GameRulesEnum.TntExplodes, TntExplodes));
			rules.Add(new GameRule<bool>(GameRulesEnum.SendCommandfeedback, SendCommandfeedback));
			rules.Add(new GameRule<bool>(GameRulesEnum.ExperimentalGameplay, true));
			return rules;
		}
	}
}