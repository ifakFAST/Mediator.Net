# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ifak*FAST* Mediator.Net is a modular platform for process monitoring and supervisory control (SCADA-like system). It uses a multi-process architecture where:
- **MediatorCore** orchestrates all modules and provides central supervision
- **Modules** run as separate processes for fault isolation (IO, Dashboard, EventLog, Calc, Publish, TagMetaData)
- **MediatorLib** provides shared base classes and utilities

## Common Commands

### Building
```bash
# Build entire solution
dotnet build Mediator.sln

# Build specific project
dotnet build MediatorCore/MediatorCore.csproj
```

### Running
```bash
# Run from source (from working directory)
dotnet MediatorCore/bin/Debug/net8.0/MediatorCore.dll

# Run with specific config
dotnet MediatorCore/bin/Debug/net8.0/MediatorCore.dll -c /path/to/AppConfig.xml

# Access web dashboard at http://localhost:8082/
# Default login: user 'ifak', password configured in AppConfig.xml
```

### Testing
```bash
# Run all tests
dotnet test MediatorLib_Test/MediatorLib_Test.csproj

# Run tests with verbose output
dotnet test MediatorLib_Test/MediatorLib_Test.csproj --verbosity normal

# Run specific test class
dotnet test MediatorLib_Test/MediatorLib_Test.csproj --filter "FullyQualifiedName~Test_VTQ"

# Run integration tests (from Run directory)
dotnet MediatorCore/bin/Debug/net8.0/MediatorCore.dll --clearDBs --config TestHistorianDBs/TestConfig.xml
```

### Web Dashboard Development
```bash
# Build web components (from WebDashboard directory)
cd ../WebDashboard/App && npm run build
cd ../ViewBundle_Generic && npm run build

# Or use the provided batch file
# cd ../WebDashboard && Build.bat

# Development with hot reload
cd ../WebDashboard/App && npm run serve
cd ../WebDashboard/ViewBundle_Generic && npm run serve
```

## Architecture

### Core Components
- **MediatorCore**: Main orchestrator (.NET 8.0 Web SDK)
- **MediatorLib**: Shared library (netstandard2.1 + net461 multi-targeting)
- **Modules**: Separate console applications communicating via TCP

### Module Development Pattern
All modules follow this structure:
1. Console application with `Program.cs` accepting TCP port argument
2. Module class inheriting from `ModuleBase` or `ModelObjectModule<T>` 
3. Connection via `ExternalModuleHost.ConnectAndRunModule(port, module)`
4. Configuration through XML model files

### Key Base Classes
- `ModuleBase`: Abstract base for all modules
- `ModelObjectModule<T>`: Recommended base with built-in configuration support
- `AdapterBase`: Base for IO protocol adapters

### Configuration System
- **AppConfig.xml**: Main system configuration (modules, database, users)
- **Model_*.xml**: Module-specific configurations (IO, Dashboard, Calc, etc.)
- All configs located in Run directory alongside executables

### Data Flow
- Variables represent time-stamped values with quality indicators
- Modules communicate through variable read/write operations
- Time series data stored in SQLite/PostgreSQL with custom compression

## Development Guidelines

### Module Development
- Inherit from `ModelObjectModule<ConfigClass>` for configuration support
- Implement required methods: `Init()`, `Run()`, `ReadVariables()`, `WriteVariables()`
- Use async patterns with `SingleThreadedAsync` base class when needed
- Handle module lifecycle properly (Start, Stop, Restart)

### Testing
- Unit tests use xUnit framework
- Test projects target .NET 8.0
- Integration tests available in `/Run/TestHistorianDBs/`

### Protocol Adapters (IO Module)
- Inherit from `AdapterBase` 
- Implement protocol-specific communication
- Handle scheduling and historization
- See `Doc/HowTo_AdapterIO.md` for detailed guidance

### Dashboard Views
- Vue.js + TypeScript + Vuetify
- Single-page applications loaded dynamically
- See `Doc/HowTo_DashboardViews.md` for view development

## Key Directories

- `MediatorCore/`: Main application entry point
- `MediatorLib/`: Shared library and base classes
- `Module_*/`: Individual modules (IO, Dashboard, EventLog, Calc, Publish)
- `../Run/`: Runtime directory with configurations and executables  
- `../WebDashboard/`: Vue.js web applications (App + ViewBundle_Generic + ViewBundle_TagMetaData)
- `../Doc/`: Development documentation and guides

## Dependencies

### Runtime Requirements
- .NET 8.0 runtime
- Node.js (for web dashboard development)

### Key NuGet Packages
- NLog (logging)
- CommandLineParser (CLI argument parsing)
- Microsoft.Data.SQLite (time series storage)
- Npgsql (PostgreSQL support)

### Protocol Libraries
- OPC Foundation libraries (OPC UA)
- ModbusTCP implementations
- MQTT client libraries
- Database providers (SQLite, PostgreSQL, MySQL, MSSQL)

## Important Configuration Notes

- Default web dashboard port: 8082
- Module communication via random TCP ports assigned by MediatorCore
- Database files stored in Run directory (DB_*.db)
- Logging configuration in NLog.config files
- Certificate handling for OPC UA and HTTPS in Run directory