# Architecture decision records

Numbered, immutable once ratified, superseded only by a later ADR. Format: [template.md](template.md).

| ADR | Decision | Status |
|---|---|---|
| [ADR-001](ADR-001-one-foundry-bounded-recipes.md) | One Foundry, bounded recipes — a single engine with versioned data-only recipes, never per-idea executables or a "generate anything" surface | Accepted |
| [ADR-002](ADR-002-winforms-first-ui-independent-services.md) | WinForms first, UI-independent services, standard controls only | Accepted |
| [ADR-003](ADR-003-open-ocfproj-package.md) | Open `.ocfproj` project package as the portable source of truth | Accepted |
| [ADR-004](ADR-004-structural-approvedartifact-gate.md) | Structural ApprovedArtifact gate on render, export, save-as-final, and print | Accepted |
| [ADR-005](ADR-005-taskdock-absorption.md) | TaskDock absorbed into Scaffold Smith as its task-entry preset | Accepted — ratified 29 Aug 2026 |
| [ADR-006](ADR-006-public-name-honest-ink.md) | The public name is Honest Ink ("— the classroom foundry"); repository and internal identifiers unchanged; counsel confirmation pre-release | Accepted — decided 29 Aug 2026 |
| [ADR-007](ADR-007-managed-pilot-upgrades-are-side-by-side.md) | Managed pilot upgrades are side-by-side and project preparation is copy-on-write | Proposed — advisory pending product-owner ratification and District IT deployment decisions |
| [ADR-008](ADR-008-public-module-display-names.md) | Six public module display names change; stable project, recipe, schema, and diagnostic identifiers do not | Accepted — decided 30 Aug 2026 |
| [ADR-009](ADR-009-strandplan-display-name.md) | StrandPlan replaces GridLesson as the provisional lesson-design display; stable `lesson-loom` identities remain unchanged | Accepted — corrected 31 Aug 2026 |

## Decision companions

| Packet | Purpose | Status |
|---|---|---|
| [Recipe identity disposition packet](recipe-identity-disposition-packet.md) | Records Option A for all 23 alpha-tag recipe identities plus the same-commit first-admission freeze for 15 candidate-only identities; retains the independent schema-1 `recipeHash` release stop without reopening ADR-001 | **RATIFIED — OPTION A** |
