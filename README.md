# ifak*FAST* Mediator

## Modular platform for process monitoring and supervisory control

The [ifak*FAST*](https://fast.ifak.eu/) Mediator enables the composition and integration of modules that provide specific functionality for generic automation needs including data acquisition, visualization, alarm management and control. It can be used to build SCADA-like applications by combining generic modules like data acquisition with application specific modules, e.g. for asset management or online sensor quality evaluation.

The Mediator core is responsible for supervision and integration of the modules and provides time series data management and role-based rights management. Higher-level functionality needs to be provided by modules. A module is a software component with a specific configuration model (typically in form of an XML file) that defines a set of variables. A variable represents a runtime changing value with timestamp and quality, e.g. a measurement or set-point. A module may read and write variables and the configuration of other modules and may provide specific services for use by other modules.

Running the Mediator requires [.Net Core 2.1](https://www.microsoft.com/net/download) or newer. Future versions of the Mediator will allow for creating modules with Java.

The Mediator core and all generic modules in this repository are licensed under the MIT License. We offer [professional support](https://fast.ifak.eu/contact) for development and customization of ifak*FAST* based solutions.

## Available generic modules

### Module **IO**

* Used for signal-based data acquisition, e.g. via OPC DA
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

### Module **Simba# Control** (not part of open-source distribution)

* Enables model-based supervisory control solutions
* Define control model by flow-based diagrams with [SIMBA#](https://simba.ifak.eu/)
* Evaluate control solution by integrated simulation of process and control model

## Further documentation
* IO adapter implementation for custom data sources: [HowTo_AdapterIO](./Doc/HowTo_AdapterIO.md)
* Module implementation for application logic: [HowTo_Modules](./Doc/HowTo_Modules.md)
* Dashboard view implementation for application specific user interfaces: [HowTo_DashboardViews](./Doc/HowTo_DashboardViews.md)
* [ifak*FAST* website](https://fast.ifak.eu)