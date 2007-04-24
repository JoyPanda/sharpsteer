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
	public class SimpleVehicle : SteerLibrary
	{
		// give each vehicle a unique number
		public readonly int SerialNumber;
		static int serialNumberCounter = 0;

		float mass;       // Mass (defaults to unity so acceleration=force)

		float radius;     // size of bounding sphere, for obstacle avoidance, etc.

		float speed;      // speed along Forward direction.  Because local space
		// is velocity-aligned, velocity = Forward * Speed

		float maxForce;   // the maximum steering force this vehicle can apply
		// (steering force is clipped to this magnitude)

		float maxSpeed;   // the maximum speed this vehicle is allowed to move
		// (velocity is clipped to this magnitude)

		float curvature;
		Vec3 lastForward;
		Vec3 lastPosition;
		Vec3 smoothedPosition;
		float smoothedCurvature;
		Vec3 smoothedAcceleration;

		// constructor
		public SimpleVehicle()
		{
			// set inital state
			Reset();

			// maintain unique serial numbers
			SerialNumber = serialNumberCounter++;
		}

		// reset vehicle state
		public override void Reset()
		{
			// reset LocalSpace state
			ResetLocalSpace();

			// reset SteerLibraryMixin state
			// (XXX this seems really fragile, needs to be redesigned XXX)
			base.Reset();

			Mass = 1;          // Mass (defaults to 1 so acceleration=force)
			Speed = 0;         // speed along Forward direction.

			Radius = 0.5f;     // size of bounding sphere

			MaxForce = 0.1f;   // steering force is clipped to this magnitude
			MaxSpeed = 1.0f;   // velocity is clipped to this magnitude

			// reset bookkeeping to do running averages of these quanities
			ResetSmoothedPosition();
			ResetSmoothedCurvature();
			ResetSmoothedAcceleration();
		}

		// get/set Mass
		public override float Mass
		{
			get { return mass; }
			set { mass = value; }
		}

		// get velocity of vehicle
		public override Vec3 Velocity
		{
			get { return Forward * speed; }
		}

		// get/set speed of vehicle  (may be faster than taking mag of velocity)
		public override float Speed
		{
			get { return speed; }
			set { speed = value; }
		}

		// size of bounding sphere, for obstacle avoidance, etc.
		public override float Radius
		{
			get { return radius; }
			set { radius = value; }
		}

		// get/set maxForce
		public override float MaxForce
		{
			get { return maxForce; }
			set { maxForce = value; }
		}

		// get/set maxSpeed
		public override float MaxSpeed
		{
			get { return maxSpeed; }
			set { maxSpeed = value; }
		}

		// apply a given steering force to our momentum,
		// adjusting our orientation to maintain velocity-alignment.
		public void ApplySteeringForce(Vec3 force, float elapsedTime)
		{
			Vec3 adjustedForce = AdjustRawSteeringForce(force, elapsedTime);

			// enforce limit on magnitude of steering force
			Vec3 clippedForce = adjustedForce.TruncateLength(MaxForce);

			// compute acceleration and velocity
			Vec3 newAcceleration = (clippedForce / Mass);
			Vec3 newVelocity = Velocity;

			// damp out abrupt changes and oscillations in steering acceleration
			// (rate is proportional to time step, then clipped into useful range)
			if (elapsedTime > 0)
			{
				float smoothRate = Utilities.Clip(9 * elapsedTime, 0.15f, 0.4f);
				Utilities.BlendIntoAccumulator(smoothRate, newAcceleration, ref smoothedAcceleration);
			}

			// Euler integrate (per frame) acceleration into velocity
			newVelocity += smoothedAcceleration * elapsedTime;

			// enforce speed limit
			newVelocity = newVelocity.TruncateLength(MaxSpeed);

			// update Speed
			Speed = (newVelocity.Length());

			// Euler integrate (per frame) velocity into position
			Position = (Position + (newVelocity * elapsedTime));

			// regenerate local space (by default: align vehicle's forward axis with
			// new velocity, but this behavior may be overridden by derived classes.)
			RegenerateLocalSpace(newVelocity, elapsedTime);

			// maintain path curvature information
			MeasurePathCurvature(elapsedTime);

			// running average of recent positions
			Utilities.BlendIntoAccumulator(elapsedTime * 0.06f, // QQQ
								  Position,
								  ref smoothedPosition);
		}

		// the default version: keep FORWARD parallel to velocity, change
		// UP as little as possible.
		public virtual void RegenerateLocalSpace(Vec3 newVelocity, float elapsedTime)
		{
			// adjust orthonormal basis vectors to be aligned with new velocity
			if (Speed > 0)
			{
				RegenerateOrthonormalBasisUF(newVelocity / Speed);
			}
		}

		// alternate version: keep FORWARD parallel to velocity, adjust UP
		// according to a no-basis-in-reality "banking" behavior, something
		// like what birds and airplanes do.  (XXX experimental cwr 6-5-03)
		public void RegenerateLocalSpaceForBanking(Vec3 newVelocity, float elapsedTime)
		{
			// the length of this global-upward-pointing vector controls the vehicle's
			// tendency to right itself as it is rolled over from turning acceleration
			Vec3 globalUp = new Vec3(0, 0.2f, 0);

			// acceleration points toward the center of local path curvature, the
			// length determines how much the vehicle will roll while turning
			Vec3 accelUp = smoothedAcceleration * 0.05f;

			// combined banking, sum of UP due to turning and global UP
			Vec3 bankUp = accelUp + globalUp;

			// blend bankUp into vehicle's UP basis vector
			float smoothRate = elapsedTime * 3;
			Vec3 tempUp = Up;
			Utilities.BlendIntoAccumulator(smoothRate, bankUp, ref tempUp);
			Up = tempUp.Normalize();

			AnnotationLine(Position, Position + (globalUp * 4), Color.White);  // XXX
			AnnotationLine(Position, Position + (bankUp * 4), Color.Orange); // XXX
			AnnotationLine(Position, Position + (accelUp * 4), Color.Red);    // XXX
			AnnotationLine(Position, Position + (Up * 1), Color.Yellow); // XXX

			// adjust orthonormal basis vectors to be aligned with new velocity
			if (Speed > 0) RegenerateOrthonormalBasisUF(newVelocity / Speed);
		}

		// adjust the steering force passed to applySteeringForce.
		// allows a specific vehicle class to redefine this adjustment.
		// default is to disallow backward-facing steering at low speed.
		// xxx experimental 8-20-02
		public virtual Vec3 AdjustRawSteeringForce(Vec3 force, float deltaTime)
		{
			float maxAdjustedSpeed = 0.2f * MaxSpeed;

			if ((Speed > maxAdjustedSpeed) || (force == Vec3.Zero))
			{
				return force;
			}
			else
			{
				float range = Speed / maxAdjustedSpeed;
				float cosine = Utilities.Interpolate((float)Math.Pow(range, 20), 1.0f, -1.0f);
				return Vec3.LimitMaxDeviationAngle(force, cosine, Forward);
			}
		}


		// apply a given braking force (for a given dt) to our momentum.
		// xxx experimental 9-6-02
		public void ApplyBrakingForce(float rate, float deltaTime)
		{
			float rawBraking = Speed * rate;
			float clipBraking = ((rawBraking < MaxForce) ? rawBraking : MaxForce);
			Speed = (Speed - (clipBraking * deltaTime));
		}

		// predict position of this vehicle at some time in the future
		// (assumes velocity remains constant)
		public override Vec3 PredictFuturePosition(float predictionTime)
		{
			return Position + (Velocity * predictionTime);
		}

		// get instantaneous curvature (since last update)
		public float Curvature
		{
			get { return curvature; }
		}

		// get/reset smoothedCurvature, smoothedAcceleration and smoothedPosition
		public float SmoothedCurvature
		{
			get { return smoothedCurvature; }
		}
		public float ResetSmoothedCurvature()
		{
			return ResetSmoothedCurvature(0);
		}
		public float ResetSmoothedCurvature(float value)
		{
			lastForward = Vec3.Zero;
			lastPosition = Vec3.Zero;
			return smoothedCurvature = curvature = value;
		}

		public Vec3 SmoothedAcceleration
		{
			get { return smoothedAcceleration; }
		}
		public Vec3 ResetSmoothedAcceleration()
		{
			return ResetSmoothedAcceleration(Vec3.Zero);
		}
		public Vec3 ResetSmoothedAcceleration(Vec3 value)
		{
			return smoothedAcceleration = value;
		}

		public Vec3 SmoothedPosition
		{
			get { return smoothedPosition; }
		}
		public Vec3 ResetSmoothedPosition()
		{
			return ResetSmoothedPosition(Vec3.Zero);
		}
		public Vec3 ResetSmoothedPosition(Vec3 value)
		{
			return smoothedPosition = value;
		}

		// draw lines from vehicle's position showing its velocity and acceleration
		public void AnnotationVelocityAcceleration(float maxLengthA, float maxLengthV)
		{
			float desat = 0.4f;
			float aScale = maxLengthA / MaxForce;
			float vScale = maxLengthV / MaxSpeed;
			Vec3 p = Position;
			Color aColor = new Color(new Vector3(desat, desat, 1)); // bluish
			Color vColor = new Color(new Vector3(1, desat, 1)); // pinkish

			AnnotationLine(p, p + (Velocity * vScale), vColor);
			AnnotationLine(p, p + (smoothedAcceleration * aScale), aColor);
		}
		public void AnnotationVelocityAcceleration(float maxLength)
		{
			AnnotationVelocityAcceleration(maxLength, maxLength);
		}
		public void AnnotationVelocityAcceleration()
		{
			AnnotationVelocityAcceleration(3, 3);
		}

		// set a random "2D" heading: set local Up to global Y, then effectively
		// rotate about it by a random angle (pick random forward, derive side).
		public void RandomizeHeadingOnXZPlane()
		{
			Up = Vec3.Up;
			Forward = Vec3.RandomUnitVectorOnXZPlane();
			Side = LocalRotateForwardToSide(Forward);
		}

		// measure path curvature (1/turning-radius), maintain smoothed version
		void MeasurePathCurvature(float elapsedTime)
		{
			if (elapsedTime > 0)
			{
				Vec3 dP = lastPosition - Position;
				Vec3 dF = (lastForward - Forward) / dP.Length();
				Vec3 lateral = dF.PerpendicularComponent(Forward);
				float sign = (lateral.Dot(Side) < 0) ? 1.0f : -1.0f;
				curvature = lateral.Length() * sign;
				Utilities.BlendIntoAccumulator(elapsedTime * 4.0f, curvature, ref smoothedCurvature);
				lastForward = Forward;
				lastPosition = Position;
			}
		}
	}
}
