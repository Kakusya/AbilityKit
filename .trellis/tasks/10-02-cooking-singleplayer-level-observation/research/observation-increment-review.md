# S14 scoped observation projection increment

Status: source staged only in cooking-supply-s07. No integration import or commit. Independent focused verification passed 5/5, 0 skipped. Root owns host Observe wiring and full exit verification.

API: CookingLevelObservationProjector.Project(scope, lifecycle, hostFrameSequence, recipe?, front?, installedLayout?, preparationPolicyIdentity?, geometryIdentity?, configuredPlayers?) returns CookingLevelObservation. Created supports absent kitchen; scope/epoch/frame remain visible. Recipe match scope and nonnegative watermarks are checked.

Only committed snapshots are received. Recipe contents, supply lists/origins, front nested routes/spatial/work/customer/table lists and installed layout collections are copied into read-only collections. No simulation reference, writable callback, second ledger or completion re-evaluation exists.

Hand objects derive from actual PlayerHand locations, containers from the container index. DefinitionKey and RecipeKey are stable identifiers; no identifier string guesses capability or cooking state. Explicit owner fields retain product/completion/dirty/remaining portions/order binding. Inventory separates LiveObjects, ContainerObjects, RemainingPreparedPortions, ProductObjects and physical original supply units in hands/packages. A five-portion batch stays one object and exposes five remaining portions separately. External balances and deliveries never inflate kitchen inventory. UnavailableOriginalSupplyUnits does not distinguish consumed from discarded.

Five focused cases are staged: scope/epoch/frame/Created, held container and progress/order fields, external versus physical supply inventory, deep freezing of mutable nested lists, and repeated observations leaving owner checkpoint unchanged. Actual independent execution: dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter FullyQualifiedName~CookingLevelObservationTests, 5 passed / 0 failed / 0 skipped. Evidence in supply worktree TEMP/cooking-observation-first.log and TEMP/observation-results/cooking-observation-first.trx. An initial redirection attempt failed because TEMP did not exist and did not start dotnet; the directory was created before the actual run. Existing dependency warnings remain; no new compiler error occurred.

Pending: host Observe with one committed frame/current restored owners/trusted policy/layout identities; rejected command, pause, durable-save failure, restore and successful handoff coverage. This helper does not complete S14 or add Unity UI/schema changes.
