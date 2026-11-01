using Backend.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace Backend.Services
{
    public class CurveRenderer
    {
        public string RenderCurve(RenderTask task, string webRootPath)
        {
            int width = task.CanvasWidth;
            int height = task.CanvasHeight;
            int iterations = task.Iterations;

            using var image = new Image<Rgba32>(width, height);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        pixelRow[x] = new Rgba32(0, 255, 0);
                    }
                }
            });

            double centerX = width / 2.0;
            double centerY = height / 2.0;

            bool isLissajous = !string.IsNullOrEmpty(task.FigureType) &&
                               task.FigureType.Contains("Lissajous", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < iterations; i++)
            {
                double t = i * 0.005;
                double px = 0, py = 0;

                if (isLissajous)
                {
                    double a = 5.0;
                    double b = 4.0;
                    double lissajousScale = Math.Min(width, height) * 0.4;

                    px = Math.Sin(a * t + Math.PI / 2) * lissajousScale;
                    py = Math.Sin(b * t) * lissajousScale;
                }
                else
                {
                    double R = 50.0;
                    double r = 11.0;
                    double epicycloidScale = Math.Min(width, height) / (2.0 * (R + 2.0 * r)) * 0.8;

                    px = ((R + r) * Math.Cos(t) - r * Math.Cos((R + r) / r * t)) * epicycloidScale;
                    py = ((R + r) * Math.Sin(t) - r * Math.Sin((R + r) / r * t)) * epicycloidScale;
                }

                int x = (int)(centerX + px);
                int y = (int)(centerY + py);

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int drawX = x + dx;
                        int drawY = y + dy;

                        if (drawX >= 0 && drawX < width && drawY >= 0 && drawY < height)
                        {
                            if (isLissajous)
                                image[drawX, drawY] = new Rgba32(255, 105, 180); // Рожевий
                            else
                                image[drawX, drawY] = new Rgba32(0, 210, 255);   // Блакитний
                        }
                    }
                }
            }

            string resultsFolder = Path.Combine(webRootPath, "results");
            if (!Directory.Exists(resultsFolder))
            {
                Directory.CreateDirectory(resultsFolder);
            }

            string fileName = $"{Guid.NewGuid()}.png";
            string filePath = Path.Combine(resultsFolder, fileName);

            image.Save(filePath);

            return $"/results/{fileName}";
        }
    }
}