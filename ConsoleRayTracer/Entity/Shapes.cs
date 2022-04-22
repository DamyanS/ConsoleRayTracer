﻿namespace ConsoleRayTracer;

readonly record struct World(IEnumerable<IEntity> Entities) : IAnimatedEntity
{
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
        foreach (var entity in Entities)
        {
            if (entity is IAnimatedEntity e)
            {
                e.Update(timeElapsed);
            }
        }
    }
}

readonly record struct And<L, R>(L Left, R Right) : IEntity
    where L : IEntity
    where R : IEntity
{
    public HitRecord? Hit(in Ray ray, float tMin, float tMax) =>
        Left.Hit(ray, tMin, tMax) switch
        {
            HitRecord left => Right.Hit(ray, tMin, left.T) ?? left,
            null => Right.Hit(ray, tMin, tMax),
        };

    public float Illuminate<I>(in I entity, in HitRecord record) where I : IEntity =>
        Left.Illuminate(entity, record) + Right.Illuminate(entity, record);
}

readonly record struct Plane<A>(A Axis) : IEntity where A : IAxis
{
    public HitRecord? Hit(in Ray ray, float tMin, float tMax)
    {
        var t = -ray.Origin.Get(Axis.Axis) / ray.Direction.Get(Axis.Axis);
        return t < tMin || t > tMax ? null : new(t, ray.PointAt(t), Axis.Unit);
    }
}

readonly record struct Circle<A>(float Radius, A Axis) : IEntity where A : IAxis
{
    public HitRecord? Hit(in Ray ray, float tMin, float tMax) =>
        new Plane<A>(Axis).Hit(ray, tMin, tMax) is HitRecord record
            ? Math.Sqrt(Vector3.Dot(record.Point, record.Point)) <= Radius ? record : null
            : null;
}

readonly record struct Rect<A>(float Width, float Height, A Axis) : IEntity where A : IAxis
{
    private readonly float _width = Width / 2f;
    private readonly float _height = Height / 2f;

    public HitRecord? Hit(in Ray ray, float tMin, float tMax) =>
        new Plane<A>(Axis).Hit(ray, tMin, tMax) is HitRecord record
            ? record.Point.Get(Axis.Main) < -_width
                || record.Point.Get(Axis.Main) > _width
                || record.Point.Get(Axis.Secondary) < -_height
                || record.Point.Get(Axis.Secondary) > _height
                ? null
                : record
            : null;
}

readonly record struct Sphere(float Radius) : IEntity
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
        var normal = point / Radius;
        return new(t, point, normal);
    }
}

readonly record struct Cylinder(float Radius, float Height) : IEntity
{
    private readonly And<Apply<Circle<AxisY>>, Circle<AxisY>> _bases = new(
        new(new(Radius, new()), new(0f, Height, 0f)),
        new(Radius, new())
    );

    public HitRecord? Hit(in Ray ray, float tMin, float tMax)
    {
        var baseHit = _bases.Hit(ray, tMin, tMax);
        tMax = baseHit?.T ?? tMax;

        Ray side = new(ray.Origin with { Y = 0 }, ray.Direction with { Y = 0f });
        var a = Vector3.Dot(side.Direction, side.Direction);
        var b = Vector3.Dot(side.Origin, side.Direction);
        var c = Vector3.Dot(side.Origin, side.Origin) - Radius * Radius;
        var sqrtD = (float)Math.Sqrt(b * b - a * c);
        if (float.IsNaN(sqrtD))
        {
            return baseHit;
        }

        var t = (-b - sqrtD) / a;
        if (t < tMin || t > tMax)
        {
            t = (-b + sqrtD) / a;
            if (t < tMin || t > tMax)
            {
                return baseHit;
            }
        }
        var point = ray.PointAt(t);
        if (point.Y < 0f || point.Y > Height)
        {
            return baseHit;
        }
        var normal = Vector3.Normalize(point with { Y = 0f });
        return new(t, point, normal);
    }
}

readonly record struct Cone(float Radius, float Height) : IEntity
{
    private readonly float _ratio = Radius / Height;

    public HitRecord? Hit(in Ray ray, float tMin, float tMax)
    {
        var baseHit = new Circle<AxisY>(Radius, new()).Hit(ray, tMin, tMax);
        tMax = baseHit?.T ?? tMax;

        Ray side = new(ray.Origin with { Y = 0f }, ray.Direction with { Y = 0f });
        var tan = _ratio * _ratio;
        var D = Height - ray.Origin.Y;
        var a = Vector3.Dot(side.Direction, side.Direction) - tan * ray.Direction.Y * ray.Direction.Y;
        var b = Vector3.Dot(side.Origin, side.Direction) + tan * D * ray.Direction.Y;
        var c = Vector3.Dot(side.Origin, side.Origin) - tan * D * D;
        var sqrtD = (float)Math.Sqrt(b * b - a * c);
        if (float.IsNaN(sqrtD))
        {
            return baseHit;
        }

        var t = (-b - sqrtD) / a;
        if (t < tMin || t > tMax)
        {
            t = (-b + sqrtD) / a;
            if (t < tMin || t > tMax)
            {
                return baseHit;
            }
        }
        var point = ray.PointAt(t);
        if (point.Y < 0f || point.Y > Height)
        {
            return baseHit;
        }
        var normal = Vector3.Normalize(point with { Y = (float)Math.Sqrt(point.X * point.X + point.Z * point.Z) * _ratio });
        return new(t, point, normal);
    }
}

readonly record struct RectPrism(float Width, float Height, float Depth) : IEntity
{
    private readonly And<And<And<Apply<Rect<AxisZ>>, Apply<Rect<AxisX>>>, Rect<AxisY>>, And<And<Apply<Rect<AxisZ>>, Apply<Rect<AxisX>>>, Apply<Rect<AxisY>>>> _rects = new(
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

    public HitRecord? Hit(in Ray ray, float tMin, float tMax) => _rects.Hit(ray, tMin, tMax);
}
