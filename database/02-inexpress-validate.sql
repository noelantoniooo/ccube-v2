/* ============================================================================
   02-inexpress-validate.sql
   ----------------------------------------------------------------------------
   DUMMY InExpress simulation for local development/testing (database: InExpress2).

   >>> This whole script is a PLACEHOLDER. PMI owns the real InExpress. <<<
   The boss's instruction: the C-Cube app passes ONLY the barcode; InExpress does
   all the work and returns a small, fixed answer.

   The CONTRACT the C-Cube app depends on (keep this stable - this is what PMI
   must match):

       EXEC dbo.usp_ValidateCase @Barcode = '<the case barcode>';

   must return exactly ONE row with these columns:
       Id                 INT           the InExpress row id for this case   [nullable]
       ProductDescription NVARCHAR      product description (for display + history)
       FaCode             NVARCHAR      FA code             (for display + history)
       IsValid            BIT           1 = valid (PASS), 0 = invalid (REJECT) [required]

   Anything else (a missing row, an error, a timeout) makes the app REJECT the
   case - silence is never a PASS. Column names are matched case-insensitively.

   HOW TO RUN: connect to the InExpress2 test database and run this whole script.
   It (1) removes leftover objects from earlier experiments, (2) creates a small
   seeded dbo.CaseData table so scans return realistic data, and (3) creates the
   dummy usp_ValidateCase that looks a barcode up in that table.
   ============================================================================ */

SET NOCOUNT ON;
GO

/* ---- 1. Remove leftover objects from earlier compliance-cache experiments --- */
IF OBJECT_ID('FS.GetLastShippingCaseComplianceStatuses', 'P') IS NOT NULL
    DROP PROCEDURE FS.GetLastShippingCaseComplianceStatuses;
IF OBJECT_ID('intobj.ConsolidatedHierarchies', 'U') IS NOT NULL
    DROP TABLE intobj.ConsolidatedHierarchies;
IF OBJECT_ID('intobj.ConsolidatedHierarchiesArchive', 'U') IS NOT NULL
    DROP TABLE intobj.ConsolidatedHierarchiesArchive;
GO

/* ---- 2. The dummy "known cases" table -------------------------------------- */
IF OBJECT_ID('dbo.CaseData', 'U') IS NOT NULL
    DROP TABLE dbo.CaseData;
GO

CREATE TABLE dbo.CaseData
(
    Id                 INT IDENTITY(1001,1) NOT NULL
        CONSTRAINT PK_CaseData PRIMARY KEY,
    Barcode            NVARCHAR(50)  NOT NULL
        CONSTRAINT UQ_CaseData_Barcode UNIQUE,
    ProductDescription NVARCHAR(100) NOT NULL,
    FaCode             NVARCHAR(30)  NOT NULL,
    IsValid            BIT           NOT NULL
);
GO

/* Seeded 40-character barcodes. The field positions match v1's CBL layout so the
   app's client-side parse produces sensible values:
       chars 23-24 = year (yy), 25-26 = month, 27-28 = day
       chars 33-34 = case-packer (machine) number
       chars 35-36 = hour, 37-38 = minute, 39-40 = second
   e.g. '...250921...07 1430 05' -> CP 07, produced 2025-09-21 14:30:05.          */
INSERT INTO dbo.CaseData (Barcode, ProductDescription, FaCode, IsValid) VALUES
    ('0110461200000001000001250921000007143005', 'Marlboro Red KS 20s',    '10461200', 1),
    ('0110461200000002000002250921000012150522', 'Marlboro Gold KS 20s',   '10461201', 1),
    ('0110461200000003000003250920000007221240', 'Philip Morris Blue 20s', '10461202', 1),
    ('0110461200000004000004250921000012034711', 'Marlboro Red KS 20s',    '10461200', 0),
    ('0110461200000005000005250921000003093000', 'Fortune Red 20s',        '10461205', 1);
GO

PRINT 'Created and seeded dbo.CaseData (5 dummy cases).';
GO

/* ---- 3. The dummy validate proc -------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_ValidateCase
    @Barcode NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    /* Known barcode -> return its real row. */
    IF EXISTS (SELECT 1 FROM dbo.CaseData WHERE Barcode = @Barcode)
    BEGIN
        SELECT Id, ProductDescription, FaCode, IsValid
        FROM   dbo.CaseData
        WHERE  Barcode = @Barcode;
        RETURN;
    END

    /* Unknown barcode -> NOT valid. A case InExpress doesn't recognise must be
       REJECTED (fail-safe), exactly as PMI's real proc would. We still return the
       one-row shape the app expects, just with no product data and IsValid = 0. */
    SELECT
        CAST(NULL AS INT)            AS Id,
        CAST(NULL AS NVARCHAR(100))  AS ProductDescription,
        CAST(NULL AS NVARCHAR(30))   AS FaCode,
        CAST(0 AS BIT)               AS IsValid;
END
GO

PRINT 'Created/updated dbo.usp_ValidateCase (dummy simulation).';
GO
