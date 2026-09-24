// ScanEngine.cs
// -----------------------------------------------------------------------------
// The background loop that answers each case. Every ~200 ms it:
//   1. Reads the PLC in one consistent snapshot.
//   2. Confirms the PLC is alive (its heartbeat must be advancing).
//   3. Pulses our heartbeat back to the PLC.
//   4. On a new case, decides PASS/REJECT and writes the verdict to the PLC.
//   5. Logs the scan to history (after the verdict, so it never delays the line).
//
// A barcode typed by hand is answered the same way through ManualCheck.
//
// A case passes only on an explicit "valid" answer; a no-read, bad barcode,
// timeout or error all become a REJECT with a specific reason code.
// -----------------------------------------------------------------------------

using CCube.Data;
using CCube.Models;
using CCube.Plc;
using Microsoft.Data.SqlClient;

namespace CCube.Core;

public sealed class ScanEngine : IDisposable
{
    private readonly IPlcLink _plc;
    private readonly IValidator _validator;
    private readonly IHistorySink _history;
    private readonly HealthMonitor _health;
    private readonly string _stationId;
    private readonly MaskingSettings _masking;

    private readonly int _pollMs = 200;
    private readonly int _plcWatchdogMs = 2000;   // PLC is "dead" if its heartbeat freezes this long
    private readonly int _inExpressPingMs = 5000;  // how often to health-check InExpress when idle
    private readonly int _reconnectMs = 3000;      // minimum gap between PLC connect attempts

    private Thread? _thread;
    private volatile bool _running;
    private readonly PcOutputs _outputs = new();

    private short? _lastSeqNo;
    private short _lastHeartbeat;
    private long _lastHeartbeatMovedAt;
    private long _lastPingAt;
    private long _lastConnectAt;
    private volatile bool _connecting;
    private bool _validatorHealthy = true;

    /// <summary>Raised when a case has been answered.</summary>
    public event Action<ScanRecord>? ScanCompleted;

    public ScanEngine(IPlcLink plc, IValidator validator, IHistorySink history,
        HealthMonitor health, string stationId, MaskingSettings? masking = null)
    {
        _plc = plc;
        _validator = validator;
        _history = history;
        _health = health;
        _stationId = stationId;
        _masking = masking ?? new MaskingSettings();
    }

