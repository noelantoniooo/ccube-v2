// Validation.cs
// -----------------------------------------------------------------------------
// Cheap, local checks on a barcode BEFORE we bother asking InExpress.
//
// This is deliberately structural, not format-specific: we don't yet know the
// exact CBL barcode format (an open item with PMI), so we only reject the
// obviously-broken cases (empty, too short, too long, contains control chars).
// The real "is this a valid case?" decision is InExpress's job.
//
// Note: this is a convenience filter, NOT our security boundary. The real
// protection against bad/hostile input is that every SQL call uses parameters
// (see Data/InExpressValidator.cs).
// -----------------------------------------------------------------------------

using CCube.Plc;

namespace CCube.Core;

public static class BarcodeRules
{
    // Shortest believable barcode. Anything shorter is almost certainly garbage.
    public const int MinLength = 4;

    // Longest we can even store (the PLC String field caps it at 40).
    public static int MaxLength => Db100Layout.BarcodeMaxLength;

    /// <summary>True if the barcode looks like a real barcode worth checking.</summary>
    public static bool IsPlausible(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return false;
        }

        if (barcode.Length < MinLength || barcode.Length > MaxLength)
        {
            return false;
        }

        foreach (char c in barcode)
        {
            if (char.IsControl(c)) // tabs, newlines, stray non-printing bytes
            {
                return false;
            }
        }

        return true;
    }
}

// Pulls display fields OUT of the CBL barcode, client-side, exactly like the v1
// app did. IMPORTANT: this is for DISPLAY and HISTORY only - it is NOT validation.
// Whether a case passes is InExpress's decision (the validate proc). We only chop
// the barcode up so the Case Logs grid can show the machine number and production
// time next to the InExpress result.
//
// The field positions below are v1's CBL layout (1-based character numbers).
// Confirm the real positions with PMI before plant deployment.
//   chars 23-24 = production year (yy)   25-26 = month   27-28 = day
//   chars 33-34 = case-packer (machine) number
//   chars 35-36 = hour   37-38 = minute   39-40 = second
// Everything returns null when the barcode is too short / not a full 40-char CBL,
// so a manual or garbled scan simply shows a blank cell instead of a wrong value.
public static class BarcodeFields
{
    /// <summary>
    /// The case-packer (machine) number: characters 33-34 (1-based), i.e.
    /// zero-based Substring(32, 2). Null if the barcode is shorter than that.
    /// </summary>
    public static string? CasePacker(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode) || barcode.Length < 34)
        {
            return null;
        }
        return barcode.Substring(32, 2);
    }

    /// <summary>
    /// The production date+time encoded in the barcode (v1 positions). Null if the
    /// barcode isn't a full 40-char CBL or the digits don't form a real date.
    /// </summary>
    public static DateTime? ProductionDateTime(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode) || barcode.Length < 40)
        {
            return null;
        }

        // Mid() in v1 is 1-based; Substring here is 0-based (position - 1).
        string yy = barcode.Substring(22, 2);   // chars 23-24
        string mm = barcode.Substring(24, 2);   // chars 25-26
        string dd = barcode.Substring(26, 2);   // chars 27-28
        string hh = barcode.Substring(34, 2);   // chars 35-36
        string mi = barcode.Substring(36, 2);   // chars 37-38
        string ss = barcode.Substring(38, 2);   // chars 39-40

        // Exact format, invariant culture; TryParseExact fails cleanly on garbage.
        string text = $"20{yy}-{mm}-{dd} {hh}:{mi}:{ss}";
        if (DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime produced))
        {
            return produced;
        }
        return null;
    }
}
