-- ============================================================
-- Migration squash upgrade script
-- Run ONCE on any database that was running the pre-squash migrations,
-- BEFORE deploying the new application binary.
--
-- This replaces the 12 active Data/Migrations entries (and any surviving
-- rows from the old orphaned Infrastructure/Migrations series) with the
-- single new InitialCreate entry.
--
-- The script is idempotent — safe to re-run.
-- Replace '20260423122113' below if your generated timestamp differs.
-- ============================================================

BEGIN;

-- Remove the 12 active Data/Migrations history entries
DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" IN (
    '20260404023955_UnifiedFinancialConnectivityLayer',
    '20260406080635_AddCtrlValueAgentModule',
    '20260406090917_AddAgentSavingsSnapshot',
    '20260406092152_AddAgentSetting',
    '20260406092656_AddAgentScenarioAndSetting',
    '20260406093114_AddAgentDigestEmail',
    '20260407075358_AddDemoEntityFlag',
    '20260414121403_AddAccountKeywordRules',
    '20260418033246_FixAgentPermissionsAndFlags',
    '20260421112713_RemovePlaidBasiq',
    '20260422090926_Remove2FA'
);

-- Also remove any rows from the old orphaned Infrastructure/Migrations series
-- (Feb–Apr 2026 series that was superseded by the unified architecture)
DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" IN (
    '20260212083308_InitialCreate',
    '20260213001646_AddSoftDeleteSupport',
    '20260214091924_ComprehensiveSchemaEvolution',
    '20260218072755_AddCurrentBalanceToAccount',
    '20260222062201_AddEmailVerificationAnd2FA',
    '20260222064427_AddEmailVerificationAnd2FA_2',
    '20260301073723_AddQifImport',
    '20260301102000_AddAlreadyImportedRows',
    '20260301102256_AddAlreadyImportedRowsToImportedTransactionsFiles',
    '20260301103348_UpdateStagingDuplicateIndex',
    '20260302071310_AddOfxColumnsToStagingAndFitId',
    '20260302082633_FixOFXIndexError',
    '20260303075405_AdminDashboardMultiTenant',
    '20260315005740_AddPasswordResetToUsers',
    '20260315043533_SingleAccountModel',
    '20260316080927_AddCategoryKeywordRules',
    '20260322020537_AddStartingBalanceToAccount',
    '20260322024536_AddLoanDetailsAndRateHistory',
    '20260326082920_AddInvestingExtensions',
    '20260328023404_AddAuditLogs',
    '20260328024510_AddUserAccountLockout',
    '20260328045241_AddCustomRolePermissions',
    '20260328080608_AddOnboardingCompletedAt',
    '20260329001346_AddUserDeletionRequests',
    '20260401090511_AddBondEtfMetadata',
    '20260403000448_AddIntegrations',
    '20260404010701_AddBasiqAndEntityCountry',
    '20260406000001_AddTickerInfrastructure'
);

-- Mark the squashed migration as already applied so EF does not re-run it
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260423123352_InitialCreate', '8.0.11')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
