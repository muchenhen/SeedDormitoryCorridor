using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SeedDormitoryCorridor.Platform.Windows;

internal sealed class LayeredSurfacePresenter : IDisposable
{
    private nint screenDc;
    private nint memoryDc;
    private nint dib;
    private nint oldBitmap;
    private nint bits;
    private int width;
    private int height;
    private bool disposed;

    internal LayeredSurfacePresenter()
    {
        screenDc = NativeMethods.GetDC(0);
        if (screenDc == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetDC failed.");
        }

        memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
        if (memoryDc == 0)
        {
            int error = Marshal.GetLastWin32Error();
            _ = NativeMethods.ReleaseDC(0, screenDc);
            screenDc = 0;
            throw new Win32Exception(error, "CreateCompatibleDC failed.");
        }
    }

    internal void Present(nint hwnd, Bitmap source, Point location)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        EnsureSize(source.Width, source.Height);
        CopyPixels(source);

        var destination = new NativeMethods.Point(location.X, location.Y);
        var size = new NativeMethods.Size(width, height);
        var origin = new NativeMethods.Point(0, 0);
        var blend = new NativeMethods.BlendFunction
        {
            BlendOp = NativeMethods.AcSrcOver,
            SourceConstantAlpha = 255,
            AlphaFormat = NativeMethods.AcSrcAlpha,
        };
        if (!NativeMethods.UpdateLayeredWindow(hwnd, screenDc, ref destination, ref size, memoryDc, ref origin, 0, ref blend, NativeMethods.UlwAlpha))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateLayeredWindow failed.");
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        ReleaseDib();
        if (memoryDc != 0)
        {
            NativeMethods.DeleteDC(memoryDc);
            memoryDc = 0;
        }

        if (screenDc != 0)
        {
            _ = NativeMethods.ReleaseDC(0, screenDc);
            screenDc = 0;
        }

        disposed = true;
    }

    private void EnsureSize(int requiredWidth, int requiredHeight)
    {
        if (width == requiredWidth && height == requiredHeight && dib != 0)
        {
            return;
        }

        ReleaseDib();
        var info = new NativeMethods.BitmapInfo
        {
            Header = new NativeMethods.BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
                Width = requiredWidth,
                Height = -requiredHeight,
                Planes = 1,
                BitCount = 32,
            },
        };
        dib = NativeMethods.CreateDIBSection(memoryDc, ref info, NativeMethods.DibRgbColors, out bits, 0, 0);
        if (dib == 0 || bits == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateDIBSection failed.");
        }

        oldBitmap = NativeMethods.SelectObject(memoryDc, dib);
        if (oldBitmap == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SelectObject failed.");
        }

        width = requiredWidth;
        height = requiredHeight;
    }

    private void CopyPixels(Bitmap source)
    {
        Rectangle rectangle = new(0, 0, width, height);
        BitmapData data = source.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            int rowBytes = width * 4;
            for (int y = 0; y < height; y++)
            {
                nint sourceRow = data.Stride >= 0
                    ? data.Scan0 + (y * data.Stride)
                    : data.Scan0 + ((height - 1 - y) * -data.Stride);
                unsafe
                {
                    Buffer.MemoryCopy(
                        (void*)sourceRow,
                        (void*)(bits + (y * rowBytes)),
                        rowBytes,
                        rowBytes);
                }
            }
        }
        finally
        {
            source.UnlockBits(data);
        }
    }

    private void ReleaseDib()
    {
        if (oldBitmap != 0 && memoryDc != 0)
        {
            NativeMethods.SelectObject(memoryDc, oldBitmap);
            oldBitmap = 0;
        }

        if (dib != 0)
        {
            NativeMethods.DeleteObject(dib);
            dib = 0;
        }

        bits = 0;
        width = 0;
        height = 0;
    }
}
