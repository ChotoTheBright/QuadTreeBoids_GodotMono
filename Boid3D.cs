﻿using Godot;
using System;
using System.Diagnostics;
internal class Boid3D : Vertex3D
{
    public Boid3D(Vector3 position) : base(position) { }

    public Vector3 acc { get; private set; } // acceleration
    public Vector3 vel { get; private set; } // velocity

    static float closeRangeSq = 15f * 15f; // range to avoid
    static float largeRange = 30f; // range to get close to

    static float velocityAlignment = 0.05f;
    static float positionAlignment = 0.05f;
    static float pathAlignment = 0.1f;
    static float avoidStrength = 0.4f;
    static float randomStrength = 0.1f;

    static float velocityDecay = 0.985f;
    static float accStrength = 0.2f;

    static float maxVel = 5.0f;

    static float margin = 50.0f;
    static float criticalMargin = 5.0f;

    static Random rng = new Random();

    public void Update(Octree<Boid3D> boids, Path path)
    {
        // get surrounding boids
        var flock = boids.Query(Position, largeRange);

        // compute averages of 
        // - velocity
        // - position
        // - position of *very* close boids (that are to be avoided)
        (Vector3 vel, Vector3 pos, Vector3 closePos, int close) flockAvg = (
            new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 0), 0);
        for (int i = 0; i < flock.Count; i++)
        {
            var other = flock[i];

            flockAvg.vel += other.vel;
            flockAvg.pos += other.Position;
            if (Position.DistanceSquaredTo(other.Position) < closeRangeSq)
            {
                flockAvg.closePos += other.Position;
                flockAvg.close++;
            }
        }

        // divide by count to get average
        flockAvg = (
            flockAvg.vel / flock.Count,
            flockAvg.pos / flock.Count,
            flockAvg.close > 0 ? flockAvg.closePos / flockAvg.close : flockAvg.closePos,
            flockAvg.close);

        // find the closest point on curve
        var pathvel = path.Curve.InterpolateBaked(path.Curve.GetClosestOffset(Position) + 10, true);

        // add all forces together
        acc =
            (flockAvg.vel - vel).Normalized() * velocityAlignment +
            (flockAvg.pos - Position).Normalized() * positionAlignment -
            (flockAvg.closePos - Position).Normalized() * avoidStrength +
            (pathvel - Position).Normalized() * pathAlignment +
            (new Vector3(rng.Next(-100, 100), rng.Next(-100, 100), rng.Next(-100, 100))).Normalized() * randomStrength;

        // if boid is too close to edge, steer away from it
        // this assumes the quadtree is positioned at 0, 0, 0
        if (Position.x < margin)
            acc = new Vector3(Math.Abs(acc.x) * 2f, acc.y, acc.z);
        if (Position.y < margin)
            acc = new Vector3(acc.x, Math.Abs(acc.y) * 2f, acc.z);
        if (Position.z < margin)
            acc = new Vector3(acc.x, acc.y, Math.Abs(acc.z) * 2f);
        if (Position.x > boids.boundary.Size.x - margin)
            acc = new Vector3(-Math.Abs(acc.x) * 2f, acc.y, acc.z);
        if (Position.y > boids.boundary.Size.y - margin)
            acc = new Vector3(acc.x, -Math.Abs(acc.y) * 2f, acc.z);
        if (Position.z > boids.boundary.Size.z - margin)
            acc = new Vector3(acc.x, acc.y, -Math.Abs(acc.z) * 2f);

        // first part of euler integration (updating velocity due to accel)
        acc = acc.Normalized() * accStrength;
        vel *= velocityDecay;
        vel += acc;
        vel = vel.Constrain(-maxVel, maxVel, -maxVel, maxVel, -maxVel, maxVel);

        // if boid is *very* close to edge, move away from it 
        // this assumes (again) the quadtree is positioned at 0, 0, 0
        if (Position.x < criticalMargin)
            vel = new Vector3(Math.Abs(vel.x), vel.y, vel.z);
        if (Position.y < criticalMargin)
            vel = new Vector3(vel.x, Math.Abs(vel.y), vel.z);
        if (Position.z < criticalMargin)
            vel = new Vector3(vel.x, vel.y, Math.Abs(vel.z));
        if (Position.x > boids.boundary.Size.x - criticalMargin)
            vel = new Vector3(-Math.Abs(vel.x), vel.y, vel.z);
        if (Position.y > boids.boundary.Size.y - criticalMargin)
            vel = new Vector3(vel.x, -Math.Abs(vel.y), vel.z);
        if (Position.z > boids.boundary.Size.z - criticalMargin)
            vel = new Vector3(vel.x, vel.y, -Math.Abs(vel.z));

        // second part of euler integration (updating position due to velocity)
        Position += vel + 0.5f * new Vector3(acc.x * acc.x, acc.y * acc.y, acc.z * acc.z);
        Position = Position.Constrain(0, boids.boundary.Size.x, 0, boids.boundary.Size.y, 0, boids.boundary.Size.z);
    }
}
