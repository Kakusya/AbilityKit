# Accepted API amendment — AK-I13-API-CLARIFY-20261007-01

This bounded amendment takes precedence over original declarations for FailurePack, IFlowOrchestrator.RunAsync and RoleControl. Original declarations remain history, not active overloads. Full verbatim responsibilities, calling sequences, CLI/schema/failure semantics and minimal checks: [dot raw reply](dot-api-amendment-reply-raw.txt). Reviewed published source: `6ad948d0c72255873b4fbd1cd6b5fce7594e3d13`; this accepts design, not an implementation candidate.

## Declaration 1 (verbatim)

```csharp
public sealed record FlowInvocation(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);
```

## Declaration 2 (verbatim)

```csharp
public sealed record FailurePack(
    RunIdentity Run,
    FlowRequest Request,
    SourceStamp Source,
    FlowInvocation ActualInvocation,
    FlowFailure FirstFailure,
    IReadOnlyList<FlowObservation> RelevantCuts,
    IReadOnlyList<LoggedEvent> EventWindow,
    string ReproduceCommand,
    CleanupResult Cleanup);
```

## Declaration 3 (verbatim)

```csharp
public enum FlowDiagnosticFault
{
    None,
    FailAfterStart
}
```

## Declaration 4 (verbatim)

```csharp
public sealed record FlowRunOptions(
    FlowInvocation ActualInvocation,
    FlowDiagnosticFault DiagnosticFault = FlowDiagnosticFault.None);
```

## Declaration 5 (verbatim)

```csharp
public interface IFlowOrchestrator
{
    Task<FlowResult> RunAsync(
        FlowRequest request,
        FlowRunOptions options,
        CancellationToken cancellationToken);
}
```

## Declaration 6 (verbatim)

```csharp
public sealed record RoleControl(
    int SchemaVersion,
    RunIdentity Run,
    string RoleId,
    long ControlSequence,
    string ControlId,
    RoleControlKind Kind,
    RoleInit? Init,
    FlowAction? Action,
    string? ReplayOfCallId,
    string? BarrierId,
    string? ObservationPoint);
```

## Current behavior and ownership

Program captures actual executable/argument boundaries/working directory; options are copied immutable invocation inputs. All callers pass FlowRunOptions explicitly; no fabricated-source compatibility overload. FailurePack Source/Run/Cleanup matches result. Save effective request.json. Reproduction points to it and a concrete fresh output directory using the approved --output-root override. Diagnostic flags stay in reproduce argv. No gameplay/wire/schema upgrade.

CLI: run --request <file> [--output-root <fresh-directory>] [--diagnostic-fault fail-after-start | --diagnostic-exit-before-run]. Unknown/duplicate/mutually exclusive options reject before host creation, exit2. Only one diagnostic fault value. Fault-after-start means real valid initial observation, no gameplay dispatch, HarnessOrEnvironmentFailure/DiagnosticFailAfterStart/diagnostic-after-start, Failed1, execution incomplete/product Undetermined; preserve initial facts and independent bounded cleanup. Evidence completeness describes produced evidence, not unexecuted goals. Exit-before-run validates/prepares fresh output/saves request then returns86 before orchestrator/session/collector/children; no invented FlowResult/RunId/result/failurepack, caller identifies incomplete.

RoleControl ArmAction.ControlId equals new invocation CallId. New action requires Action.CallId match; replay uses Action=null and known same role/run/scope/session/generation ReplayOfCallId. Release has another ID, no second execution on duplicate controls. RoleInit unchanged; discover current binding through actual Join/baseline, never install snapshots over role control. Parent verifies actual server instance/scope and each client own generation, not equal generations between distinct clients.

No new files/dependencies/helpers/rule semantics; amend original FlowContracts/Program/FlowOrchestrator/FlowReport/CookingFixedFlowTests/fixed-flow files and main task. Apply within current S1; stop at S1 technical gate, S2 later consumes RoleControl. Focused checks only, actual S3 success/failure/no-result caller evidence can reuse matching prior success; synthetic controls remain labeled synthetic.
