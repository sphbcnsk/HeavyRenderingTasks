using Backend.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Threading;

namespace Backend.Services
{
    public class CurveRenderer
    {
        public string RenderCurve(RenderTask task, string webRootPath, Action<int> reportProgress = null, CancellationToken cancellationToken = default)
        {
            int width = task.CanvasWidth;
            int height = task.CanvasHeight;
            int iterations = task.Iterations;

            bool isLissajous = !string.IsNullOrEmpty(task.FigureType) && task.FigureType.Contains("Lissajous", StringComparison.OrdinalIgnoreCase);
            bool isRose = !string.IsNullOrEmpty(task.FigureType) && task.FigureType.Contains("Rose", StringComparison.OrdinalIgnoreCase);

            Rgba32 backgroundColor;
            if (isLissajous)
            {
                backgroundColor = new Rgba32(220, 100, 20); 
            }
            else if (isRose)
            {
                backgroundColor = new Rgba32(30, 130, 60); 
            }
            else 
            {
                backgroundColor = new Rgba32(180, 20, 40); 
            }

            Rgba32 figureColor = new Rgba32(255, 240, 50);

            using var image = new Image<Rgba32>(width, height);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        pixelRow[x] = backgroundColor;
                    }
                }
            });

            double centerX = width / 2.0;
            double centerY = height / 2.0;

            int reportStep = iterations / 20;
            if (reportStep == 0) reportStep = 1;

            for (int i = 0; i < iterations; i++)
            {
                if (i % 1000 == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.Sleep(1);
                }

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
                else if (isRose)
                {
                    double k = 5.0;
                    double roseScale = Math.Min(width, height) * 0.4;
                    px = Math.Cos(k * t) * Math.Cos(t) * roseScale;
                    py = Math.Cos(k * t) * Math.Sin(t) * roseScale;
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
                            image[drawX, drawY] = figureColor;
                        }
                    }
                }

                if (i > 0 && i % reportStep == 0 && reportProgress != null)
                {
                    int percent = (int)((double)i / iterations * 100);
                    reportProgress(percent);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

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