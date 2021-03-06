namespace ConsoleRayTracer;

public readonly record struct Group(IReadOnlyList<IEntity> Entities) : IAnimatedEntity
{
    private readonly IReadOnlyList<IAnimatedEntity> _animatedEntities =
        Entities.Select(e => e as IAnimatedEntity).Where(e => e is not null).ToArray()!;

    public HitRecord? Hit(in Ray ray, float tMin, float tMax)
    {
        var closest = tMax;
        HitRecord? hit = null;
        foreach (var entity in Entities)
        {
            if (entity.Hit(ray, tMin, closest) is HitRecord record)
            {
                closest = record.T;
                hit = record;
            }
        }
        return hit;
    }

    public void Update(float timeElapsed)
    {
        foreach (var animated in _animatedEntities)
        {
            animated.Update(timeElapsed);
        }
    }
}

public readonly record struct Lights(IReadOnlyList<IEntity> Sources) : IAnimatedEntity
{
    private readonly IReadOnlyList<IAnimatedEntity> _animatedSources =
        Sources.Select(e => e as IAnimatedEntity).Where(e => e is not null).ToArray()!;

    public float Illuminate<I>(in I entity, in HitRecord record) where I : IEntity
    {
        var accum = 0f;
        foreach (var source in Sources)
        {
            accum += source.Illuminate(entity, record);
        }
        return accum;
    }

    public void Update(float timeElapsed)
    {
        foreach (var animated in _animatedSources)
        {
            animated.Update(timeElapsed);
        }
    }
}

public readonly record struct LightSource : IEntity
{
    public float Illuminate<I>(in I entity, in HitRecord record) where I : IEntity
    {
        Ray toLight = new(record.Point, Vector3.Normalize(-record.Point));
        return entity.Hit(toLight, 0.001f, float.PositiveInfinity) is null
            ? Math.Max(record.Brightness * Vector3.Dot(toLight.Direction, record.Normal), 0f)
            : 0f;
    }
}

public readonly record struct And<L, R>(L Left, R Right) : IEntity
    where L : IEntity
    where R : IEntity
{
    public HitRecord? Hit(in Ray ray, float tMin, float tMax) =>
        Left.Hit(ray, tMin, tMax) is HitRecord left
            ? Right.Hit(ray, tMin, left.T) ?? left
            : Right.Hit(ray, tMin, tMax);

    public float Illuminate<I>(in I entity, in HitRecord record) where I : IEntity =>
        Left.Illuminate(entity, record) + Right.Illuminate(entity, record);
}

public readonly record struct Plane<A>(A Axis) : IEntity where A : IAxis
{
    public HitRecord? Hit(in Ray ray, float tMin, float tMax)
    {
        var t = -Axis.GetAxis(ray.Origin) / Axis.GetAxis(ray.Direction);
        return t < tMin || t > tMax ? null : new(t, ray.PointAt(t), Axis.Unit);
    }
}

public readonly record struct Circle<A>(float Radius, A Axis) : IEntity where A : IAxis
{
    public HitRecord? Hit(in Ray ray, float tMin, float tMax) =>
        new Plane<A>(Axis).Hit(ray, tMin, tMax) is HitRecord record
            ? Math.Sqrt(Vector3.Dot(record.Point, record.Point)) <= Radius ? record : null
            : null;
}

public readonly record struct Rect<A>(float Width, float Height, A Axis) : IEntity where A : IAxis
{
    private readonly float _width = Width / 2f;
    private readonly float _height = Height / 2f;

    public HitRecord? Hit(in Ray ray, float tMin, float tMax) =>
        new Plane<A>(Axis).Hit(ray, tMin, tMax) is HitRecord record
            ? Axis.GetMain(record.Point) < -_width
                || Axis.GetMain(record.Point) > _width
                || Axis.GetSecondary(record.Point) < -_height
                || Axis.GetSecondary(record.Point) > _height
                ? null
                : record
            : null;
}

public readonly record struct Sphere(float Radius) : IEntity
{
    public HitRecord? Hit(in Ray ray, float tMin, float tMax)
    {
        var a = Vector3.Dot(ray.Direction, ray.Direction);
        var b = Vector3.Dot(ray.Origin, ray.Direction);
        var c = Vector3.Dot(ray.Origin, ray.Origin) - Radius * Radius;
        var sqrtD = (float)Math.Sqrt(b * b - a * c);
        if (float.IsNaN(sqrtD))
        {
            return null;
        }

        var t = (-b - sqrtD) / a;
        if (t < tMin || t > tMax)
        {
            t = (-b + sqrtD) / a;
            if (t < tMin || t > tMax)
            {
                return null;
            }
        }
        var point = ray.PointAt(t);
        return new(t, point, point / Radius);
    }
}

