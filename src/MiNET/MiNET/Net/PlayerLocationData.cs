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
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2026 Niclas Olofsson.
// All Rights Reserved.
#endregion

using System;
using System.Numerics;

namespace MiNET.Net
{
	public enum PlayerLocationType
	{
		Coordinates = 0,
		Hide = 1
	}

	public class PlayerLocationData : IPacketDataObject
	{
		public PlayerLocationType Type { get; set; }
		public long EntityUniqueId { get; set; }
		public Vector3? Position { get; set; }

		public static PlayerLocationData Coordinates(long entityUniqueId, Vector3 position)
		{
			return new PlayerLocationData
			{
				Type = PlayerLocationType.Coordinates,
				EntityUniqueId = entityUniqueId,
				Position = position
			};
		}

		public static PlayerLocationData Hide(long entityUniqueId)
		{
			return new PlayerLocationData
			{
				Type = PlayerLocationType.Hide,
				EntityUniqueId = entityUniqueId
			};
		}

		public void Write(Packet packet)
		{
			packet.Write((int) Type);
			packet.WriteSignedVarLong(EntityUniqueId);

			if (Type == PlayerLocationType.Coordinates)
			{
				if (!Position.HasValue)
				{
					throw new InvalidOperationException("PlayerLocationData Coordinates requires a position.");
				}

				packet.Write(Position.Value);
			}
		}

		public static PlayerLocationData Read(Packet packet)
		{
			var location = new PlayerLocationData
			{
				Type = (PlayerLocationType) packet.ReadInt(),
				EntityUniqueId = packet.ReadSignedVarLong()
			};

			if (location.Type == PlayerLocationType.Coordinates)
			{
				location.Position = packet.ReadVector3();
			}

			return location;
		}
	}
}
