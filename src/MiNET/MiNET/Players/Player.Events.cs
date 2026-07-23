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
using MiNET.Entities;
using MiNET.Entities.World;
using MiNET.Items;
using MiNET.Utils.Vectors;
using MiNET.Worlds;

namespace MiNET.Players
{
	public partial class Player
	{
		public event EventHandler<PlayerEventArgs> PlayerJoining;

		protected virtual void OnPlayerJoining(PlayerEventArgs e)
		{
			PlayerJoining?.Invoke(this, e);
		}

		public event EventHandler<PlayerEventArgs> PlayerJoin;

		protected virtual void OnPlayerJoin(PlayerEventArgs e)
		{
			PlayerJoin?.Invoke(this, e);
		}

		public event EventHandler<PlayerEventArgs> LocalPlayerIsInitialized;

		protected virtual void OnLocalPlayerIsInitialized(PlayerEventArgs e)
		{
			LocalPlayerIsInitialized?.Invoke(this, e);
		}

		public event EventHandler<PlayerEventArgs> PlayerLeave;

		protected virtual void OnPlayerLeave(PlayerEventArgs e)
		{
			PlayerLeave?.Invoke(this, e);
		}

		public event EventHandler<PlayerEventArgs> Ticking;

		protected virtual void OnTicking(PlayerEventArgs e)
		{
			Ticking?.Invoke(this, e);
		}

		public event EventHandler<PlayerEventArgs> Ticked;

		protected virtual void OnTicked(PlayerEventArgs e)
		{
			Ticked?.Invoke(this, e);
		}

		public event EventHandler<PlayerChatEventArgs> PlayerChat;

		public event EventHandler<PlayerMoveEventArgs> PlayerMove;

		public event EventHandler<PlayerTeleportEventArgs> PlayerTeleport;

		public event EventHandler<PlayerChangeDimensionEventArgs> PlayerChangeDimension;

		public event EventHandler<PlayerDropItemEventArgs> PlayerDropItem;

		public event EventHandler<PlayerPickupItemEventArgs> PlayerPickupItem;

		public event EventHandler<PlayerEntityInteractEventArgs> PlayerEntityInteract;

		public event EventHandler<PlayerEntityAttackEventArgs> PlayerEntityAttack;

		public event EventHandler<PlayerToggleSprintEventArgs> PlayerToggleSprint;
	}

	public class PlayerEventArgs : EventArgs
	{
		public Player Player { get; }
		public Level Level { get; }

		public PlayerEventArgs(Player player)
		{
			Player = player;
			Level = player?.Level;
		}
	}

	public class PlayerEventCancellable : PlayerEventArgs
	{
		public bool Cancel { get; set; }

		public PlayerEventCancellable(Player player) : base(player)
		{
		}
	}

	public class PlayerChatEventArgs : PlayerEventCancellable
	{
		public string Message { get; set; }

		public PlayerChatEventArgs(Player player, string message) : base(player)
		{
			Message = message;
		}
	}

	public class PlayerMoveEventArgs : PlayerEventCancellable
	{
		public PlayerLocation From { get; }
		public PlayerLocation To { get; set; }
		public bool IsOnGround { get; set; }
		public bool IsFlyingHorizontally { get; set; }

		public PlayerMoveEventArgs(Player player, PlayerLocation from, PlayerLocation to, bool isOnGround, bool isFlyingHorizontally) : base(player)
		{
			From = from;
			To = to;
			IsOnGround = isOnGround;
			IsFlyingHorizontally = isFlyingHorizontally;
		}
	}

	public class PlayerTeleportEventArgs : PlayerMoveEventArgs
	{
		public PlayerTeleportEventArgs(Player player, PlayerLocation from, PlayerLocation to) : base(player, from, to, player?.IsOnGround ?? false, false)
		{
		}
	}

	public class PlayerChangeDimensionEventArgs : PlayerTeleportEventArgs
	{
		public Level FromLevel { get; }
		public Level ToLevel { get; set; }
		public Dimension FromDimension { get; }
		public Dimension ToDimension { get; set; }

		public PlayerChangeDimensionEventArgs(Player player, Level fromLevel, Level toLevel, PlayerLocation from, PlayerLocation to, Dimension fromDimension, Dimension toDimension) : base(player, from, to)
		{
			FromLevel = fromLevel;
			ToLevel = toLevel;
			FromDimension = fromDimension;
			ToDimension = toDimension;
		}
	}

	public class PlayerDropItemEventArgs : PlayerEventCancellable
	{
		public Item Item { get; set; }
		public PlayerLocation Position { get; set; }

		public PlayerDropItemEventArgs(Player player, Item item, PlayerLocation position) : base(player)
		{
			Item = item;
			Position = position;
		}
	}

	public class PlayerPickupItemEventArgs : PlayerEventCancellable
	{
		public ItemEntity ItemEntity { get; }
		public Item Item => ItemEntity?.Item;

		public PlayerPickupItemEventArgs(Player player, ItemEntity itemEntity) : base(player)
		{
			ItemEntity = itemEntity;
		}
	}

	public class PlayerEntityInteractEventArgs : PlayerEventCancellable
	{
		public Entity Target { get; }
		public Item ItemInHand { get; }
		public int ActionId { get; }

		public PlayerEntityInteractEventArgs(Player player, Entity target, Item itemInHand, int actionId) : base(player)
		{
			Target = target;
			ItemInHand = itemInHand;
			ActionId = actionId;
		}
	}

	public class PlayerEntityAttackEventArgs : PlayerEntityInteractEventArgs
	{
		public PlayerEntityAttackEventArgs(Player player, Entity target, Item itemInHand, int actionId) : base(player, target, itemInHand, actionId)
		{
		}
	}

	public class PlayerToggleSprintEventArgs : PlayerEventCancellable
	{
		public bool IsSprinting { get; set; }

		public PlayerToggleSprintEventArgs(Player player, bool isSprinting) : base(player)
		{
			IsSprinting = isSprinting;
		}
	}
}
