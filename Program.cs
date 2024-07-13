using Krane.Core;
using Krane.Extensions;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using var app = new BlackHoleSim();
app.Start();
class BlackHoleSim : Game
{
    public static int WIDTH = 1000;
    public static int HEIGHT = 1000;
    List<PhysicsBody> bodies = [];
    LightSource? LightSource { get; set; }
    public BlackHoleSim() : base((1000, 1000), "Blackhole Sim",5000)
    {
        bodies.Add(new BlackHole
        {
            Movable = true,
            Mass = 100_000,
            Density = 100,
            Position = new(500, 500)
        });
        bodies.Add(new BlackHole
        {
            Movable = true,
            Mass = 100_000,
            Density = 100,
            Position = new(11, 500)
        });
        bodies.Add(new BlackHole
        {
            Movable = true,
            Mass = 10,
            Position = new(700, 500),
            Velocity = new(0, 900)
        });

        Input.OnKeyPress += KeyDown;
    }
    bool paused = false;
    private void KeyDown(object? sender, KeyEventArgs e)
    {
        // if l is pressed, create a light source
        if (e.Code == Keyboard.Key.L)
        {
            LightSource = new LightSource
            {
                Position = Input.MousePos,
                Radius = 10
            };
        }
        // if k is pressed, remove the light source
        if (e.Code == Keyboard.Key.K)
        {
            LightSource = null;
        }
        // if space is pressed, create a new black hole
        if (e.Code == Keyboard.Key.Space)
        {
            bodies.Add(new BlackHole
            {
                Movable = true,
                Mass = 100000,
                Density = 100,
                Position = Input.MousePos
            });
        }
        if(e.Code == Keyboard.Key.P)
        {
            paused = !paused;
        }
    }

    public override void Update()
    {
        foreach (var body in bodies)
        {
            if (Input.MousePos.Distance(body.Position) < body.Radius)
            {
                if (Input.DScroll > 0)
                {
                    body.Mass *= 1+Input.DScroll;
                    body.Mass = Math.Max(10, body.Mass);
                }
                if (Input.DScroll < 0)
                {
                    body.Mass /= 1 + -Input.DScroll;
                    body.Mass = Math.Max(10, body.Mass);
                }
                if (Input.IsMouseDown(Mouse.Button.Left))
                {
                    body.Velocity *= 0;
                    body.Position = Input.MousePos;
                    continue;
                }
                if (Input.IsMouseDown(Mouse.Button.Right))
                {
                    body.Destroyed = true;
                    continue;
                }
            }
            if (paused) continue;
            body.Update(bodies);
        }

        bodies.RemoveAll(b => b.Destroyed);
    }
    public override void Draw()
    {
        Render.Clear();
        LightSource?.Draw(bodies);
        bodies.ForEach(b => b.Draw());
    }
}
class LightSource
{
    // when turned on, the light source will emit light rays in all directions,
    // these rays are visualized as lines and can be affected by gravity
    // the rays are drawn like a raymarching algorithm assuming the light has a high speed in its current direction of travel
    // the rays will be affected by gravity and will bend towards the black hole

    public bool On { get; set; } = true;
    public float Radius { get; set; }
    public Vector2f Position { get; set; }
    public float AngularResolution { get; set; } = 5f;
    public float Resolution { get; set; } = 1f;
    public float c { get; set; } = 100f;
    public float maxDistance = 500f;
    public async void Draw(List<PhysicsBody> bodies)
    {
        var source = new CircleShape(Radius)
        {
            Position = Position,
            FillColor = Color.White
        }.Center();
        Render.Draw(source);
        if (!On) return;

        for (float angle = 0f; angle < 360; angle += AngularResolution)
        {
            var direction = new Vector2f(MathF.Cos(angle),MathF.Sin(angle)) * c;
            List<Vector2f> points = [Position];

            for(float step = 0; step < maxDistance; step += c)
            {
                var currentPos = points.Last();

                var forcesSum = new Vector2f(0, 0);
                // sum up the forces
                foreach (var body in bodies)
                {
                    var dir = Position - body.Position;
                    var distance = (float)Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
                    var force = 6.674f * 1e-10f * body.Mass / (distance);
                    forcesSum += direction.Normalize().Mul(force);
                }
                direction += forcesSum * c;
                direction = direction.Normalize() * c;
                var nextPos = currentPos + direction;
                points.Add(nextPos);
            }
            for (int i = 0; i < points.Count; i++)
            {
                var c = new Color((uint)Random.Shared.Next());
                var line = new LineShape(points[i], points[Math.Min(i + 1,points.Count-1)], 1, c);
                Render.Draw(line);
            }
        }

    }
}
public class PhysicsBody
{
    public bool Destroyed { get; set; }
    public bool Movable { get; set; }
    public float Mass { get; set; }
    public float Density { get; set; } = 1f;
    public float Radius => (float)Math.Sqrt(Mass / Density / Math.PI);
    public Vector2f Position { get; set; }
    public Vector2f Velocity { get; set; }
    public Vector2f Acceleration { get; set; }
    public virtual void Update(List<PhysicsBody> bodies) { }
    public virtual void Draw() { }
    public void Pull(Vector2f force) => Acceleration += force / Mass;
    public void Integrate()
    {
        if (!Movable) return;
        var dt = GameTime.DeltaTime.AsSeconds();
        Velocity += Acceleration * dt;
        Position += Velocity * dt;
        Acceleration *= 0;

        if (Position.X < 0 || Position.X > BlackHoleSim.WIDTH || Position.Y < 0 || Position.Y > BlackHoleSim.HEIGHT)
        {
            Position = new(Math.Clamp(Position.X,0,BlackHoleSim.WIDTH), Math.Clamp(Position.Y, 0, BlackHoleSim.HEIGHT));
            Velocity *= -.8f;
        }
    }
    public void Collide(List<PhysicsBody> bodies)
    {
        // ignore self and bodies that are not in contact
        foreach (var body in bodies)
        {
            if (this == body) continue;
            if (body.Position.Distance(Position) > body.Radius + Radius) continue;
            // resolve collision
            var direction = Position - body.Position;
            var distance = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            var overlap = (Radius + body.Radius) - distance;
            var normal = direction.Normalize();
            // resolve position accounting for mass
            
            Position += normal * overlap * (Movable?(body.Mass / (Mass + body.Mass)):0);
            body.Position -= normal * overlap * (body.Movable?(Mass / (Mass + body.Mass)):0);
            // resolve velocity
            var relativeVelocity = Velocity - body.Velocity;
            var velocityAlongNormal = relativeVelocity.X * normal.X + relativeVelocity.Y * normal.Y;
            if (velocityAlongNormal > 0) continue;
            var e = 1f;
            var j = -(1 + e) * velocityAlongNormal;
            j /= 1 / Mass + 1 / body.Mass;
            var impulse = normal * j * 0.9f;
            Velocity += impulse / Mass;
            body.Velocity -= impulse / body.Mass;
        }
    }
}
class BlackHole : PhysicsBody
{
    public override void Update(List<PhysicsBody> bodies)
    {
        foreach (var body in bodies)
        {
            if (body == this) continue;
            var direction = Position - body.Position;
            var distance = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            var force = 6.674f * Mass * body.Mass / (distance);
            body.Pull(direction.Normalize().Mul(force));
        }
        Integrate();
        Collide(bodies);
    }
    public override void Draw()
    {
        var circle = new CircleShape(Radius)
        {
            Position = Position,
            FillColor = Color.Black,
            OutlineColor = Color.Yellow,
            OutlineThickness = 1
        }.Center();
        Render.Draw(circle);
    }
}