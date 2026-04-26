# Scenario Instructions: .NET Version Upgrade

## Scenario Overview
Upgrade solution from .NET 8 to .NET 10

## Preferences

### Target Framework
- **Target**: .NET 10.0 (LTS)
- **Confirmed**: 2024-01-15

### Flow Mode
- **Mode**: Automatic
- **Behavior**: Run end-to-end, pause only when blocked or needing user input

### Technical Preferences
*(Package version preferences, migration approach preferences will be recorded here)*

### Custom Instructions
*(Task-specific rules and preferences will be recorded here)*

## Strategy
**Selected**: All-At-Once
**Rationale**: Small solution (2 projects), uniform framework baseline (both on .NET 8), zero breaking changes, 100% package compatibility — straightforward upgrade ideal for atomic operation.

### Execution Constraints
- Single atomic upgrade — all projects updated together
- Validate full solution build after upgrade
- Complete all framework and package updates before testing

### Commit Strategy
- **Default**: Single Commit at End
- **Behavior**: All changes committed together after successful completion

## Key Decisions Log
*(Major decisions with rationale and date will be recorded here)*
