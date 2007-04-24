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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Bnoerj.AI.Steering
{
	/// <summary>
	/// Pathway: a pure virtual base class for an abstract pathway in space, as for
	/// example would be used in path following.
	/// </summary>
	public abstract class Pathway
	{
		// Given an arbitrary point ("A"), returns the nearest point ("P") on
		// this path.  Also returns, via output arguments, the path tangent at
		// P and a measure of how far A is outside the Pathway's "tube".  Note
		// that a negative distance indicates A is inside the Pathway.
		//FIXME: change the out's to a returned struct
		public abstract Vec3 MapPointToPath(Vec3 point, out Vec3 tangent, out float outside);

		// given a distance along the path, convert it to a point on the path
		public abstract Vec3 MapPathDistanceToPoint(float pathDistance);

		// Given an arbitrary point, convert it to a distance along the path.
		public abstract float MapPointToPathDistance(Vec3 point);

		// is the given point inside the path tube?
		public bool IsInsidePath(Vec3 point)
		{
			float outside;
			Vec3 tangent;
			MapPointToPath(point, out tangent, out outside);
			return outside < 0;
		}

		// how far outside path tube is the given point?  (negative is inside)
		public float HowFarOutsidePath(Vec3 point)
		{
			float outside;
			Vec3 tangent;
			MapPointToPath(point, out tangent, out outside);
			return outside;
		}
	}
}
