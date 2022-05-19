﻿namespace RayTracer;

interface IWindow<R> where R : IRenderer
{
    int Width { get; }
    int Height { get; }
    R Renderer { get; }

    void Update();
    void Set(int x, int y, float color);
    void Set(int x, int y, char ch);
    void Push();
    ConsoleKey? KeyPressed();

    void Draw(Func<int, int, float> pixelColor) =>
        Parallel.For(0, Height, y => Parallel.For(0, Width, x => Set(x, y, pixelColor(x, y))));

    void Draw(Func<int, int, char> pixelChar) =>
        Parallel.For(0, Height, y => Parallel.For(0, Width, x => Set(x, y, pixelChar(x, y))));

    void Draw<E, C>(E entity, C camera) where E : IEntity where C : ICamera
    {
        var scaleX = 1f / Width;
        var scaleY = 1f / Height;
        Draw((x, y) => Renderer.PixelColor(entity, camera, x * scaleX, y * scaleY));
    }

    void Draw(string? label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        const int borderWidth = 1;
        const int padding = 2;

        var width = label.Length + (borderWidth + padding) * 2;
        var height = 1 + (borderWidth + padding) * 2;
        var topLeftX = (Width - width) / 2;
        var topLeftY = (Height - height) / 2;

        for (var dx = 0; dx < width; dx++)
        {
            for (var dy = 0; dy < borderWidth; dy++)
            {
                Set(topLeftX + dx, topLeftY + dy, 1f);
                Set(topLeftX + dx, topLeftY + height - borderWidth + dy, 1f);
            }
        }

        for (var dy = borderWidth; dy < height - borderWidth; dy++)
        {
            for (var dx = 0; dx < borderWidth; dx++)
            {
                Set(topLeftX + dx, topLeftY + dy, 1f);
                Set(topLeftX + width - borderWidth + dx, topLeftY + dy, 1f);
            }
            for (var dx = 0; dx < padding; dx++)
            {
                Set(topLeftX + borderWidth + dx, topLeftY + dy, 0f);
                Set(topLeftX + width - borderWidth - padding + dx, topLeftY + dy, 0f);
            }
            for (var dx = 0; dx < label.Length; dx++)
            {
                if (dy == borderWidth + padding)
                {
                    Set(topLeftX + borderWidth + padding + dx, topLeftY + dy, label[dx]);
                }
                else
                {
                    Set(topLeftX + borderWidth + padding + dx, topLeftY + dy, 0f);
                }
            }
        }
    }
}
