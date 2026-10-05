# Dot decision

Flow ID / request ID / type:

Conversation/reply identity/raw full reply reference:

Reviewed full SHA / decision: accept-plan | accept-candidate | needs-revision | blocked | ambiguous

reply_posted_utc (null if unknown) / reliability + source / observed_utc:

timing_basis / trusted upper UTC + uncertainty / time source / optional lower UTC:

Complete/not-generating proof / full+prior raw snapshots / verified request boundary:

Timing: timely | timely-late-read | late | unknown-time

Original deadline / supplement intent+receipt / prior timeout+pause refs:

Accepted scope/delivered hashes (final acceptance) / blockers/conflicts:

Main scope/evidence finding / next allowed action/required authority:

New linked request ID + Owner reopening or valid revision provenance (if needed):

Reliable complete observation within deadline proves timeliness without invented posted UTC. Late/unknown-time/wrong-SHA replies do not reopen expired requests. Timely late-read preserves/enters pause even if old pause flag was stale, and does not restore dispatch/write rights. Evidence commits list diff from frozen accepted candidate and prove hashes unchanged. [Dialogue](../references/dot-dialogue.md) owns these rules.
