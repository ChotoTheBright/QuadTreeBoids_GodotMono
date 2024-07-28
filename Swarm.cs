using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

public class Swarm : Node2D
{
    [Export(PropertyHint.Range, "0,5000,1")]
    int amount = 500;

    [Export]
    Texture boidTexture;

    [Export]
    Texture foodTexture;

    [Export]
    NodePath rectPath;
    ColorRect rect;
    Rectangle boundary;

    [Export]
    NodePath colliderPath;
    Vector2[] colliderPolygon;
    Transform colliderTransform;

    [Export]
    NodePath pathPath;
    Path2D path;

    [Export]
    ShaderMaterial material;

    QuadTree<Boid> boids;
    List<Boid> boidList             = new List<Boid>();
    List<Sprite> spriteList         = new List<Sprite>();
    List<(Vec2 pos, int t)> food    = new List<(Vec2, int)>();
    List<Sprite> foodSpriteList     = new List<Sprite>();

    Vec2 pos;
    Vec2 size;

    public override void _Ready()
    {
        // get the path the boids should swarm towards
        if(pathPath != null)
        {
            path = GetNode<Path2D>(pathPath);
            var pathPoints = path.Curve.GetBakedPoints();

            for (int i = 0; i < pathPoints.Length; i++)
                pathPoints[i] =  path.ToGlobal(pathPoints[i]);

            path = new Path2D();
            for (int i = 0; i < pathPoints.Length; i++)
                path.Curve.AddPoint(pathPoints[i]); 
        }
            

        if(colliderPath != null)
        {
            Debug.Print(colliderPath.ToString());
            var c = GetNode<Path2D>(colliderPath);
            if(c != null)
            {
                colliderPolygon = c.Curve.GetBakedPoints();

                for(int i = 0; i < colliderPolygon.Length; i++)
                    colliderPolygon[i] = c.ToGlobal(colliderPolygon[i]);
            }
        }

        // get the rect defining the position and size of the area where the BOIDs live
        rect = GetNode<ColorRect>(rectPath);
        pos = new Vec2(rect.RectGlobalPosition.x, rect.RectGlobalPosition.y);
        size = new Vec2(rect.RectSize.x, rect.RectSize.y);

        // keep the bounding rect because we need to rebuild the quadtree a buncha times
        boundary = new Rectangle(pos + size * 0.5f, size);

        // setup spatial data structure
        boids = new QuadTree<Boid>(boundary);

        // spawn some random boids
        var rng = new Random();
        for(int i = 0; i < amount; i++)
        {
            var b = new Boid(
                rng.Next((int)pos.x, (int)(pos.x + size.x)),
                rng.Next((int)pos.y, (int)(pos.y + size.y)));

            if (colliderPolygon != null && Geometry.IsPointInPolygon(b.ToVector2(), colliderPolygon))
                continue;

            boids.Insert(b);
            boidList.Add(b);

            // make some sprites to display the boids
            var s = new Sprite();
            s.Texture = boidTexture;
            if (material != null)
                s.Material = material;
            
            s.Position = b.ToVector2();
            spriteList.Add(s);

            // increase z index so boids get drawn above food
            s.ZIndex++;
            AddChild(s);
        }
    }

    public override void _PhysicsProcess(float delta)
    {
        // update and rebuilding needs two loops
        // one for updating, which uses the current quad-tree for efficient searching of the space
        // one for rebuilding the tree with the new positions and updating sprites

        var threadSafeBag = new ConcurrentBag<Boid>();
        Parallel.For(0, boidList.Count, () => new List<Boid>(), 
        (i, _, localList) =>
        {
            var boid = boidList[i];
            boid.Update(boids, path, food, colliderPolygon);
            localList.Add(boid);
            return localList;
        },
        (localList) =>
        {
            foreach (var boid in localList)
                threadSafeBag.Add(boid); // Aggregate all boids that have been updated
        });


        boidList.Clear();
        boidList.AddRange(threadSafeBag);

        // rebuild the quad-tree every frame (yes, this is nessecary)
        boids = new QuadTree<Boid>(boundary);
        for (int i = 0; i < boidList.Count; i++)
        {
            boids.Insert(boidList[i]);

            // update sprite positions and rotations
            spriteList[i].Position = boidList[i].ToVector2();
            spriteList[i].Rotation = boidList[i].vel.Angle();
        }

        // remove foob that has been monched on enough
        for (int i = food.Count - 1; i >= 0; i--)
        {
            food[i] = (food[i].pos, food[i].t + 1);
            if (food[i].t > 3000)
            {
                food.RemoveAt(i);
                RemoveChild(foodSpriteList[i]);
                foodSpriteList.RemoveAt(i);
            }
        }
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventMouseButton btnEvent)
            if (btnEvent.Pressed && btnEvent.ButtonIndex == (int)ButtonList.Left)
                AddFood(btnEvent.Position);

        base._UnhandledInput(input);
    }

    private void AddFood(Vector2 pos)
    {
        if (pos.x < this.pos.x || pos.y < this.pos.y ||
            pos.x > this.pos.x + this.size.x || pos.y > this.pos.y + this.size.y)
            return;
        food.Add((pos.ToVec2(), 0));
        var s = new Sprite();
        s.Texture = foodTexture;
        s.Rotate((float)(Util.rng.NextDouble() * 6.28));
        s.Position = pos;
        AddChild(s);
        foodSpriteList.Add(s);
    }

}
