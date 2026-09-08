// Db100Layout.cs
// -----------------------------------------------------------------------------
// THE PLC CONTRACT. This file is the single source of truth for where each
// piece of information lives inside the shared PLC data block - which byte, and
// for bits, which bit in that byte.
//
// The app and the PLC program (written in TIA Portal by PMI) must AGREE on these
// numbers exactly, like two people using the same seating chart. If PMI's real
// data block uses different offsets, you change them HERE (one place) and the
// rest of the app follows automatically.
//
// The block is read and written as ONE chunk of bytes so that everything we read
// is a single, consistent snapshot (never a fresh sequence number sitting next
// to a stale barcode).
//
//   PLC -> PC (we read these):        PC -> PLC (we write these):
//     0  SeqNo        (Int, 2)          48  AckSeqNo     (Int, 2)
//     2  Barcode      (String[40])      50.0 ResultValid (Bool)
//     44 PlcHeartbeat (Int, 2)          50.1 ResultGood  (Bool)
//     46.0 ScannerNoRead (Bool)         52  ResultCode   (Int, 2)
//     46.1 PlcReady   (Bool)            54  PcHeartbeat  (Int, 2)
//                                       56.0 PcOnline     (Bool)
//
// TIA Portal requirements for this to work over S7.NET plus:
//   * On this data block, UNCHECK "Optimized block access" (so absolute byte
//     addressing like DBB2 is valid).
//   * Enable PUT/GET communication on the CPU.
// -----------------------------------------------------------------------------

namespace CCube.Plc;

public static class Db100Layout
{
    // The whole block is 58 bytes (offsets 0..57). We always read/write all of it
    // (for reads) or the PC-owned slice (for writes).
    public const int BlockSizeBytes = 58;

    // The barcode field holds up to 40 characters. An S7 String needs 2 extra
    // header bytes, so it occupies 42 bytes total (offsets 2..43).
    public const int BarcodeMaxLength = 40;

    // ---- Fields the PLC writes and we READ ------------------------------------
    public const int SeqNo = 0;          // Int  : bumped when a new case arrives
    public const int Barcode = 2;        // String[40]
    public const int PlcHeartbeat = 44;  // Int  : counts up while the PLC is alive
    public const int FlagsIn = 46;       // byte holding the two input bits below
    public const int ScannerNoReadBit = 0; // FlagsIn.0 : scanner failed to read
    public const int PlcReadyBit = 1;       // FlagsIn.1 : PLC is in ready/run state

    // ---- Fields we WRITE and the PLC reads ------------------------------------
    public const int AckSeqNo = 48;      // Int  : echo of SeqNo (written LAST)
    public const int FlagsOut = 50;      // byte holding the two result bits below
    public const int ResultValidBit = 0; //   FlagsOut.0 : the verdict is meaningful
    public const int ResultGoodBit = 1;  //   FlagsOut.1 : true = PASS, false = REJECT
    public const int ResultCode = 52;    // Int  : reason code (see ResultCode.cs)
    public const int PcHeartbeat = 54;   // Int  : our liveness pulse
    public const int FlagsOnline = 56;   // byte holding PcOnline
    public const int PcOnlineBit = 0;    //   FlagsOnline.0 : alive AND able to validate

    // The PC-owned region is bytes 48..56. When we send our answer we write ONLY
    // this slice, so we never overwrite the PLC's own barcode/seqno with zeros.
    public const int PcRegionStart = AckSeqNo;                 // 48
    public const int PcRegionLength = (FlagsOnline + 1) - AckSeqNo; // 56+1-48 = 9
}
