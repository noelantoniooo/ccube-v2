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

// Pulls fields out of the CBL barcode. Kept separate so the "meaning" of barcode
// positions lives in one place (confirm the real layout with PMI).
public static class BarcodeFields
{
    /// <summary>
    /// The case-packer (machine) number: characters 33-34 (1-based) of the
    /// 40-character barcode, i.e. zero-based Substring(32, 2). Falls back to the
    /// first two characters for a shorter barcode so the grid still shows a row.
    /// </summary>
    public static string CasePackerId(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return "—";
        }
        if (barcode.Length >= 34)
        {
            return barcode.Substring(32, 2);
        }
        return barcode.Length >= 2 ? barcode[..2] : barcode;
    }
}
