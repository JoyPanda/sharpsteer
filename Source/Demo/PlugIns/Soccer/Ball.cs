// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using System;
using System.Collections.Generic;
using System.Text;
using Bnoerj.AI.Steering;
using Microsoft.Xna.Framework.Graphics;

namespace Bnoerj.SharpSteer.Soccer
{
	public class Ball : SimpleVehicle
	{
		public Ball(AABBox bbox)
		{
			m_bbox = bbox;
			Reset();
		}

		// reset state
		public override void Reset()
		{
			base.Reset(); // reset the vehicle 
			Speed = 0.0f;         // speed along Forward direction.
			MaxForce = 9.0f;      // steering force is clipped to this magnitude
			MaxSpeed = 9.0f;         // velocity is clipped to this magnitude

			SetPosition(0, 0, 0);
			ClearTrailHistory();    // prevent long streaks due to teleportation 
			SetTrailParameters(100, 6000);
		}

		// per frame simulation update
		public void Update(float currentTime, float elapsedTime)
		{
			ApplyBrakingForce(1.5f, elapsedTime);
			ApplySteeringForce(Velocity, elapsedTime);
			// are we now outside the field?
			if (!m_bbox.IsInsideX(Position))
			{
				Vec3 d = Velocity;
				RegenerateOrthonormalBasis(new Vec3(-d.X, d.Y, d.Z));
				ApplySteeringForce(Velocity, elapsedTime);
			}
			if (!m_bbox.IsInsideZ(Position))
			{
				Vec3 d = Velocity;
				RegenerateOrthonormalBasis(new Vec3(d.X, d.Y, -d.Z));
				ApplySteeringForce(Velocity, elapsedTime);
			}
			RecordTrailVertex(currentTime, Position);
		}

		public void Kick(Vec3 dir, float elapsedTime)
		{
			Speed = (dir.Length());
			RegenerateOrthonormalBasis(dir);
		}

		// draw this character/vehicle into the scene
		public void Draw()
		{
			Drawing.DrawBasic2dCircularVehicle(this, Color.Green);
			DrawTrail();
		}

		AABBox m_bbox;
	}
}
