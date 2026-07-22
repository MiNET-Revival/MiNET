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
	public partial class Level : IBlockAccess
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(Level));

		public static readonly BlockCoordinates Up = new BlockCoordinates(0, 1, 0);
		public static readonly BlockCoordinates Down = new BlockCoordinates(0, -1, 0);
		public static readonly BlockCoordinates South = new BlockCoordinates(0, 0, 1);
		public static readonly BlockCoordinates North = new BlockCoordinates(0, 0, -1);
		public static readonly BlockCoordinates East = new BlockCoordinates(1, 0, 0);
		public static readonly BlockCoordinates West = new BlockCoordinates(-1, 0, 0);

		public IWorldProvider WorldProvider { get; set; }

		private int _worldDayCycleTime = 24000;

		public PlayerLocation SpawnPoint { get; set; } = null;

		public ConcurrentDictionary<long, Player> Players { get; private set; } = new ConcurrentDictionary<long, Player>();

//TODO: Need to protect this, not threadsafe
		public ConcurrentDictionary<long, Entity> Entities { get; private set; } = new ConcurrentDictionary<long, Entity>();

//TODO: Need to protect this, not threadsafe
		public List<BlockEntity> BlockEntities { get; private set; } = new List<BlockEntity>();

//TODO: Need to protect this, not threadsafe
		public ConcurrentDictionary<BlockCoordinates, long> BlockWithTicks { get; private set; } = new ConcurrentDictionary<BlockCoordinates, long>();

//TODO: Need to protect this, not threadsafe
		public string LevelId { get; private set; }

		public string LevelName { get; private set; }
		public Dimension Dimension { get; set; } = Dimension.Overworld;

		public GameMode GameMode { get; private set; }
		public bool IsSurvival => GameMode == GameMode.Survival;
		public bool HaveDownfall { get; set; }
		public Difficulty Difficulty { get; set; }
		public bool AutoSmelt { get; set; } = false;
		public long WorldTime { get; set; }
		public long CurrentWorldCycleTime { get; private set; }
		public long TickTime { get; set; }
		public int SkylightSubtracted { get; set; }
		public long StartTimeInTicks { get; private set; }
		public bool EnableBlockTicking { get; set; } = false;
		public bool EnableChunkTicking { get; set; } = false;

		public bool AllowBuild { get; set; } = true;
		public bool AllowBreak { get; set; } = true;

		public EntityManager EntityManager { get; protected set; }
		public EntitySpawnManager EntitySpawnManager { get; protected set; }

		public int ViewDistance { get; set; }

		public Random Random { get; private set; }

		public int SaveInterval { get; set; } = 300;
		public int UnloadInterval { get; set; } = -1;

		public Level(LevelManager levelManager, string levelId, IWorldProvider worldProvider, EntityManager entityManager, GameMode gameMode = GameMode.Survival, Difficulty difficulty = Difficulty.Normal, int viewDistance = 11)
		{
			Random = new Random();

			LevelManager = levelManager;
			EntityManager = entityManager;
			EntitySpawnManager = new EntitySpawnManager(this);
			LevelId = levelId;
			GameMode = gameMode;
			Difficulty = difficulty;
			ViewDistance = viewDistance;
			WorldProvider = worldProvider;
		}

		public LevelManager LevelManager { get; }
		public Level NetherLevel { get; set; }
		public Level TheEndLevel { get; set; }
		public Level OverworldLevel { get; set; }
	}
}