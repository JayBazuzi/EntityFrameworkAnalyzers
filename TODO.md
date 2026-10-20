# TODO

## How to pick up a rule

- Rule IDs are `ENTITY_FRAMEWORK_NNNN`; the next free one is `ENTITY_FRAMEWORK_0002`. Add tests in `test/EntityFrameworkQueryAnalyzers.Tests/`, a README example via `begin-snippet`, and let `RulesTableTests` regenerate the rules table.
- Don't duplicate rules that EF Core already ships (`EF1001`, `EF1002`, ...).
- Add a `src/EntityFrameworkQueryAnalyzers.CodeFixes` project (see [AzureFunctionAnalyzers](https://github.com/JayBazuzi/AzureFunctionAnalyzers)) when the first code fix is written.

## Ideas
