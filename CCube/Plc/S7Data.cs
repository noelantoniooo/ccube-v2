// S7Data.cs
// -----------------------------------------------------------------------------
// The ONE place that knows how a Siemens PLC stores numbers, bits and text as
// raw bytes. Everything else in the app works with normal C# values (short,
// bool, string); this file translates to/from the PLC's byte format.
//
// Why this matters: Siemens uses conventions that differ from a PC:
//   * Whole numbers (S7 "Int") are 16 bits, BIG-ENDIAN (high byte first).
//   * Bits (S7 "Bool") are packed 8-to-a-byte, addressed as byte.bit (e.g. 46.1).
//   * Text (S7 "String") is NOT null-terminated. Byte 0 = declared max length,
//     byte 1 = current length, then the characters.
//
// We keep this tiny and self-contained so it's easy to read and easy to trust.
// A "Span<byte>" below just means "a window onto part of a byte array" - you can
// read it as "the bytes".
// -----------------------------------------------------------------------------

using System.Text;

namespace CCube.Plc;

public static class S7Data
{
    // --- Whole numbers (S7 Int = signed 16-bit, big-endian) --------------------

    /// <summary>Read a Siemens Int (2 bytes, high byte first) at the given offset.</summary>
    public static short GetInt(ReadOnlySpan<byte> data, int offset)
    {
        // High byte is first, low byte second - the opposite of a normal PC.
        return (short)((data[offset] << 8) | data[offset + 1]);
    }

    /// <summary>Write a Siemens Int (2 bytes, high byte first) at the given offset.</summary>
    public static void SetInt(Span<byte> data, int offset, short value)
    {
        data[offset] = (byte)(value >> 8);       // high byte
        data[offset + 1] = (byte)(value & 0xFF); // low byte
    }

    // --- Single bits (S7 Bool, addressed as byte.bit) --------------------------

    /// <summary>Read one bit (0-7) out of the byte at the given offset.</summary>
    public static bool GetBool(ReadOnlySpan<byte> data, int offset, int bit)
    {
        return (data[offset] & (1 << bit)) != 0;
    }

    /// <summary>
    /// Set or clear one bit without disturbing the other 7 bits in that byte.
    /// (Several of our flags share a single byte, so we must read-modify-write.)
    /// </summary>
    public static void SetBool(Span<byte> data, int offset, int bit, bool value)
    {
        if (value)
        {
            data[offset] |= (byte)(1 << bit);   // turn the bit on
        }
        else
        {
            data[offset] &= (byte)~(1 << bit);  // turn the bit off
        }
    }

    // --- Text (S7 String) ------------------------------------------------------

    /// <summary>
    /// Read a Siemens String. Layout: [maxLen][curLen][char0][char1]...
    /// We clamp the length so a corrupt PLC value can't make us read past the end.
    /// </summary>
    public static string GetString(ReadOnlySpan<byte> data, int offset, int maxChars)
    {
        int declaredLength = data[offset + 1]; // byte 1 = current length

        // Safety: never trust the length blindly. Clamp to what actually fits.
        int length = Math.Clamp(declaredLength, 0, maxChars);

        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append((char)data[offset + 2 + i]); // chars start after the 2-byte header
        }
        return sb.ToString();
    }

    /// <summary>
    /// Write a Siemens String. Fills in the 2-byte header then the characters,
    /// truncating if the text is longer than the field allows.
    /// </summary>
    public static void SetString(Span<byte> data, int offset, int maxChars, string value)
    {
        int length = Math.Min(value.Length, maxChars);

        data[offset] = (byte)maxChars;   // byte 0 = declared max length
        data[offset + 1] = (byte)length; // byte 1 = current length

        for (int i = 0; i < length; i++)
        {
            data[offset + 2 + i] = (byte)value[i]; // ASCII characters
        }
        // Any leftover character slots are left as-is; curLen tells readers to stop.
    }
}
