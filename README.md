# ifak*FAST* Mediator

## Modular platform for process monitoring and supervisory control

The [ifak*FAST*](https://fast.ifak.eu/) Mediator enables the composition and integration of modules that provide specific functionality for generic automation needs including data acquisition, visualization, alarm management and control. It can be used to build SCADA-like applications by combining generic modules like data acquisition with application specific modules, e.g. for asset management or online sensor quality evaluation.

The Mediator core is responsible for supervision and integration of the modules and provides time series data management and role-based rights management. Higher-level functionality needs to be provided by modules. A module is a software component with a specific configuration model (typically in form of an XML file) that defines a set of variables. A variable represents a runtime changing value with timestamp and quality, e.g. a measurement or set-point. A module may read and write variables and the configuration of other modules and may provide specific services for use by other modules.

Running the generic Mediator distribution requires that [.NET 10](https://dotnet.microsoft.com) has been installed. Platform specific distributions for Windows x64 and Linux x64 are provided that do not require a .NET runtime installation.

The Mediator core and all generic modules in this repository are licensed under the MIT License. We offer [professional support](https://www.ifak-ts.com/#kontakt) for development and customization of ifak*FAST* based solutions.

## Available generic modules

### Module **IO**

* Used for signal-based data acquisition, e.g. via OPC DA, OPC UA, ModbusTCP, SQL, MQTT
* Extensible through adapters for different protocols
* Configuration of scheduling and historization

### Module **Dashboard**

* Provides a web-based dashboard for visualization and interaction
* A dashboard consists of a set of customizable views, e.g. for IO and alarms and events
* Extensible by providing your own views in form of single-page web apps

### Module **EventLog**

* Used for management of events (like warnings and alarms) that are sent by modules
* Enables the acknowledgement and reset of warnings and alarms
* Enables notifications to users, e.g. by e-mail

### Module **Calculation**
* Define cyclic calculations, e.g. for control or key-performance-indicator calculation
* Two types of calculation available: C# scripts, Python scripts, and [SIMBA#](https://simba.ifak.eu/)
* Enables model-based supervisory control solutions
* Evaluate control solution by integrated simulation of process and control model

### Module **Publish**
* Publish variable values and histories to external systems
* Supports cyclic publishing and event-driven publishing
* Includes MQTT and OPC UA publishing options

### Module **TagMetaData**
* Manage tag metadata and relationships
* Includes a visual editor for tag models and block libraries

## Quick Start
1. Get the [latest release](https://github.com/ifakFAST/Mediator.Net/releases/latest)
2. Unzip
3. Run: Either start `Run.bat` on Windows or `./Run.sh` on Linux (in the release package)
4. Navigate to http://localhost:8082/ using the browser
5. Login with user name and password, for default values see `ReadMe.txt` (release package) or `AppConfig.xml`

## Further documentation
* IO adapter implementation for custom data sources: [HowTo_AdapterIO](./Doc/HowTo_AdapterIO.md)
* Module implementation for application logic: [HowTo_Modules](./Doc/HowTo_Modules.md)
* Dashboard view implementation for application specific user interfaces: [HowTo_DashboardViews](./Doc/HowTo_DashboardViews.md)
* [ifak*FAST* website](https://fast.ifak.eu)
