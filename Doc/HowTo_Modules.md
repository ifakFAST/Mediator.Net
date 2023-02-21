# How to create a Module for the ifak*FAST* Mediator platform

## Create project

* Create a new console application project targeting .Net 7 or .Net 4.6.1 or higher
* Add a reference to **MediatorLib**

## Create module class

A module is a public class that derives from the abstract class **Ifak.Fast.Mediator.ModuleBase** and implements all required methods, e.g. "Init", "Run", "GetAllObjects", ...

When the configuration of the module is described by a class hierarchy and serialized to an XML or JSON file, you can derive your Module from **Ifak.Fast.Mediator.Util.ModelObjectModule** (recommended). In this case all methods related to reading and modifying the configuration are already implemented. To make this work, the configuration root class and all member classes must implement the **Ifak.Fast.Mediator.Util.IModelObject** interface. Usually it is best to derive from class **Ifak.Fast.Mediator.Util.ModelObject** which implements the interface using reflection. See below for a minimum compiling example.

Only when you have special requirements, you may have to derive your module class directly from **Ifak.Fast.Mediator.ModuleBase** instead.

```csharp
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyModule
{
    public class Module : ModelObjectModule<Config>
    {
        public override async Task Run(Func<bool> shutdown)
        {
            while (!shutdown())
            {
               await Task.Delay(500);
            }
        }
    }

    public class Config : ModelObject
    {
        // TODO
    }
}
```

## Implement module class

For any reasonable module you have to override at least the methods **Init**, **Run**,  and probably **ReadVariables** and **WriteVariables**.

```csharp
public class Module : ModelObjectModule<Config>
{
    public override async Task Init(ModuleInitInfo info,
                                    VariableValue[] restoreVariableValues,
                                    Notifier notifier,
                                    ModuleThread moduleThread)
    {
        await base.Init(info, restoreVariableValues, notifier, moduleThread);
        // TODO: Initialize, e.g. restore variable values
    }

    public override async Task Run(Func<bool> shutdown)
    {
        // TODO: Start running
        while (!shutdown())
        {
            await Task.Delay(500);
        }
        // TODO: Clean up
    }

    protected override async Task OnConfigModelChanged(bool init)
    {
        await base.OnConfigModelChanged(init);
        // TODO: Handle change of configuration
    }

    public override Task<VTQ[]> ReadVariables(Origin origin,
                                              VariableRef[] variables,
                                              Duration? timeout)
    {
        // TODO: Read requested variables and return corresponding VTQ array
        // This method is only called when another module or client calls
        // Connection.ReadVariablesSync
        throw new NotImplementedException();
    }

    public override Task<WriteResult> WriteVariables(Origin origin,
                                                     VariableValue[] values,
                                                     Duration? timeout, 
                                                     bool sync)
    {
        // TODO: Write variable values and return WriteResult to indicate
        // individual write errors
        throw new NotImplementedException();
    }
}

public class Config : ModelObject
{
   public string ID { get; set; } = "Root";
   public string Name { get; set; } = "Root";

   protected override Variable[] GetVariablesOrNull(IEnumerable<IModelObject> parents)
   {
      var variable = new Variable(
              name: "Value",
              type: DataType.Float64,
              dimension: 1,
              defaultValue: DataValue.FromDouble(0.0),
              remember: true,
              history: History.IntervalDefault(Duration.FromSeconds(10)));

      return new Variable[] {
         variable
      };
   }

   // TODO: Add further members to module configuration as needed
}
```

## Implement Program.Main method

For running the Module as a dedicated process which is supervised by the Mediator core, you have to implement the Program.Main method (entry point of the console application) as follows:

```csharp
static void Main(string[] args)
{
    if (args.Length < 1)
    {
        Console.Error.WriteLine("Missing argument: port");
        return;
    }

    int port = int.Parse(args[0]);

    // Required to suppress premature shutdown when
    // pressing CTRL+C in parent Mediator console window:
    Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
    };

    var module = new MyModule.Module();
    Ifak.Fast.Mediator.ExternalModuleHost.ConnectAndRunModule("localhost", port, module);
    Console.WriteLine("Terminated.");
}
```

## Add Module to Mediator configuration

In order to use the new Module with the Mediator, you have to add a module instance description to the global Mediator configuration file (**AppConfig.xml**). There are two options of how to run a Module instance:

* Inside of the Mediator process on a dedicated thread
* Outside of the Mediator process as a dedicated process (recommended)

The second option is usually recommended because it delivers the best failure recovery, i.e. if there is a memory leak in the Module implementation, the Module process will terminate with OutOfMemory error and will be restarted automatically by the Mediator core.

Here is an example configuration for the **first option** (in process):

```XML
<Module id="IO" name="IO" enabled="true">
    <VariablesFileName>Var_IO.xml</VariablesFileName>
    <ImplAssembly>Module_IO.dll</ImplAssembly>
    <ImplClass>Ifak.Fast.Mediator.IO.Module</ImplClass>
    <Config>
        <NamedValue name="model-file" value="Model_IO.xml"/>
    </Config>
    <HistoryDBs>
        <HistoryDB name="IO" type="SQLite" prioritizeReadRequests="true">
            <ConnectionString>Filename=DB_IO.db</ConnectionString>
            <Settings>
                <string>page_size=4096</string>
                <string>cache_size=5000</string>
            </Settings>
        </HistoryDB>
    </HistoryDBs>
</Module>
```
Here is an example configuration for the **second option** (separate process):

```XML
<Module id="IO" name="IO" enabled="true">
    <VariablesFileName>Var_IO.xml</VariablesFileName>
    <!-- path is relative to current working directory: -->
    <ExternalCommand>./Bin/Module_IO/Module_IO.exe</ExternalCommand>
    <ExternalArgs>{PORT}</ExternalArgs>
    <Config>
        <NamedValue name="model-file" value="Model_IO.xml"/>
    </Config>
    <HistoryDBs>
        <HistoryDB name="IO" type="SQLite" prioritizeReadRequests="true">
            <ConnectionString>Filename=./Data/DB_IO.db</ConnectionString>
            <Settings>
                <string>page_size=4096</string>
                <string>cache_size=5000</string>
            </Settings>
        </HistoryDB>
    </HistoryDBs>
</Module>
```