# Foreign key naming rule

Proposed rule: `ENTITY_FRAMEWORK_0005`.

## Summary

A foreign key property's name must end with the name of the entity it points to, followed by the name of that entity's key property.

For the common case where the key is called `Id`, the name must match `.*{EntityName}Id`.

## Motivation

A foreign key name should say what it references. `OwnerId` doesn't tell you whether it points to a `User`, a `Customer` or a `Team`. `CreatedByUserId` does, and it still lets the prefix describe the role.

## Behavior

For each foreign key in the EF model, the analyzer reports a diagnostic on the foreign key property when its name does not end with `{PrincipalEntityName}{PrincipalKeyPropertyName}`.

| Property | Principal | Result |
| --- | --- | --- |
| `UserId` | `User` | OK |
| `CreatedByUserId` | `User` | OK |
| `BillingAddressId` | `Address` | OK |
| `ParentCategoryId` | `Category` | OK |
| `CreatedById` | `User` | Reported |
| `OwnerId` | `User` | Reported |

The match is a case-sensitive `EndsWith`, not a regex.

## Why not match the navigation name?

EF's own convention builds the foreign key name from the navigation name (`<NavigationName><PrincipalKey>`). This rule deliberately ignores the navigation name, because a name such as `CreatedBy` says who did it but not what type it is. Matching the entity name keeps the type visible in the foreign key name.

## Detection

Read foreign keys from the EF model metadata, so that keys configured by convention, by `[ForeignKey]` and by the Fluent API are all covered. A syntax-only check of property names would miss the Fluent API case.

Match against the entity type name, not the table name. Table names are often pluralized (`Users`) or remapped with `ToTable`, which would cause false positives.

## Severity

Warning.

## Open questions

- **Self-references.** `ParentId` on `Category` is reported, because it doesn't end in `CategoryId`. `ParentCategoryId` passes. Is that the intended outcome, or should self-references be exempt?
    - That is the intended outcome
- **Name equal to the suffix.** Should a bare `UserId` pointing at `User` always pass, including on a self-referencing `User` table, where it would be confusing?
    - Yes
- **Key name.** The rule assumes the principal key is called `Id`. When the key is `UserKey` or `Code`, is the suffix `{EntityName}{KeyPropertyName}` (`UserKey`)? Or should it be `{EntityName}Id` whatever the key is called?
    - Always Id
- **Composite keys.** Skip them, or require only the entity name as part of the name?
    - Skip them

- **Foreign keys to alternate keys.** When the foreign key targets an alternate key rather than the primary key, which key name goes in the suffix?
    - Skip them
- **Shadow properties.** Shadow foreign keys have no property in code to report on. Should the diagnostic go on the navigation property, or should they be skipped?
    - Skip them
- **Inheritance.** `Employee` derives from `Person`, and the key `Id` is declared on `Person`. For `Order.EmployeeId` pointing at `Employee`, should the suffix use the type the foreign key points at (`EmployeeId`) or the root of the hierarchy where the key is declared (`PersonId`)?
    - The type the foreign key points at (`EmployeeId`). Accepting the base type name as well is not planned.
- **Generated or external entities.** Should the rule skip entities from generated code or from another assembly, where the name can't be changed?
    - No, include them
- **Severity.** Is this Info or Warning? The previous rule in this repo was lowered to Info (see `ENTITY_FRAMEWORK_0002`), which suggests Info.
   - warning
- **Code fix.** Renaming a property also changes the column name and requires a migration. Is a code fix worth providing, or should the rule only report?
   - No, only report.
- **Configuration.** Should the suffix pattern be configurable through `.editorconfig`, for teams that name things differently?
    - No, the suffix pattern is fixed.
    
