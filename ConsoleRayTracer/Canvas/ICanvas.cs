﻿namespace ConsoleRayTracer;

public interface ICanvas
{
    int Width { get; }
    int Height { get; }

    void Refresh();
    void Set(int x, int y, float color);
    void Set(int x, int y, char ch);
    void Commit();
}

public static class CanvasExtensions
{
    public static void Draw<C>(this C canvas, Func<int, int, float> color)
        where C : class, ICanvas
    {
        Parallel.For(0, canvas.Height, y =>
        {
            Parallel.For(0, canvas.Width, x =>
            {
                canvas.Set(x, y, color(x, y));
            });
        });
    }

    public static void Draw<C>(this C canvas, Func<int, int, char> ch)
        where C : class, ICanvas
    {
        Parallel.For(0, canvas.Height, y =>
        {
            Parallel.For(0, canvas.Width, x =>
            {
                canvas.Set(x, y, ch(x, y));
            });
        });
    }

    public static void Draw<C, R, D>(this C canvas, in D drawable, R renderer)
        where C : class, ICanvas
        where R : class, IRenderer
        where D : IDrawable
    {
        drawable.Draw(canvas, renderer);
    }
}