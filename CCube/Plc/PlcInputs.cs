// PlcInputs.cs
// -----------------------------------------------------------------------------
// A tidy, read-only snapshot of everything the PLC tells us in one read.
//
// The app receives PlcInputs and only ever READS them - it has no way to write
// back into the PLC-owned fields through this type. (We answer via PcOutputs.)
// This "can only read inputs, can only write outputs" split makes it impossible
// to accidentally clobber a field the PLC owns.
//
// A "record" is just a small class for holding data; this one is immutable.
// -----------------------------------------------------------------------------

namespace CCube.Plc;

public sealed record PlcInputs(
    short SeqNo,          // changes when a new case is presented
    string Barcode,       // the case barcode the scanner wrote into the PLC
    short PlcHeartbeat,   // counts up while the PLC is alive
    bool ScannerNoRead,   // the scanner failed to read this case
    bool PlcReady);       // the PLC is in ready/run state
