param(
    [Parameter(Mandatory = $true)] [string] $Source,
    [Parameter(Mandatory = $true)] [string] $Destination
)

Add-Type -AssemblyName System.Drawing

$code = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class IcoWriter
{
    static readonly int[] DibSizes = { 16, 24, 32, 48, 64, 128 };

    public static void Write(string sourcePng, string outIco)
    {
        using (var source = new Bitmap(sourcePng))
        {
            if (source.Width != source.Height)
                throw new Exception("the source has to be square, this one is "
                                    + source.Width + "x" + source.Height);

            var frames = new List<KeyValuePair<int, byte[]>>();
            foreach (var size in DibSizes)
                frames.Add(new KeyValuePair<int, byte[]>(size, Dib(source, size)));
            frames.Add(new KeyValuePair<int, byte[]>(256, Png(source, 256)));

            using (var file = new FileStream(outIco, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(file))
            {
                w.Write((ushort)0);
                w.Write((ushort)1);
                w.Write((ushort)frames.Count);

                var offset = 6 + 16 * frames.Count;
                foreach (var frame in frames)
                {
                    var size = frame.Key;
                    w.Write((byte)(size == 256 ? 0 : size));
                    w.Write((byte)(size == 256 ? 0 : size));
                    w.Write((byte)0);
                    w.Write((byte)0);
                    w.Write((ushort)1);
                    w.Write((ushort)32);
                    w.Write(frame.Value.Length);
                    w.Write(offset);
                    offset += frame.Value.Length;
                }
                foreach (var frame in frames) w.Write(frame.Value);
            }
        }
    }

    static Bitmap Scale(Bitmap source, int size)
    {
        var scaled = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, size, size));
        }
        return scaled;
    }

    static byte[] Png(Bitmap source, int size)
    {
        using (var scaled = Scale(source, size))
        using (var memory = new MemoryStream())
        {
            scaled.Save(memory, ImageFormat.Png);
            return memory.ToArray();
        }
    }

    static byte[] Dib(Bitmap source, int size)
    {
        using (var scaled = Scale(source, size))
        using (var memory = new MemoryStream())
        using (var w = new BinaryWriter(memory))
        {
            w.Write(40);
            w.Write(size);
            w.Write(size * 2);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write(0);
            w.Write(size * size * 4);
            w.Write(0); w.Write(0);
            w.Write(0); w.Write(0);

            for (var y = size - 1; y >= 0; y--)
                for (var x = 0; x < size; x++)
                {
                    var p = scaled.GetPixel(x, y);
                    w.Write(p.B); w.Write(p.G); w.Write(p.R); w.Write(p.A);
                }

            var stride = ((size + 31) / 32) * 4;
            var row = new byte[stride];
            for (var y = size - 1; y >= 0; y--)
            {
                Array.Clear(row, 0, row.Length);
                for (var x = 0; x < size; x++)
                    if (scaled.GetPixel(x, y).A < 128)
                        row[x / 8] |= (byte)(0x80 >> (x % 8));
                w.Write(row);
            }
            w.Flush();
            return memory.ToArray();
        }
    }
}
'@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing -ErrorAction Stop

[IcoWriter]::Write((Resolve-Path $Source), (Join-Path (Get-Location) $Destination))
Write-Host ("{0} -> {1} ({2} bytes)" -f $Source, $Destination, (Get-Item $Destination).Length)

foreach ($s in 16, 24, 32, 48, 64, 128) {
    $icon = New-Object System.Drawing.Icon($Destination, (New-Object System.Drawing.Size($s, $s)))
    if ($icon.Width -ne $s) { throw "asked for ${s}px, got $($icon.Width)px" }
    $icon.Dispose()
}
Write-Host "all frames read back"
