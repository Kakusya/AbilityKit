#!/usr/bin/env python3
"""Evaluate frozen local ordinary-load Release evidence; never edits input artifacts."""
import argparse
from datetime import datetime
import hashlib
import json
import math
from pathlib import Path
import re
import sys
import uuid

SCHEME = "381b4222-f694-41f0-9685-ff5bb260df2e"
TARGETS = {"minimumPerParticipant": 285, "terminalP95Ms": 100, "terminalP99Ms": 250,
           "projectionP95Ms": 200, "projectionP99Ms": 400}


def read_text(path):
    raw = path.read_bytes()
    return raw.decode("utf-16" if raw.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8-sig")


def load(path):
    def reject_constant(value):
        raise ValueError("Non-finite JSON constant: " + value)
    value = json.loads(read_text(path), parse_constant=reject_constant)
    def verify_finite(item):
        if isinstance(item, float):
            require(math.isfinite(item), "Non-finite JSON number")
        elif isinstance(item, dict):
            for nested in item.values():
                verify_finite(nested)
        elif isinstance(item, list):
            for nested in item:
                verify_finite(nested)
    verify_finite(value)
    return value


def valid_guid(value):
    return isinstance(value, str) and uuid.UUID(value).int != 0


def require(condition, message):
    if not condition:
        raise ValueError(message)


def integer(value):
    return type(value) is int and value >= 0


def sha(value):
    return isinstance(value, str) and re.fullmatch(r"[0-9a-fA-F]{64}", value) is not None


def latency(distribution, count):
    require(type(distribution["samples"]) is int and distribution["samples"] == count and
            all(type(distribution[k]) in (int, float) and math.isfinite(distribution[k]) and distribution[k] >= 0
                for k in ("p50", "p95", "p99")), "Invalid latency cohort")
    require(distribution["p50"] <= distribution["p95"] <= distribution["p99"], "Invalid percentile ordering")
    return {key: distribution[key] for key in ("samples", "p50", "p95", "p99")}


def evaluate(directory):
    directory = Path(directory).resolve()
    result = {"artifact": str(directory), "status": "NOT_VERIFIED", "reference": "local-amd9800x3d-32gb-win11-balanced",
              "limits": TARGETS, "resourceThreshold": "UNSET", "degradedNetworkQosThreshold": "UNSET",
              "physicalTwoPc": "NOT_VERIFIED", "evidenceIssues": [], "limitFailures": [], "repeats": []}
    try:
        require(read_text(directory / "build-configuration.txt").strip() == "Release", "Release configuration evidence required; historical Debug is not eligible")
        for name in ("source-commit.txt", "source-status.txt", "sdk-info.txt", "run.log"):
            text = read_text(directory / name)
            require(name == "source-status.txt" or bool(text.strip()), "Missing nonempty " + name)
        require(re.fullmatch(r"[0-9a-f]{40}", read_text(directory / "source-commit.txt").strip()) is not None, "Invalid source HEAD")
        environment = load(directory / "environment-inventory.json")
        require(not environment["errors"], "Incomplete inventory")
        require(environment["referenceSelection"] == result["reference"], "Wrong selected reference")
        require(environment["capturedUtc"] and environment["machine"], "Inventory timestamp/machine missing")
        require(len(environment["cpu"]) == 1 and "9800X3D" in environment["cpu"][0]["Name"].upper(), "Wrong reference CPU")
        require(environment["cpu"][0]["AddressWidth"] == 64, "Reference architecture must be64bit")
        require(30 * 1024**3 <= environment["computer"]["TotalPhysicalMemory"] <= 34 * 1024**3, "Wrong32GB reference RAM")
        require("WINDOWS 11" in environment["os"]["Caption"].upper() and int(environment["os"]["BuildNumber"]) >= 22000, "Wrong Windows11 reference")
        require(environment["power"]["guid"].lower() == SCHEME, "Reference balanced power scheme differs")
        require(isinstance(environment["topProcesses"], list) and isinstance(environment["managedProcesses"], list), "Contention snapshots missing")
        require(isinstance(environment["gcEnvironment"], dict) and environment["gcModeEvidence"], "GC/config evidence missing")
        process = load(directory / "process-exit.json")
        require(process["launched"] is True and type(process["exitCode"]) is int and process["exitCode"] == 0, "Successful actual native process exit required")
        require(process["configuration"] == "Release" and process["startedUtc"] and process["completedUtc"], "Process configuration/timing missing")
        stamp = lambda value: datetime.fromisoformat(value.replace("Z", "+00:00"))
        require(0 <= (stamp(process["startedUtc"]) - stamp(environment["capturedUtc"])).total_seconds() <= 60, "Inventory is not contemporaneous before execution")
        require(stamp(process["completedUtc"]) > stamp(process["startedUtc"]), "Process completion timestamp invalid")
        executable = load(directory / "executable-hash.json")["Hash"]
        require(sha(executable) and process["executableSha256"].upper() == executable.upper(), "Executable provenance conflicts")
        build = load(directory / "build-provenance.json")
        require(build["configuration"] == "Release" and build["executableSha256"].upper() == executable.upper(), "Matching actual Release build provenance required")
        require(re.fullmatch(r"[0-9a-f]{40}", build["sourceHead"]) and build["builtUtc"], "Build source provenance missing")
        provenance = load(directory / "tool-provenance.json")
        require(provenance["configuration"] == "Release" and provenance["sourceHead"] == read_text(directory / "source-commit.txt").strip(), "Checkout provenance conflict")
        paths = {}
        for section in ("sources", "binaries"):
            require(isinstance(provenance[section], list) and provenance[section], "Missing frozen " + section)
            seen = set()
            for entry in provenance[section]:
                require(sha(entry["sha256"]), "Invalid artifact SHA")
                relative = entry["artifactPath"]
                path = (directory / relative).resolve()
                require(path.is_relative_to(directory) and path != directory, "Artifact escapes directory")
                require(relative not in seen, "Duplicate provenance entry")
                seen.add(relative)
                require(hashlib.sha256(path.read_bytes()).hexdigest().upper() == entry["sha256"].upper(), "Frozen artifact hash mismatch: " + relative)
                paths[entry.get("sourcePath", entry.get("name"))] = entry["sha256"].upper()
        for source in ("tools/run-cooking-network-measurement.ps1", "tools/evaluate-cooking-network-reference.py"):
            require(source in paths, "Tool source missing: " + source)
        require(paths.get("AbilityKit.Game.Cooking.NetworkMeasurement.dll") == executable.upper(), "Frozen executable mismatch")
        for name in ("AbilityKit.Game.Cooking.EtRuntime.dll", "AbilityKit.Game.Cooking.Session.dll", "AbilityKit.Game.Cooking.NetworkMeasurement.runtimeconfig.json"):
            require(name in paths, "Dependency/runtime evidence missing: " + name)
        built_sources = build["sourceHashes"]
        require(len(built_sources) == 5 and len({e["sourcePath"] for e in built_sources}) == 5, "Actual compiled source hashes missing")
        for entry in built_sources:
            require(paths.get(entry["sourcePath"]) == entry["sha256"].upper(), "NoBuild source differs from actual compiled source: " + entry["sourcePath"])
        measurement = load(directory / "measurement.json")
        require(measurement["passed"] is True and measurement["controlOnly"] is False, "Complete successful load report required")
        require(measurement["topology"] in ("InProcess", "SameMachineUdp"), "Reference topology outside approved scope")
        arguments = [str(value) for value in process["arguments"]]
        require(len(arguments) == 7 and "/bin/Release/net10.0/" in arguments[0].replace("\\", "/") and arguments[0].endswith("AbilityKit.Game.Cooking.NetworkMeasurement.dll") and arguments[1].endswith("measurement.json"), "Actual Release native arguments missing")
        people = measurement["participantCount"]
        require(type(people) is int and people in (2, 4) and measurement["offeredRate"] == 5, "Only ordinary2/4?5 workload eligible")
        require(arguments[2:] == [measurement["topology"], "--participants", str(people), "--offered-rate", "5"], "Native invocation differs from reported ordinary workload")
        require(integer(measurement["pid"]) and measurement["pid"] > 0 and measurement["machine"] == environment["machine"], "Runtime process/machine missing or conflicting")
        require(measurement["sdkRuntime"] and valid_guid(measurement["etBuild"]) and valid_guid(measurement["sessionBuild"]), "Runtime/MVID identity missing")
        controls = [r for r in measurement["reports"] if r["kind"] == "ReceiptCapacity16"]
        require(len(controls) == 1 and controls[0]["accepted"] == 16 and controls[0]["rejected"] == 1 and controls[0]["cachedDuplicate"] is True and controls[0]["finalReadiness"]["asserted"] is True, "Actual capacity/retry/readiness control missing")
        repeats = [r for r in measurement["reports"] if r["kind"] == "BaselineMeasurement"]
        require(len(measurement["reports"]) == 4 and len(repeats) == 3 and {r["repeat"] for r in repeats} == {1, 2, 3}, "Exactly three fresh complete repeats required")
        instances = set()
        for repeat in repeats:
            require(valid_guid(repeat["serverInstance"]), "Invalid server instance")
            instances.add(repeat["serverInstance"])
            expected_config = "0D3AB7525979E14D7DEDA624AB386C4B1C3C628C5B461CDE4C1D09A490E3C9DE" if people == 2 else "66ECE057B95560D64BA5CFBF22CFCB38845B63A8DDC131F09E73D23A60DB2180"
            require(repeat["configurationIdentity"]["Schema"] == "cooking-definition-v3" and repeat["configurationIdentity"]["Sha256"] == expected_config, "Reference fixture identity changed")
            require(repeat["participantCount"] == people and repeat["offeredPerParticipantPerSecond"] == 5 and repeat["warmupSeconds"] == 10 and repeat["sampleSeconds"] == 60, "Workload changed")
            require(repeat["offered"] == [300] * people and repeat["warmupOffered"] == [50] * people, "Offered schedule incomplete")
            issued = repeat["issued"]
            require(integer(issued) and issued > 0 and all(repeat[k] == issued for k in ("admitted", "accepted", "completed", "projectionCompleted")), "Issued/admitted/completed/projected mismatch")
            require(all(repeat[k] == 0 for k in ("rejected", "cancelled", "pending", "terminalTimeouts", "projectionTimeouts")), "Functional error/timeout/pending")
            participants = repeat["perParticipant"]
            require(len(participants) == people and {p["participant"]["Value"] for p in participants} == {"measure-" + chr(97 + i) for i in range(people)}, "Actual participant identities missing")
            require(sum(p["issued"] for p in participants) == issued, "Aggregate participant count mismatch")
            require(len(repeat["skippedBackpressure"]) == len(repeat["schedulerSkipped"]) == people and issued + sum(repeat["skippedBackpressure"]) + sum(repeat["schedulerSkipped"]) == 300 * people, "Aggregate offered accounting mismatch")
            require(repeat["baselineBytes"] <= 8 * 1024**2 and repeat["baselineTokens"] <= 1048576, "Recorded baseline exceeds frozen bounds")
            summary = {"repeat": repeat["repeat"], "issued": issued, "perParticipant": [], "terminal": latency(repeat["rttMs"], issued), "projection": latency(repeat["sendToCommittedProjectionMs"], issued)}
            for participant in participants:
                count = participant["issued"]
                require(integer(count) and count > 0 and all(participant[k] == count for k in ("admitted", "accepted", "completed", "projectionCompleted")), "Participant legal completion mismatch")
                require(participant["offered"] == 300 and integer(participant["backpressure"]) and integer(participant["schedulerSkipped"]) and count + participant["backpressure"] + participant["schedulerSkipped"] == 300, "Participant offered accounting mismatch")
                require("rttMs" in participant and "sendToCommittedProjectionMs" in participant,
                        "Missing per-participant latency evidence; aggregate percentiles cannot substitute")
                terminal = latency(participant["rttMs"], count)
                projection = latency(participant["sendToCommittedProjectionMs"], count)
                summary["perParticipant"].append({"participant": participant["participant"]["Value"], "completed": count,
                                                   "terminal": terminal, "projection": projection})
                for field, distribution, q, target in (("rttMs", terminal, "p95", "terminalP95Ms"),
                        ("rttMs", terminal, "p99", "terminalP99Ms"),
                        ("sendToCommittedProjectionMs", projection, "p95", "projectionP95Ms"),
                        ("sendToCommittedProjectionMs", projection, "p99", "projectionP99Ms")):
                    if distribution[q] > TARGETS[target]:
                        result["limitFailures"].append({"repeat": repeat["repeat"], "participant": participant["participant"]["Value"],
                            "metric": field + "." + q, "actual": distribution[q], "maximum": TARGETS[target]})
                if count < TARGETS["minimumPerParticipant"]:
                    result["limitFailures"].append({"repeat": repeat["repeat"], "participant": participant["participant"]["Value"], "metric": "fullyCompletedOffers", "actual": count, "requiredMinimum": 285})
            ready = repeat["finalReadiness"]
            require(ready["asserted"] is True and len(ready["clients"]) == people and all(c["IsSynchronized"] is True for c in ready["clients"]), "Clients not synchronized at exit")
            view = ready["LatestSessionProjection"]["Participants"]
            require({p["Participant"]["Value"] for p in view} == {"measure-" + chr(97 + i) for i in range(people)}, "Server roster identities conflict")
            require(len(view) == people and all(p["ConnectedOwnerBinding"] is True and p["Ready"] is True and p["CleanupPending"] is False and p["ConnectionGeneration"] > 0 for p in view), "Server exit bindings invalid")
            require({c["Identity"]["Participant"]["Value"] for c in ready["clients"]} == {"measure-" + chr(97 + i) for i in range(people)}, "Client roster identities conflict")
            scopes = []
            for client in ready["clients"]:
                identity = client["Identity"]
                server = next(p for p in view if p["Participant"] == identity["Participant"])
                scopes.append(identity["Scope"])
                require(identity["Epoch"] == identity["Scope"]["LevelEpoch"] and identity["Epoch"] > 0, "Client scope epoch invalid")
                require(client["ServerSessionInstance"] == identity["ServerSessionInstance"] == repeat["serverInstance"] and identity["ConnectionGeneration"] == server["ConnectionGeneration"] and sha(identity["StateHash"]) and valid_guid(identity["IssueId"]), "Client binding/hash identity invalid")
            require(all(scope == scopes[0] for scope in scopes), "Client current scopes conflict")
            require(sha(repeat["fullStateHash"]) and ready["sameFrameAuthorityHash"] == repeat["fullStateHash"], "Same-frame full-state hash conflict")
            for field in ("rttMs", "terminalObservedToCommittedProjectionMs", "sendToCommittedProjectionMs"):
                distribution = repeat[field]
                require(distribution["samples"] == issued and all(type(distribution[k]) in (int, float) and math.isfinite(distribution[k]) and distribution[k] >= 0 for k in ("p50", "p95", "p99")), "Invalid latency cohort")
                require(distribution["p50"] <= distribution["p95"] <= distribution["p99"], "Invalid percentile ordering")
            for field in ("hostReceiveConsumeMs", "hostConsumeSendMs"):
                require(repeat[field]["samples"] == repeat["timingSamples"], "Host receipt cohort mismatch")
            for field, q, target in (("rttMs", "p95", "terminalP95Ms"), ("rttMs", "p99", "terminalP99Ms"), ("sendToCommittedProjectionMs", "p95", "projectionP95Ms"), ("sendToCommittedProjectionMs", "p99", "projectionP99Ms")):
                if repeat[field][q] > TARGETS[target]:
                    result["limitFailures"].append({"repeat": repeat["repeat"], "metric": field + "." + q, "actual": repeat[field][q], "maximum": TARGETS[target]})
            result["repeats"].append(summary)
        require(len(instances) == 3, "Repeated authority instance is not three fresh runs")
        result["topology"] = measurement["topology"]
        result["participants"] = people
        result["status"] = "NOT_ACCEPTED" if result["limitFailures"] else "ACCEPTED_LOCAL_REFERENCE"
    except (OSError, ValueError, KeyError, TypeError, IndexError, StopIteration) as error:
        result["evidenceIssues"].append(str(error))
        result["status"] = "NOT_VERIFIED"
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("artifact", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = evaluate(args.artifact)
    text = json.dumps(result, indent=2, allow_nan=False)
    if args.output:
        artifact = args.artifact.resolve()
        require(not args.output.resolve().is_relative_to(artifact), "Evaluator output must stay outside immutable input artifact")
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text + "\n", encoding="utf-8")
    print(text)
    return {"ACCEPTED_LOCAL_REFERENCE": 0, "NOT_ACCEPTED": 1, "NOT_VERIFIED": 2}[result["status"]]


if __name__ == "__main__":
    sys.exit(main())
