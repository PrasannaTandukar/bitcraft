---
name: bitcraft-test-hardening
description: 'Create or improve Bitcraft tests for backend behavior, security, authorization, ownership, privacy, and regression coverage. Use when asked to test, validate, harden, reproduce bugs, or add xUnit coverage.'
argument-hint: '[changed area, feature, bug, or risk]'
---

# Bitcraft Test Hardening

Use this skill to add high-signal tests without broad test churn.

## When to Use This Skill

- The user asks for tests, validation, regression coverage, or security hardening.
- A backend behavior change needs Domain/Application coverage.
- Authorization, ownership, privacy, or invalid-state behavior is at risk.

## Procedure

1. Inspect nearest existing tests before creating new ones.
2. Identify behavior gaps and security/privacy invariants.
3. Add tests under the correct `tests/` project.
4. Use Arrange-Act-Assert and `[MethodName]_[Scenario]_[ExpectedResult]` names.
5. Run the narrowest relevant test first.
6. Broaden only when shared behavior changed.

## Coverage Targets

- Happy path.
- Invalid state.
- Authorization and ownership failures.
- Cross-tenant or cross-user data access when applicable.
- Cancellation or provider failure when meaningful.
- Snapshot immutability and audit links when changed.

## Gotchas

- Do not test framework internals.
- Do not create brittle tests that assert incidental ordering or exact internal implementation unless that is the contract.
- Do not modify production code unless the user asks for fixes too.
