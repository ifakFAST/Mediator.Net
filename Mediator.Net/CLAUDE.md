# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Mediator.Net is a modular platform for process monitoring and supervisory control built on .NET 10.0. It follows a distributed architecture where a core orchestration engine dynamically loads independent modules at runtime.

## Build Commands

```bash
# Build entire solution
dotnet build Mediator.sln

# Build in release mode
dotnet build Mediator.sln -c Release

# Run tests
dotnet test MediatorLib_Test/MediatorLib_Test.csproj

# Run a single test
dotnet test MediatorLib_Test/MediatorLib_Test.csproj --filter "FullyQualifiedName~TestName"
```

## Running the Application

```bash
# Basic run (from MediatorCore output directory)
MediatorCore.exe -c AppConfig.xml

# Available CLI options
MediatorCore.exe -c <config_file> -t <title> -l <log_dir> -n <log_name>
MediatorCore.exe encrypt <text>   # Encrypt text for use in config files
```

## Architecture

### Module System

Each major subsystem is an independent module loaded by `MediatorCore` at startup. Modules run on dedicated threads and communicate with the core via a `Notifier` interface.

```
MediatorCore (orchestrator)
├── Module_IO       — Protocol adapters (MQTT, OPC-UA, Modbus, SQL, HTTP, Modbus, etc.)
├── Module_Calc     — C# scripting and Python-based calculations
├── Module_Dashboard — ASP.NET Core web UI
├── Module_EventLog — Alarm and event logging, email/MQTT notifications
├── Module_Publish  — Data publishing (MQTT, OPC-UA)
└── Module_TagMetaData — Tag metadata management
```

All modules inherit from `ModuleBase` (in `MediatorLib`) and implement:
- `Init()` — module initialization with config
- `Run(shutdown)` — main execution loop
- `GetAllObjects()` — expose the module's object tree
- `ReadVariables()` / `WriteVariables()` — data access

### Core Data Type: VTQ

Every variable value is represented as a `VTQ` (Value-Timestamp-Quality):
- `V` — `DataValue` (typed: Double, Int, Bool, String, etc.)
- `T` — `Timestamp` (when the value was recorded)
- `Q` — `Quality` enum (Good, Bad, Uncertain)

`VTTQ` is an extended variant with separate collection and transmission timestamps.

### API / Request Handling

The core hosts an HTTP/WebSocket server on a configured port (path prefix `/Mediator/`). All ~80+ API endpoints are defined in `MediatorLib/RequestDefs.cs`. Requests are processed by `HandleClientRequests.cs` in `MediatorCore`. Both binary and JSON serialization are supported.

### Configuration

The system is configured via XML (`AppConfig.xml`). Config classes are in `MediatorLib/Config.cs`. Sensitive values in config can be encrypted using the `encrypt` CLI command and `SimpleEncryption.cs`.

### History / Timeseries

`HistoryManager.cs` manages time-series persistence. Supported backends: SQLite, PostgreSQL. Key classes: `TimeSeriesDB.cs`, `CompressedTimeseriesReader.cs`, `HistoryAggregationCache.cs`.

### IO Adapters (Module_IO)

Adapters are loaded dynamically from external assemblies. Each adapter type (MQTT, OPC-UA, OPC Classic, Modbus, SQL, HTTP, PI, TextFile, FAST) implements a common adapter interface.

## Project Structure

| Project | Purpose |
|---|---|
| `MediatorCore` | Main executable; orchestrates module loading, HTTP/WS server, history |
| `MediatorLib` | Shared library (netstandard2.1 + net461); core types, interfaces, serialization |
| `Module_IO` | I/O adapter module |
| `Module_Calc` | C# scripting + Python calculation engine |
| `Module_Dashboard` | Web-based monitoring dashboard |
| `Module_EventLog` | Event/alarm logging and notifications |
| `Module_Publish` | MQTT/OPC-UA data publishing |
| `Module_TagMetaData` | Tag metadata management |
| `MediatorLib_Test` | xUnit tests (serialization-focused) |

## Code Conventions

- C# 14.0, .NET 10.0, file-scoped namespaces, nullable reference types enabled
- Coding style enforced via `.editorconfig` (369 lines) — PascalCase types, camelCase locals
- MIT license header required on all source files (see `.editorconfig` for template)
- Version is defined in `MediatorLib/MediatorLib.csproj` (`<Version>`)
