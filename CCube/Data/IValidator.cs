// IValidator.cs
// -----------------------------------------------------------------------------
// The contract for asking whether a case is valid. CaseResult is the answer the
// InExpress validate proc gives back for one barcode:
//     Id                 the InExpress row id for the case (null if unknown)
//     ProductDescription product description, for display + history
//     FaCode             FA code, for display + history
//     IsValid            true = PASS, false = REJECT
// (This matches the SP contract PMI is providing - see database/02-*.sql.)
// -----------------------------------------------------------------------------

namespace CCube.Data;

public sealed record CaseResult(
    bool IsValid,
    int? Id = null,
    string? ProductDescription = null,
    string? FaCode = null);

public interface IValidator
{
    /// <summary>Ask whether this barcode is a valid case. May throw on DB errors;
    /// the scan engine catches those and turns them into a safe REJECT.</summary>
    CaseResult Validate(string barcode);

    /// <summary>Health probe: can we reach the validation source right now?
    /// Never throws - returns false if unreachable.</summary>
    bool Ping();
}
