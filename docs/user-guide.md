# User Guide

ActDefend operates as a Background UI Desktop Application. After granting Administrator elevation metrics, the tool sits actively tracing limits softly mapped exclusively via ETW.

## System Dashboard Visuals
When actively running, the User Interface displays active system metrics:
- **ELEVATION:** `elevated` - Administrator bounds correctly established.
- **COLLECTOR:** `running` - ETW `SessionName` cleanly hooking kernel metrics.
- **PROCESSES TRACKED:** Aggregated `ActiveProcessCount`.

## Recent Alerts Display
Any suspicious mapping (usually tracked initially via the Safe Simulator executable) surfaces directly within the central table:
- Displaying `DetectionAlert` bounds.
- Capturing rapid `.locked` traversal logic and `Score` outputs.

## Managing False Positives (Trusted Rules)
Legitimate operational workflows (e.g., executing `7zip`, large localized development string manipulation) can accidentally cross rigid bounds inside Stage 1 processing limits. 

The `TrustedProcessRepository` natively accepts explicit process allowances resolving exclusions mapping perfectly directly to SQLite overrides locally preventing false-positive loops permanently against identical `.exe` streams.