public readonly record struct Cylinder(float Radius, float Height) : IEntity
{
    private readonly And<And<Apply<Circle<AxisY>>, Circle<AxisY>>, Lateral> _components = new(
        new(
            new(new(Radius, new()), new(0f, Height, 0f)),
            new(Radius, new())
        ),
        new(Radius, Height)
    );

    public HitRecord? Hit(in Ray ray, float tMin, float tMax) => _components.Hit(ray, tMin, tMax);

    readonly record struct Lateral(float Radius, float Height) : IEntity
    {
        public HitRecord? Hit(in Ray ray, float tMin, float tMax)
        {
            Ray side = new(ray.Origin with { Y = 0 }, ray.Direction with { Y = 0f });
            var a = Vector3.Dot(side.Direction, side.Direction);
            var b = Vector3.Dot(side.Origin, side.Direction);
            var c = Vector3.Dot(side.Origin, side.Origin) - Radius * Radius;
            var sqrtD = (float)Math.Sqrt(b * b - a * c);
            if (float.IsNaN(sqrtD))
            {
                return null;
            }

            var t = (-b - sqrtD) / a;
            if (t < tMin || t > tMax)
            {
                t = (-b + sqrtD) / a;
                if (t < tMin || t > tMax)
                {
                    return null;
                }
            }
            var point = ray.PointAt(t);
            return point.Y < 0f || point.Y > Height
                ? null
                : new(t, point, Vector3.Normalize(point with { Y = 0f }));
        }
    }
}

public readonly record struct Cone(float Radius, float Height) : IEntity
{
    private readonly And<Circle<AxisY>, Lateral> _components = new(
        new(Radius, new()),
        new(Radius, Height)
    );

    public HitRecord? Hit(in Ray ray, float tMin, float tMax) => _components.Hit(ray, tMin, tMax);

    readonly record struct Lateral(float Radius, float Height) : IEntity
    {
        private readonly float _ratio = Radius / Height;

        public HitRecord? Hit(in Ray ray, float tMin, float tMax)
        {
            Ray side = new(ray.Origin with { Y = 0f }, ray.Direction with { Y = 0f });
            var tan = _ratio * _ratio;
            var D = Height - ray.Origin.Y;
            var a = Vector3.Dot(side.Direction, side.Direction) - tan * ray.Direction.Y * ray.Direction.Y;
            var b = Vector3.Dot(side.Origin, side.Direction) + tan * D * ray.Direction.Y;
            var c = Vector3.Dot(side.Origin, side.Origin) - tan * D * D;
            var sqrtD = (float)Math.Sqrt(b * b - a * c);
            if (float.IsNaN(sqrtD))
            {
                return null;
            }

            var t = (-b - sqrtD) / a;
            if (t < tMin || t > tMax)
            {
                t = (-b + sqrtD) / a;
                if (t < tMin || t > tMax)
                {
                    return null;
                }
            }
            var point = ray.PointAt(t);
            return point.Y < 0f || point.Y > Height
                ? null
                : new(t, point, Vector3.Normalize(point with { Y = (float)Math.Sqrt(point.X * point.X + point.Z * point.Z) * _ratio }));
        }
    }
}

public readonly record struct RectPrism(float Width, float Height, float Depth) : IEntity
{
    private readonly And<And<And<Apply<Rect<AxisZ>>, Apply<Rect<AxisX>>>, Rect<AxisY>>, And<And<Apply<Rect<AxisZ>>, Apply<Rect<AxisX>>>, Apply<Rect<AxisY>>>> _components = new(
        new(
            new(
                new(new(Width, Height, new()), new(0f, Height / 2f, -Depth / 2f)),
                new(new(Depth, Height, new()), new(-Width / 2f, Height / 2f, 0f))
            ),
            new(Width, Depth, new())
        ),
        new(
            new(
                new(new(Width, Height, new()), new(0f, Height / 2f, Depth / 2f)),
                new(new(Depth, Height, new()), new(Width / 2f, Height / 2f, 0f))
            ),
            new(new(Width, Depth, new()), new(0f, Height, 0f))
        )
    );

    public HitRecord? Hit(in Ray ray, float tMin, float tMax) => _components.Hit(ray, tMin, tMax);
}
