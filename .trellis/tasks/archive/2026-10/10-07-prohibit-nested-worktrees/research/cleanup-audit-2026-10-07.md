# Worktree cleanup audit - 2026-10-07

## Permission state

- Windows Developer Mode is enabled at `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock` with `AllowDevelopmentWithoutDevLicense = 1`.
- A symbolic-link creation check passed. Its temporary link, target file, and containing directory were removed after verification.
- OpenSSH Server was not installed because no observed host failure required it.

## Reclaimed worktrees

The root coordinator closed their settled Orca terminals and removed these worktrees one at a time:

- `issue6-compiler-input-target-repair-ccswitch`: clean commit `3fe069c3`; patch-id matches integrated candidate commit `c13f6bbd`.
- `issue6-compiler-trace-repair-ccswitch`: clean commit `a54b49ab`; patch-id matches integrated candidate commit `2b258b6e`.
- `issue6-compiler-target-compat-repair-ccswitch`: uncommitted source patch matches the source portion of integrated candidate commit `8497d3a6`; its report differs from the integrated copy only by a trailing blank line.
- `issue6-final-verifier-ccswitch`: the unique Slice 12 failure report was copied byte-for-byte into the Issue 6 candidate task before removal; SHA-256 `647854023553D367D420A6762F29FA7075D0BE421AE2CDB36110F8553B33BD48`.
- `issue6-final-verifier2-ccswitch`: Slice 13 report hash matched the candidate copy.
- `issue6-final-verifier3-ccswitch`: Slice 15 report hash matched the candidate copy.

The source branches remain present after worktree removal.

## Retained worktrees

- Root `AbilityKit`: coordinator workspace.
- `issue6-cooking-truthful-gates`: current Issue 6 candidate and durable evidence owner.
- `issue6-test-gate-results`: stopped historical evidence required by the project scope contract.
- `issue6-final-verifier4-ccswitch`: retained because its agent was still working at the audit checkpoint.
- `issue6-slice17-exact-sha-verifier-ccswitch`: unauthorized descendant retained because its agent was still working; its overlapping exclusivity claim is invalid and it must be audited before cleanup.

No retained session was messaged during this cleanup.
