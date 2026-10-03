using AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance;
if (args.FirstOrDefault() == "frame-controls") return RichFrameControls.Run();
string Option(string key, string fallback) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
return SingleThreadOwner.Run(() => new RichRunner(args.FirstOrDefault() ?? "help", Option("--case", "manual-paused"), Option("--run-id", "missing"), Option("--ip", "127.0.0.1"), int.Parse(Option("--port", "0")), Option("--topology", "SeparateHostsRequiresPairedEvidence"), Option("--source", "UNSET"), Option("--dirty", "UNSET"), Path.GetFullPath(Option("--report", "rich-report.json"))).Run());
