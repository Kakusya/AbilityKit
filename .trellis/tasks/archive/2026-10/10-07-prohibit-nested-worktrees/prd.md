# Project-wide prohibition on nested worktrees

## Goal

Make worktree creation coordinator-only across AbilityKit, prevent agents inside non-main worktrees from creating descendants, and record safe lifecycle cleanup rules.

## Requirements

- Worktree creation is reserved to the root coordinator for every AbilityKit task and workflow.
- A worker, agent, or terminal running in a non-main worktree must not create a descendant through Orca, native Git, or delegation to another session.
- Dispatches must state the prohibition and direct isolation requests back to the root coordinator.
- Existing descendant worktrees are reclaimed incrementally only after active processes, uncommitted changes, retained evidence, and branch reachability are checked.
- An unauthorized nested worktree invalidates any claimed exclusive verification window that overlaps it.

## Acceptance Criteria

- [x] Root `AGENTS.md` states the project-wide coordinator-only creation rule and prohibited entry points.
- [x] The supervised delivery SOP applies the same rule to all future dispatches and cleanup decisions.
- [x] The rule distinguishes worktree creation from starting a fresh terminal in an approved existing worktree.
- [x] Cleanup records preserve user work, task evidence, and branches that are not yet safely reachable elsewhere.
- [x] No Issue 6 candidate code or Cooking product behavior changes as part of this governance task.

## Notes

- Owner clarified on 2026-10-07 that this is a permanent project constraint, not an Issue 6-only rule.
- This is a lightweight documentation and operating-contract task; PRD-only planning is sufficient.
