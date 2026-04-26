# .NET 10 Upgrade Plan

## Overview

**Target**: Upgrade from .NET 8 to .NET 10 (LTS)
**Scope**: 2 projects (~1700 LOC) — 1 console application + 1 class library

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: Small solution (2 projects), both on .NET 8, straightforward upgrade with 100% package compatibility and zero breaking API changes detected.

## Tasks

### 01-prerequisites: Verify SDK and Environment

Validate the .NET 10 SDK is installed and global.json files (if any) are compatible with the target framework.

**Done when**: .NET 10 SDK validated and any global.json files confirmed compatible or updated

---

### 02-update-target-frameworks: Update All Project Target Frameworks

Update both projects (md2docx.csproj and MDConverter.csproj) to target net10.0. This updates the TargetFramework element in both project files simultaneously.

**Done when**: Both project files specify `<TargetFramework>net10.0</TargetFramework>`

---

### 03-restore-and-build: Restore Dependencies and Build Solution

Restore NuGet packages for the updated target framework and build the entire solution. Address any compilation errors that arise from the framework change.

Packages to restore:
- DocumentFormat.OpenXml 3.2.0
- Markdig 0.38.0

**Done when**: Solution builds successfully with zero compilation errors

---

### 04-run-tests: Execute Test Suite

Run the full test suite to verify functionality after the upgrade. Validate that all tests pass and there are no runtime behavioral issues.

**Done when**: All tests pass, or test projects identified if none exist
