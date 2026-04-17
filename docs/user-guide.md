# User Guide

ActDefend operates as a Background UI Desktop Application. After granting Administrator elevation metrics, the tool sits actively tracing limits softly mapped exclusively via ETW.

## System Dashboard Visuals
When actively running, the User Interface displays active system metrics updating continuously:
- **ELEVATION:** Green (`Active`) indicates Administrator bounds correctly established. Red indicates missing privileges.
- **COLLECTOR:** Green (`Running`) indicates ETW is cleanly hooking kernel metrics.
- **EVENTS PROCESSED:** Total File IO metrics ingested.
- **TRACKED PROCESSES:** Active programs currently being analyzed by the Feature Extractor.
- **EVENTS DROPPED:** Amber warning if pipeline backpressure forces ingestion skips.
- **UPTIME:** Duration the ActDefend daemon has been active.

## Recent Alerts Display
Any suspicious mapping (usually tracked initially via the Safe Simulator executable) surfaces directly within the central table:
- Displaying `DetectionAlert` bounds.
- Capturing rapid `.locked` traversal logic and `Score` outputs.

## Managing False Positives (Trusted Rules)
Legitimate operational workflows (e.g., executing `7zip`, large localized development string manipulation) can accidentally cross rigid bounds inside Stage 1 processing limits. 

The `TrustedProcessRepository` natively accepts explicit process allowances resolving exclusions mapping perfectly directly to SQLite overrides locally preventing false-positive loops permanently against identical `.exe` streams.
