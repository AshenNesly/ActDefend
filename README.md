# ActDefend: Lightweight Behavioural Ransomware Detection System

ActDefend is a specialized Windows desktop ransomware early-warning system built explicitly for low-resource environments (e.g., SMEs, educational labs, individual endpoints). The system operates heavily in user-space utilizing low-overhead Event Tracing for Windows (ETW) architecture to monitor per-process file behaviors dynamically.

## High-Level Architecture
The system intentionally avoids heavy Machine Learning (ML) constraints to maintain an incredibly small footprint across limited compute contexts:

`Windows Event Source (ETW) -> Monitor/Collector -> Feature Extractor -> Detection Engine -> Local Storage / UI`

1. **Lightweight Scoring (Stage 1):** Fast metric aggregation utilizing short sliding windows over disk interaction rates (renames, distinct traversals, writes).
2. **Entropy Confirmation (Stage 2):** Capped execution only triggering conditional payload analysis strictly when suspicions breach preconfigured thresholds.

## Requirements
- **OS:** Windows 10/11 64-bit
- **Runtime:** .NET 10
- **Privileges:** Administrator mapping absolutely required for ETW hooks.

## Quick Start
To review internal configurations and run loops manually, navigate through `docs/`.
* [Installation](docs/installation.md)
* [Configuration limits](docs/configuration.md)
* [Technical Design](docs/technical-design.md)
* [Testing Limits & Evaluation Plan](docs/testing-and-evaluation.md)
