// PcOutputs.cs
// -----------------------------------------------------------------------------
// Everything WE send back to the PLC in one write: the verdict and our liveness.
//
// This is the only shape the app can write to the PLC. Its fields map one-to-one
// onto the PC-owned bytes in the data block (see Db100Layout).
// -----------------------------------------------------------------------------

namespace CCube.Plc;

public sealed class PcOutputs
{
    public short AckSeqNo { get; set; }      // echo the case's SeqNo (written LAST)
    public bool ResultValid { get; set; }    // this verdict is meaningful
    public bool ResultGood { get; set; }     // true = PASS, false = REJECT
    public ResultCode ResultCode { get; set; } // the reason for the verdict
    public short PcHeartbeat { get; set; }   // our liveness pulse (counts up)
    public bool PcOnline { get; set; }       // alive AND able to validate
}
