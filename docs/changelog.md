# Changelog

This project tracks deliverables mapped iteratively across the implementation block.

### Phase 8b - UX / Tray Refinement (Current)
- **Tray Icon:** Generated `shield.ico` and embedded it as a WPF `<Resource>` in `Detector.GUI.csproj`. Resolved via pack URI — no missing-resource crash on launch.
- **Balloon Notifications:** Severity-aware titles (CRITICAL / HIGH / MEDIUM / LOW) with process name and PID in each balloon tip.
- **Dashboard:** Added EVENTS DROPPED panel (amber when non-zero), UPTIME panel, alert count label, and status bar that reflects live collector state. Alert rows now show a colour-coded severity pill badge and formatted timestamps.
- **AlertRowViewModel:** Introduced a thin wrapper around `DetectionAlert` for cleaner per-row bindings in XAML.
- **Elevation Restore:** Reinstated the UAC elevation relaunch that was temporarily commented out for debugging.
- **Docs:** Updated `docs/modules/GUI.md`.

### Phase 8 - Evaluation Readiness & Hardening
- **Hardening:** Added `System.ComponentModel.DataAnnotations` validating `ActDefendOptions` at startup ensuring graceful startup crashes cleanly avoiding silent bad metrics safely.
- **ETW Safety:** Internal updates gracefully handling unexpected heavy processing loop drops directly within `EtwEventCollector` protecting the UI pipeline from zombie state mappings linearly natively.
- **Docs:** Fully populated all core documentation requirements.

### Phase 7 - Safe Simulator & End-to-End Validation
- **Simulator Configs:** Updated `Detector.Simulator` correctly mapping configurable file outputs, depth arrays mimicking clean `DirectorySpread` configurations locally.
- **Headless Tests:** Configured robust decoupled integration loops spinning up native decoupled UI-Less testing matrices asserting strict False-Positive bounding natively within SQLite Database endpoints reliably.

### Phase 6 - GUI Dashboard 
- **WPF Implementation:** Delivered bounded Taskbar execution mapped nicely with internal status properties over a `MainWindow` UI perfectly matching `IMonitoringStatus` limits nicely without breaking core limits recursively cleanly.

### Phase 5 - Entropy Analysis (Stage 2)
### Phase 4 - Detection Engine (Stage 1)
### Phase 3 - Feature Extractor
### Phase 2 - Event Collector
### Phase 1 - Solution Foundations
