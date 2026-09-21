/* ============================================================================
   01-ccube-schema.sql
   ----------------------------------------------------------------------------
   Creates C-Cube's OWN history database (test database: CCube2).
   One row per case the app answers, for history / audit / reporting. A copy of
   every row is ALSO written to a daily CSV file on disk (a second backup the
   boss asked for) - see Data/CsvHistoryRepository.cs.

   The columns mirror what the operator sees in the Case Logs grid:
       InExpressId, StationId, Barcode, CasePacker, ProductDescription, FaCode,
       ProductionAt, ValidationResult (+ ResultCode reason + ScannedAtUtc).

   >>> WARNING: this script DROPS and recreates dbo.ScanHistory. <<<
   The column set changed, so it cannot be migrated in place here. Only run it on
   a test/empty database, or archive existing rows first.

   HOW TO RUN: connect to the CCube2 (or the plant CCube) database in SSMS / Azure
   Data Studio and run the whole script.
   ============================================================================ */

SET NOCOUNT ON;
GO

/* ---- Remove leftover objects from earlier compliance-cache experiments ------ */
IF OBJECT_ID('dbo.CaseCompliance', 'U') IS NOT NULL
    DROP TABLE dbo.CaseCompliance;
IF OBJECT_ID('dbo.SyncCursor', 'U') IS NOT NULL
    DROP TABLE dbo.SyncCursor;
GO

/* ---- The scan history table ------------------------------------------------- */
IF OBJECT_ID('dbo.ScanHistory', 'U') IS NOT NULL
    DROP TABLE dbo.ScanHistory;
GO

CREATE TABLE dbo.ScanHistory
(
    Id                 BIGINT IDENTITY(1,1) NOT NULL
        CONSTRAINT PK_ScanHistory PRIMARY KEY,        -- our own row id
    ScannedAtUtc       DATETIME2(3)  NOT NULL,        -- when the app received the case (UTC)
    StationId          NVARCHAR(20)  NOT NULL,        -- which station produced this row
    Barcode            NVARCHAR(50)  NOT NULL,        -- the case barcode (from scanner/PLC)
    CasePacker         NVARCHAR(10)  NULL,            -- machine # parsed from the barcode
    InExpressId        INT           NULL,            -- Id returned by the InExpress proc
    ProductDescription NVARCHAR(100) NULL,            -- from the InExpress proc
    FaCode             NVARCHAR(30)  NULL,            -- from the InExpress proc
    ProductionAt       DATETIME2(3)  NULL,            -- production datetime parsed from the barcode
    ValidationResult   NVARCHAR(10)  NOT NULL,        -- 'Good' or 'Reject'
    ResultCode         NVARCHAR(30)  NOT NULL         -- reason name, e.g. 'Ok','NotValid','Timeout'
        CONSTRAINT DF_ScanHistory_ResultCode DEFAULT ('Ok')
);
GO

-- Speeds up "today's scans" / date-range reports.
CREATE INDEX IX_ScanHistory_ScannedAt ON dbo.ScanHistory (ScannedAtUtc);
GO

PRINT 'Created dbo.ScanHistory.';
GO
