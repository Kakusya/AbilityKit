# Scoped menu Ready Host increment

## Boundary and baseline

Integration worktree merged master125ffe906 as d1658e989; Trellis-only historical conflicts used current master records. Untracked older preparation research was preserved under integration local/preserved-preparation-configuration-increment.md. This increment owns CookingLevelEtHost.cs, CookingMenuGameplayFactory.cs, CookingMenuReadyEtTests.cs. It does not complete S14 natural operating or durable loading, networking or Unity.

## Implemented behavior

The optional ICookingMenuGameplayFactory extends the existing preparation factory with per-scope immutable catalog and frozen menu policy. Current selected menus are a subset of loaded ContentProvenance.SelectedMenus with the same catalog hash. Scoped Front order templates equal the selected menu template set, rather than the entire loaded graph. Legacy factories retain their behavior.

The actual owned Preparing kitchen receives ConfigureMenuPolicy through the trusted Created/Preparing installation authority. CompletePreparation reads that same owner's manufacturing availability, returns structured MenuDiagnostics, and leaves all observed state unchanged on MenuNotReady. Temporary stock warnings do not become normal business failure. Successor and retry stage current-scope Front/menu before publication; carried objects remain under the new policy without granting new permissions.

Checkpoint export wires the root-owned required nullable MenuConfigurationIdentity. Restore compares against trusted factory input before creating kitchen/ET authority, freezes menu/Front/preparation once, and reuses those exact frozen instances. Menu-enabled Running restore restores the real Recipe checkpoint before Ready validation/Start; saved Front data is then restored without losing customers. Saved identity does not grant policy.

## Findings fixed during implementation and review

- Initial fixture supplied multiple raw objects into ordinary spatial slots. It now declares one item per distinct trusted raw slot and recomputes candidate/config identity; production single-slot validation was retained. Its geometry contains configured player poses and all actual station/source references.
- Retry fixture duplicated standard seed supply before Host standard restoration. Its retry factory leaves raw seed initialization to the existing Host retry path.
- Early compilation fixes corrected a restore-result named argument, fixture WashableContainerDefinitions and observation Front member.
- A PowerShell encoding roundtrip damaged existing Chinese comments. All eight original XML blocks and three ordinary Chinese comment lines were restored verbatim from UTF-8 master125ffe906; the final diff contains only intended logic.
- Optional menu factories returning null must not silently become legacy. Catalog/policy/scoped Front returns now reject malformed null configuration. The actual null-catalog test proves no factory Create/authority publication and later valid construction succeeds.
- Repeated mutable factory reads could install policy B after comparing saved identity A. Restore now freezes once and passes frozen values to a private constructor and preparation initializer. An alternating A/B factory is read once; the recovered identity is A and subsequent stable restore remains usable.
- Root/producer fixed Configure's lifecycle guard after genuine bound Created/Running red tests. That guard is a prerequisite, not Host-owned code or evidence claimed solely from these ET tests.

## Actual verification

Command executed in integration:

`dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --filter FullyQualifiedName~CookingMenuReadyEtTests --logger "trx;LogFileName=cooking-menu-ready-runtime-first.trx"`

Final result: **12 passed, 0 failed, 0 skipped**, test duration about 1 second, command 4.7 seconds; compilation passed without new warnings/errors in that final incremental run. `git diff --check` passed.

The twelve cases cover current F01 subset of loaded F01+D31, missing appliance/material/Front structured Ready rejection with identical Observe, Preparing and Running restore canonical equivalence and pre-create identity rejection, successor D31/retry F01 Front selection, null-catalog no publication, restored forbidden material Pickup accepted / PutIn MenuNotAuthorized / Drop accepted in both phases, and freeze-once alternating factory control.

Earlier compilation and fixture failures plus intermediate 4/4 and 7/8 results are preserved; they are not final gate evidence. Logs/TRX are in `local/Logs/cooking-execution/menu-ready-et/`, final `cooking-menu-ready-runtime-first.log` and `.trx`. No broad gate was run for this increment. Source and integration .NET window released before parent review.

## Uncommitted prerequisite manifest

Root owns the copied CookingLevelCheckpoint.cs format8 schema change. Producer owns copied CookingMenuRuntimePolicy.cs, CookingRecipeLoop.cs, CookingRecipeSupply.cs and CookingPortions.cs. Root explicitly authorized three immutable menu-field assignments in integration CookingGenerationTransactionCopy.cs and will mirror/review them. These prerequisites remain uncommitted by this Host increment. Root's five legacy format8 test expectation files remain separately owned and must be assembled before broad gates.
