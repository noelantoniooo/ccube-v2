// IValidator.cs
// -----------------------------------------------------------------------------
// The contract for asking whether a case is valid. CaseResult is the answer:
// valid or not, plus optional details the stored proc may return for display
// and history.
// -----------------------------------------------------------------------------

namespace CCube.Data;

public sealed record CaseResult(
    bool IsValid,
    string? ProductName = null,
    string? BatchNo = null,
    string? Reason = null);

public interface IValidator
{
    /// <summary>Ask whether this barcode is a valid case. May throw on DB errors;
    /// the scan engine catches those and turns them into a safe REJECT.</summary>
    CaseResult Validate(string barcode);

    /// <summary>Health probe: can we reach the validation source right now?
    /// Never throws - returns false if unreachable.</summary>
    bool Ping();
}
