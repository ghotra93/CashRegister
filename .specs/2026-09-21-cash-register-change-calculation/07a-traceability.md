# Traceability: 2026-09-21-cash-register-change-calculation

> Generated 2026-09-21 by `.github/scripts/traceability-dotnet.sh`.
> Sources: `[Trait("AC", "AC-NNN")]` on xUnit tests; `[AC-NNN]` prefixes on Vitest test names; `acs_covered` in `.tdd-state.json`.

## Summary

- Active ACs: 28 (AC-014 withdrawn → DC-001)
- ACs with at least one tagged test: 28
- Uncovered ACs: none
- Orphaned tags (tag names a non-active AC): none

## Matrix

| AC | Tasks | Tests | Tagged tests (first 4) |
|---|---|---|---|
| AC-001 | T-008, T-010, T-012 | 3 | `FileEndpointsTests.Upload_ReadmeSample_Returns201WithOneLinePerTransaction`<br>`FileUploadPerformanceTests.Upload_1000LineFile_P95IsUnder500Milliseconds`<br>`ChangeFileProcessorTests.Process_ReadmeSample_ProducesOneOutputLinePerTransaction` |
| AC-002 | T-008 | 1 | `ChangeFileProcessorTests.Process_OutputLines_KeepTheInputOrder` |
| AC-022 | T-008 | 1 | `ChangeFileProcessorTests.Process_BlankAndWhitespaceLines_AreIgnored` |
| AC-023 | T-008, T-010, T-015, T-017 | 5 | `FileEndpointsTests.Upload_MoreThan1000Lines_Returns400TooManyLines`<br>`FileEndpointsTests.Upload_MoreThan1000Lines_ReturnsTooManyLinesProblemAndStoresNothing`<br>`ChangeFileProcessorTests.Process_ExactlyTheLineLimit_IsProcessed`<br>`ChangeFileProcessorTests.Process_OneLineOverTheLimit_IsRejected`<br>… +1 more |
| AC-003 | T-003, T-005 | 4 | `ChangeCalculatorTests.Calculate_NoRuleMatches_UsesMinimalChange`<br>`MinimalChangeStrategyTests.MakeChange_88Cents_Is3Quarters1Dime3Pennies`<br>`MinimalChangeStrategyTests.MakeChange_3Cents_Is3Pennies`<br>`MinimalChangeStrategyTests.MakeChange_167Cents_Is1Dollar2Quarters1Dime1Nickel2Pennies` |
| AC-004 | T-004, T-005 | 4 | `ChangeCalculatorTests.Calculate_OwedDivisibleByDivisor_UsesRandomChange`<br>`OwedDivisibleByRuleTests.Matches_OwedCentsDivisibleByThree_IsTrue`<br>`OwedDivisibleByRuleTests.Matches_OwedCentsNotDivisibleByThree_IsFalse`<br>`RandomChangeStrategyTests.MakeChange_DifferentSeeds_ProduceDifferentDenominations` |
| AC-005 | T-003, T-004 | 3 | `TransactionTests.ChangeDue_IsPaidMinusOwed`<br>`MinimalChangeStrategyTests.MakeChange_AnyAmountUpTo1000Cents_SumsExactlyToAmount`<br>`RandomChangeStrategyTests.MakeChange_ManySeedsAndAmounts_AlwaysSumsExactlyToAmount` |
| AC-013 | T-005 | 1 | `OwedDivisibleByRuleTests.Matches_UsesTheActiveDivisor` |
| AC-024 | T-005, T-012 | 4 | `ArchitectureTests.FeatureSlices_AreFreeOfCycles`<br>`ArchitectureTests.ChangeRules_AreSealedAndLiveInTheRulesNamespace`<br>`ChangeCalculatorTests.Calculate_SeveralRulesMatch_LowestPriorityValueWins`<br>`ChangeCalculatorTests.Calculate_SamePriority_FirstRegisteredWins` |
| AC-016 | T-006 | 2 | `ChangeFormatterTests.Format_NoLines_IsNoChange`<br>`ChangeFormatterTests.Format_OnlyZeroCountLines_IsNoChange` |
| AC-006 | T-006 | 1 | `ChangeFormatterTests.Format_SeveralDenominations_IsCommaSeparatedCountAndName` |
| AC-007 | T-006 | 4 | `ChangeFormatterTests.Format_UnorderedLines_ListsLargestDenominationFirst`<br>`RandomChangeStrategyTests.MakeChange_LinesAreLargestFirstWithoutZeroCounts`<br>`UsdCurrencyTests.Denominations_AreDollarQuarterDimeNickelPenny_LargestFirst`<br>`UsdCurrencyTests.Constructor_UnorderedDenominations_StoresThemLargestFirst` |
| AC-008 | T-006 | 1 | `ChangeFormatterTests.Format_ZeroCountLine_IsOmitted` |
| AC-009 | T-002, T-006 | 2 | `ChangeFormatterTests.Format_CountOfOne_UsesSingularName`<br>`DenominationTests.NameFor_CountOfOne_ReturnsSingularName` |
| AC-010 | T-002, T-006 | 2 | `ChangeFormatterTests.Format_CountAboveOne_UsesPluralName`<br>`DenominationTests.NameFor_CountGreaterThanOne_ReturnsPluralName` |
| AC-011 | T-006, T-008 | 2 | `ChangeFormatterTests.Format_MinimalChangeFor2_12Paid3_00_MatchesReadmeSample`<br>`ChangeFileProcessorTests.Process_ReadmeSample_FirstLineMatchesReadme` |
| AC-012 | T-006, T-008 | 2 | `ChangeFormatterTests.Format_MinimalChangeFor1_97Paid2_00_MatchesReadmeSample`<br>`ChangeFileProcessorTests.Process_ReadmeSample_SecondLineMatchesReadme` |
| AC-017 | T-007 | 1 | `TransactionLineParserTests.Parse_Malformed_ReturnsInvalidLineError` |
| AC-018 | T-007 | 1 | `TransactionLineParserTests.Parse_NegativeAmount_ReturnsNegativeError` |
| AC-019 | T-007 | 1 | `TransactionLineParserTests.Parse_PaidLessThanOwed_ReturnsPaidLessError` |
| AC-020 | T-007 | 1 | `TransactionLineParserTests.Parse_TooManyDecimalPlaces_ReturnsDecimalsError` |
| AC-021 | T-008 | 1 | `ChangeFileProcessorTests.Process_InvalidMiddleLine_WritesErrorAndContinues` |
| AC-029 | T-002, T-007 | 4 | `TransactionLineParserTests.Parse_UsesTheCurrencysDecimalSeparator`<br>`TransactionLineParserTests.Parse_OtherCurrencysSeparator_IsInvalidForUsd`<br>`UsdCurrencyTests.DecimalSeparator_IsDot`<br>`UsdCurrencyTests.MinorUnitDigits_IsTwo` |
| AC-025 | T-005, T-011, T-014, T-017 | 10 | `DivisorEndpointsTests.Put_ValidDivisor_ReturnsItAndGetReflectsIt`<br>`DivisorEndpointsTests.Put_NewDivisor_AppliesToFilesProcessedAfterwards`<br>`ChangeCalculatorTests.Calculate_DivisorChanged_AppliesToNextCalculation`<br>`InMemoryDivisorSettingsTests.Change_PositiveInteger_ChangesCurrent`<br>… +6 more |
| AC-026 | T-011, T-014, T-017 | 4 | `DivisorEndpointsTests.Put_DivisorBelowOneOrMissing_Returns400ProblemDetailsAndKeepsTheOldValue`<br>`DivisorEndpointsTests.Change_MissingOrBelowOne_ReturnsInvalidDivisorAndKeepsTheValue`<br>`DivisorSettings.test.tsx › shows the server error when the divisor is rejected`<br>`DivisorSettings.test.tsx › shows a generic message when saving fails without a server response` |
| AC-027 | T-010, T-015, T-017 | 13 | `FileEndpointsTests.List_AfterUpload_ContainsTheFileNewestFirst`<br>`OpenApiDocumentTests.OpenApiDocument_DescribesEveryPublicEndpoint`<br>`FileEndpointsTests.Upload_ValidFile_StoresItAtTheCurrentTimeAndReturnsCreated`<br>`FileEndpointsTests.List_ReturnsSummariesNewestFirst`<br>… +9 more |
| AC-028 | T-010, T-015, T-017 | 4 | `FileEndpointsTests.Download_ReturnsThePlainTextOutputAsAnAttachment`<br>`FileEndpointsTests.Download_KnownId_ReturnsThePlainTextOutputAsAChangeFile`<br>`InMemoryUploadedFileStoreTests.TryGet_AddedFile_ReturnsIt`<br>`UploadedFilesList.test.tsx › gives each entry a download link to its change output` |
| AC-015 | T-009, T-010, T-011, T-013, T-017, T-018 | 21 | `FileEndpointsTests.Upload_WithoutAFile_Returns400InvalidFile`<br>`FileEndpointsTests.Download_UnknownId_Returns404FileNotFound`<br>`DivisorEndpointsTests.Put_NonIntegerDivisor_Returns400ProblemDetails`<br>`HostTests.UnknownApiRoute_ReturnsNotFoundProblemDetails`<br>… +17 more |