    public void Start()
    {
        if (_running)
        {
            return;
        }
        _running = true;
        _thread = new Thread(Loop) { IsBackground = true, Name = "CCube-ScanEngine" };
        _thread.Start();
        AppLog.Info($"Scan engine started (station '{_stationId}', poll {_pollMs}ms).");
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }
        _running = false;
        _thread?.Join(millisecondsTimeout: 1500);
        AppLog.Info("Scan engine stopped.");
    }

    public void Dispose() => Stop();

    // ---- The loop -------------------------------------------------------------

    private void Loop()
    {
        _lastHeartbeatMovedAt = Environment.TickCount64;
        while (_running)
        {
            try
            {
                Tick();
            }
            catch (Exception ex)
            {
                AppLog.Error("Scan engine loop error.", ex);
                _health.SetPlc(Health.Down, "LINK LOST");
                _health.SetScanner(Health.Down);
            }

            Thread.Sleep(_pollMs);
        }
    }

    private void Tick()
    {
        long now = Environment.TickCount64;

        // Keep the InExpress light current.
        if (now - _lastPingAt > _inExpressPingMs)
        {
            _lastPingAt = now;
            _validatorHealthy = _validator.Ping();
            _health.SetInExpress(_validatorHealthy ? Health.Up : Health.Down);
        }

        if (!_plc.IsConnected)
        {
            // Connect in the background so a missing/slow PLC never blocks the loop.
            if (!_connecting && now - _lastConnectAt >= _reconnectMs)
            {
                _connecting = true;
                _lastConnectAt = now;
                Task.Run(() =>
                {
                    try { _plc.EnsureConnected(); }
                    finally { _connecting = false; }
                });
            }
            _health.SetPlc(Health.Down, "NO PLC");
            _health.SetScanner(Health.Down);
            return;
        }

        PlcInputs inputs = _plc.ReadInputs();

        if (inputs.PlcHeartbeat != _lastHeartbeat)
        {
            _lastHeartbeat = inputs.PlcHeartbeat;
            _lastHeartbeatMovedAt = now;
        }
        bool plcAlive = (now - _lastHeartbeatMovedAt) < _plcWatchdogMs;
        _health.SetPlc(plcAlive ? Health.Up : Health.Down, plcAlive ? "" : "NOT RESPONDING");
        _health.SetScanner(plcAlive ? Health.Up : Health.Down);

        _outputs.PcHeartbeat = unchecked((short)(_outputs.PcHeartbeat + 1));
        _outputs.PcOnline = _validatorHealthy;
        _plc.WriteOutputs(_outputs);

        if (_lastSeqNo is null)
        {
            _lastSeqNo = inputs.SeqNo; // adopt the current value; don't treat startup as a case
            return;
        }

        if (plcAlive && _lastSeqNo != inputs.SeqNo)
        {
            _lastSeqNo = inputs.SeqNo;
            ProcessCase(inputs);
        }
    }

    // ---- Answering a case from the PLC ---------------------------------------

    private void ProcessCase(PlcInputs inputs)
    {
        long startedAt = Environment.TickCount64;
        string barcode = inputs.Barcode?.Trim() ?? "";

        (ResultCode code, CaseResult? info, int inExpressMs) =
            DecideVerdict(barcode, inputs.ScannerNoRead, $"seq {inputs.SeqNo}");

        // Machine # masking: if this case's machine is masked, force the PLC signal
        // to GOOD so the case is physically kept, whatever the real result was. Only
        // the PLC signal changes - history/display below still record the real result.
        bool masked = IsMachineMasked(barcode);
        ResultCode plcCode = masked ? ResultCode.Ok : code;
        if (masked && !code.IsPass())
        {
            AppLog.Info($"Case seq {inputs.SeqNo}: machine # masked -> PLC told GOOD "
                + $"(actual result {code}). Logged as-is.");
        }

        try
        {
            _outputs.ResultValid = true;
            _outputs.ResultGood = plcCode.IsPass();
            _outputs.ResultCode = plcCode;
            _outputs.PcOnline = _validatorHealthy;
            _outputs.AckSeqNo = inputs.SeqNo; // written last = the verdict is ready
            _plc.WriteOutputs(_outputs);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Case seq {inputs.SeqNo}: FAILED to write verdict to PLC.", ex);
        }

        Report(barcode, code, info, inExpressMs, startedAt, $"Case seq {inputs.SeqNo}");
    }

    // ---- Answering a barcode typed by hand -----------------------------------

    /// <summary>Validate a barcode entered by an operator. It logs and shows on
    /// screen but writes nothing to the PLC.</summary>
    public void ManualCheck(string barcode, bool scannerNoRead)
    {
        long startedAt = Environment.TickCount64;
        barcode = barcode?.Trim() ?? "";

        (ResultCode code, CaseResult? info, int inExpressMs) =
            DecideVerdict(barcode, scannerNoRead, "manual");

        Report(barcode, code, info, inExpressMs, startedAt, "Manual check");
    }

    // ---- Machine # masking ---------------------------------------------------

    // True if this case's machine (case-packer number, parsed from the barcode) is
    // one the operator ticked to mask. A no-read/short barcode has no machine # to
    // read, so it's never masked (handled as usual).
    private bool IsMachineMasked(string barcode)
    {
        if (!_masking.Enabled || _masking.MaskedMachines.Count == 0)
        {
            return false;
        }
        string? casePacker = BarcodeFields.CasePacker(barcode);
        return int.TryParse(casePacker, out int machine) && _masking.IsMasked(machine);
    }

    // ---- Deciding the verdict ------------------------------------------------

    private (ResultCode code, CaseResult? info, int inExpressMs) DecideVerdict(
        string barcode, bool scannerNoRead, string context)
    {
        if (scannerNoRead)
        {
            AppLog.Warn($"{context}: scanner NO-READ -> REJECT.");
            return (ResultCode.ScannerNoRead, null, 0);
        }

        if (!BarcodeRules.IsPlausible(barcode))
        {
            AppLog.Warn($"{context}: implausible barcode '{barcode}' -> REJECT.");
            return (ResultCode.BadBarcode, null, 0);
        }

        long askedAt = Environment.TickCount64;
        try
        {
            CaseResult info = _validator.Validate(barcode);
            int ms = (int)(Environment.TickCount64 - askedAt);
            _validatorHealthy = true;
            _health.SetInExpress(Health.Up);
            return (info.IsValid ? ResultCode.Ok : ResultCode.NotValid, info, ms);
        }
        catch (SqlException ex) when (ex.Number == -2) // SQL command timeout
        {
            int ms = (int)(Environment.TickCount64 - askedAt);
            _validatorHealthy = false;
            _health.SetInExpress(Health.Down);
            AppLog.Error($"{context}: InExpress TIMEOUT -> REJECT.", ex);
            return (ResultCode.Timeout, null, ms);
        }
        catch (Exception ex)
        {
            int ms = (int)(Environment.TickCount64 - askedAt);
            _validatorHealthy = false;
            _health.SetInExpress(Health.Down);
            AppLog.Error($"{context}: InExpress ERROR -> REJECT.", ex);
            return (ResultCode.Error, null, ms);
        }
    }

    // ---- Recording the result ------------------------------------------------

    private void Report(string barcode, ResultCode code, CaseResult? info,
        int inExpressMs, long startedAt, string context)
    {
        int totalMs = (int)(Environment.TickCount64 - startedAt);
        var record = new ScanRecord
        {
            ScannedAtUtc = DateTime.UtcNow,
            StationId = _stationId,
            Barcode = barcode,
            ResultCode = code,
            // From InExpress (the validate proc):
            InExpressId = info?.Id,
            ProductDescription = info?.ProductDescription,
            FaCode = info?.FaCode,
            // Parsed from the barcode (display/history only - see BarcodeFields):
            CasePacker = BarcodeFields.CasePacker(barcode),
            ProductionAt = BarcodeFields.ProductionDateTime(barcode),
            InExpressMs = inExpressMs,
            TotalMs = totalMs,
        };
        AppLog.Info($"{context}: {record.Verdict} ({code}) '{barcode}' in {totalMs}ms.");

        try
        {
            ScanCompleted?.Invoke(record);
        }
        catch (Exception ex)
        {
            AppLog.Error("A ScanCompleted handler threw.", ex);
        }

        // History runs after the verdict and off this thread, so a slow database
        // can never delay the next case.
        Task.Run(() =>
        {
            try
            {
                _history.Save(record);
            }
            catch (Exception ex)
            {
                AppLog.Error("Failed to save scan to history.", ex);
            }
        });
    }
}
