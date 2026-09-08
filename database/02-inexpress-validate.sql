/* ============================================================================
   02-inexpress-validate.sql
   ----------------------------------------------------------------------------
   REFERENCE STUB of the InExpress validation stored procedure.

   >>> This is a PLACEHOLDER. PMI owns the real logic. <<<
   The boss's instruction: the app passes ONLY the barcode; the procedure does
   all the work (aggregation / compliance checks) and returns a simple answer.

   The CONTRACT the C-Cube app depends on (keep this stable):
       EXEC dbo.usp_ValidateCase @Barcode = '<the case barcode>';
   must return exactly ONE row with:
       IsValid      BIT           1 = valid (PASS), 0 = invalid (REJECT)   [required]
       ProductName  NVARCHAR      optional, for display + history
       BatchNo      NVARCHAR      optional, for display + history
       Reason       NVARCHAR      optional, human-readable explanation

   The body below is only so the app can be demonstrated end-to-end against a
   test database. Replace the marked section with the real logic.

   HOW TO RUN: connect to the InExpress (or a test) database and run this script.
   ============================================================================ */

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ValidateCase
    @Barcode NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    /* ---- BEGIN placeholder logic (PMI to replace) -------------------------
       Demo behaviour so testing is predictable:
         - barcode contains 'REJECT' or 'FAIL'  -> invalid
         - otherwise                            -> valid
    ------------------------------------------------------------------------ */
    DECLARE @IsValid BIT = 1;
    IF @Barcode LIKE '%REJECT%' OR @Barcode LIKE '%FAIL%'
        SET @IsValid = 0;

    SELECT
        @IsValid                                             AS IsValid,
        'Sample Product'                                     AS ProductName,
        'BATCH-0001'                                         AS BatchNo,
        CASE WHEN @IsValid = 1 THEN 'OK' ELSE 'Not valid' END AS Reason;
    /* ---- END placeholder logic ------------------------------------------- */
END
GO

PRINT 'Created/updated dbo.usp_ValidateCase (reference stub).';
GO
