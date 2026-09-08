/* ============================================================================
   01-ccube-schema.sql
   ----------------------------------------------------------------------------
   Creates C-Cube's OWN database table: dbo.ScanHistory - one row per case the
   app answers, for history / audit / reporting.

   HOW TO RUN:
     Connect to the CCube database (the one provisioned for this app) in SQL
     Server Management Studio (SSMS) or Azure Data Studio, then run this script.
     It is idempotent - safe to run more than once (it won't wipe existing data).
   ============================================================================ */

SET NOCOUNT ON;

IF OBJECT_ID('dbo.ScanHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScanHistory
    (
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScanHistory PRIMARY KEY,
        ScannedAtUtc   DATETIME2(3)   NOT NULL,   -- when the app answered (UTC)
        StationId      NVARCHAR(20)   NOT NULL,   -- which station produced this row
        Barcode        NVARCHAR(50)   NOT NULL,   -- the case barcode
        Verdict        NVARCHAR(10)   NOT NULL,   -- 'PASS' or 'REJECT'
        ResultCode     NVARCHAR(30)   NOT NULL,   -- reason name, e.g. 'Ok','NotValid','Timeout'
        IsSystemReject BIT            NOT NULL CONSTRAINT DF_ScanHistory_SysRej DEFAULT (0),
        ProductName    NVARCHAR(100)  NULL,       -- optional, from the validate proc
        BatchNo        NVARCHAR(30)   NULL,        -- optional, from the validate proc
        InExpressMs    INT            NOT NULL CONSTRAINT DF_ScanHistory_InExMs DEFAULT (0),
        TotalMs        INT            NOT NULL CONSTRAINT DF_ScanHistory_TotMs  DEFAULT (0)
    );

    -- Speeds up "today's scans" / date-range reports.
    CREATE INDEX IX_ScanHistory_ScannedAt ON dbo.ScanHistory (ScannedAtUtc);

    PRINT 'Created dbo.ScanHistory.';
END
ELSE
BEGIN
    PRINT 'dbo.ScanHistory already exists - left unchanged.';
END
GO
