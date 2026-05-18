---
description: 'Bitcraft xUnit testing standards for unit, integration, security, and regression tests.'
applyTo: 'tests/**/*.cs'
---

# Test Standards

## Test Shape
- Use xUnit and Arrange-Act-Assert.
- Test names follow `[MethodName]_[Scenario]_[ExpectedResult]`.
- Keep tests focused on behavior, invariants, authorization, ownership, privacy, and regression risk.
- Do not test framework internals.

## Coverage Expectations
- Domain tests cover invariants, invalid states, snapshot capture, ownership links, and aggregate behavior.
- Application tests cover handlers, validators, policies, and `Result<T>` outcomes.
- Infrastructure integration tests should be explicit and isolated from unit test expectations.
- Security-sensitive changes need negative tests for unauthorized, cross-owner, malformed, and missing-context cases.

## Execution
- Run the narrowest test command first.
- Broaden to `dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj` when unit behavior changed across shared logic.
- State any tests not run and why.
