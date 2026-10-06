namespace TrailBlaze.Service.Test.TestSupport;

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

/// <summary>
/// Real image bytes, written here rather than produced by the library under test.
/// </summary>
/// <remarks>
/// A fixture encoded by Magick.NET would make "the service derived a smaller copy" and "the library
/// can write an image" the same claim, so a change in the library would move both sides of an
/// assertion at once. These are built from the formats themselves — PNG through
/// <see cref="ZLibStream"/>, GIF89a by hand — so the input is an independent artefact and stays the
/// same bytes on every machine.
/// </remarks>
internal static class ImageFixtures
{
    /// <summary>Bytes that are not an image at all.</summary>
    public static byte[] NotAnImage => "this is not an image"u8.ToArray();

    /// <summary>A PNG of the given size whose every pixel is painted by <paramref name="pixel"/>.</summary>
    public static byte[] Png(int width, int height, Func<int, int, (byte R, byte G, byte B, byte A)> pixel)
    {
        // Each scanline is a filter byte (0, "none") followed by RGBA per pixel: colour type 6, so
        // the encoder needs no palette and an alpha channel is available when a test wants one.
        byte[] raw = new byte[height * ((width * 4) + 1)];
        int at = 0;

        for (int y = 0; y < height; y++)
        {
            raw[at++] = 0;

            for (int x = 0; x < width; x++)
            {
                (byte R, byte G, byte B, byte A) colour = pixel(x, y);
                raw[at++] = colour.R;
                raw[at++] = colour.G;
                raw[at++] = colour.B;
                raw[at++] = colour.A;
            }
        }

        using var compressed = new MemoryStream();

        using (var deflate = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;  // bit depth
        header[9] = 6;  // colour type: truecolour with alpha

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);

        return png.ToArray();
    }

    /// <summary>A smooth diagonal gradient, which compresses well and re-encodes small.</summary>
    public static byte[] Gradient(int width, int height) =>
        Png(width, height, (x, y) => (
            (byte)(x * 255 / Math.Max(1, width - 1)),
            (byte)(y * 255 / Math.Max(1, height - 1)),
            (byte)((x + y) * 255 / Math.Max(1, width + height - 2)),
            byte.MaxValue));

    /// <summary>
    /// A photograph's defining property for these tests: neighbouring pixels differ, so it does not
    /// compress away, and a lossy re-encode of it is dramatically smaller than the source.
    /// </summary>
    public static byte[] Noisy(int width, int height)
    {
        var random = new Random(20261006);

        return Png(width, height, (_, _) => ((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), byte.MaxValue));
    }

    /// <summary>An image whose every pixel is fully transparent.</summary>
    public static byte[] Transparent(int width, int height) =>
        Png(width, height, (_, _) => (0, 0, 0, 0));

    /// <summary>A GIF89a holding <paramref name="frames"/> distinct frames.</summary>
    /// <remarks>
    /// The LZW stream is written the simple way — a clear code, at most 254 literal codes, another
    /// clear — so the dictionary never grows past nine bits and no compressor is needed. That is a
    /// valid stream a decoder reads frame by frame, which is all this fixture has to be.
    /// </remarks>
    public static byte[] AnimatedGif(int width, int height, int frames)
    {
        using var gif = new MemoryStream();

        gif.Write(Encoding.ASCII.GetBytes("GIF89a"));

        // Logical screen descriptor: a full 256-entry global table, 8 bits per entry.
        WriteUInt16(gif, width);
        WriteUInt16(gif, height);
        gif.WriteByte(0xF7);
        gif.WriteByte(0);
        gif.WriteByte(0);

        // Entry 0 is black and entry 1 is white; the frames alternate between the two.
        for (int entry = 0; entry < 256; entry++)
        {
            gif.WriteByte(0);
            gif.WriteByte(0);
            gif.WriteByte(entry == 1 ? (byte)255 : (byte)0);
        }

        // The loop extension, so a decoder reads this as an animation rather than as unrelated stills.
        gif.Write([0x21, 0xFF, 0x0B]);
        gif.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
        gif.Write([0x03, 0x01, 0x00, 0x00, 0x00]);

        for (int frame = 0; frame < frames; frame++)
        {
            // Graphic control extension: no transparency, no delay, restore to background.
            gif.Write([0x21, 0xF9, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00]);

            gif.WriteByte(0x2C);
            WriteUInt16(gif, 0);
            WriteUInt16(gif, 0);
            WriteUInt16(gif, width);
            WriteUInt16(gif, height);
            gif.WriteByte(0);

            gif.WriteByte(8);

            byte[] lzw = Lzw(width * height, (byte)(frame % 2));
            for (int at = 0; at < lzw.Length; at += 255)
            {
                int block = Math.Min(255, lzw.Length - at);
                gif.WriteByte((byte)block);
                gif.Write(lzw, at, block);
            }

            gif.WriteByte(0);
        }

        gif.WriteByte(0x3B);

        return gif.ToArray();
    }

    /// <summary>Nine-bit literal codes, restarted before the decoder's table would outgrow them.</summary>
    private static byte[] Lzw(int pixelCount, byte value)
    {
        const int Clear = 256;
        const int End = 257;

        using var packed = new MemoryStream();
        int accumulator = 0;
        int held = 0;

        void Emit(int code)
        {
            accumulator |= code << held;
            held += 9;

            while (held >= 8)
            {
                packed.WriteByte((byte)(accumulator & 0xFF));
                accumulator >>= 8;
                held -= 8;
            }
        }

        int sinceClear = 0;
        Emit(Clear);

        for (int pixel = 0; pixel < pixelCount; pixel++)
        {
            // A clear code resets the decoder's next free entry to 258, and each code after it adds
            // one; at 254 the table would be one step from needing ten bits, so it restarts here.
            if (sinceClear == 254)
            {
                Emit(Clear);
                sinceClear = 0;
            }

            Emit(value);
            sinceClear++;
        }

        Emit(End);

        if (held > 0)
        {
            packed.WriteByte((byte)(accumulator & 0xFF));
        }

        return packed.ToArray();
    }

    private static void WriteUInt16(Stream stream, int value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);

        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(typeBytes);
        stream.Write(data);

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32([.. typeBytes, .. data]));
        stream.Write(crc);
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;

        foreach (byte value in data)
        {
            crc ^= value;

            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFF;
    }
}
