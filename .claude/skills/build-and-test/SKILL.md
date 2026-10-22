---
name: build-and-test
description: How to build and test this project. Use when building, testing, or finishing a change.
---

## While iterating

Run any `dotnet build` or `dotnet test` command that is useful (for example, filtering to one test or project).

To run a single test

```sh
dotnet test --filter "FullyQualifiedName~Namespace.ClassName.MethodName"
```

## Before finishing

At the end, run `build-and-test.cmd` from the repo root to make sure everything is good.
