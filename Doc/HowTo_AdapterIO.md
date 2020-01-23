# How to create an adapter for the **IO** Module

## Create project

* Create a new class library project targeting .Net 4.6.1 or .Net Core 3.1
* Add a reference to **MediatorLib**

## Create adapter class

An IO adapter is a class that inherits form the abstract class **Ifak.Fast.Mediator.IO.AdapterBase**.

The public class needs to have an attribute of type `Identify(string id)` attached. The id will be used in the Adapter configuration for the Type attribute.

```csharp
    [Ifak.Fast.Mediator.IO.Identify("OPC")]
    public class OPC : Ifak.Fast.Mediator.IO.AdapterBase
    {
       // ...
    }
```

The following methods need to be implemented:

* bool **SupportsScheduledReading** { get; }
* Task<Group[]> **Initialize**(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos)
* Task<VTQ[]> **ReadDataItems**(string group, IList<ReadRequest> items, Duration? timeout)
* Task<WriteDataItemsResult> **WriteDataItems**(string group, IList<DataItemValue> values, Duration? timeout)
* Task<string[]> **BrowseAdapterAddress**()
* Task<string[]> **BrowseDataItemAddress**(string idOrNull)
* Task **Shutdown**()

Each IO adapter instance is run on a dedicated thread. Therefore, it is possible to implement the adapter in a purely synchronous fashion, i.e. without using the asynchronous features of C# async/await.

## Implement adapter class

### SupportsScheduledReading

If this property returns false then no calls to ReadDataItems will happen, i.e. changes to readable DataItems must be reported using AdapterCallback.Notify_DataItemsChanged. For most adapters, you would return true.

### Initialize

This method must initialize the adapter instance given its configuration and return an array of groups.

A group indicates a set of DataItems that originate from the same source, i.e. a communication problem while reading/writing one data item likely also affects reads/writes to other DataItems of the same group. Therefore, `ReadDataItems` and `WriteDataItems` will be called only for DataItems of the same group:

```csharp
public struct Group
{
    // The ID will be used as first parameter in ReadDataItems
    // and WriteDataItems
    public string ID;
    public string[] DataItemIDs;
}
```
This method may return an empty array of groups, when there are no independent DataItems in the `Adapter` configuration.

### Optional: StartRunning

If the adapter is not purely passive (only responds to read and write requests), you have to override the StartRunning method to know when it is save to start any background task or thread that may report data changes via `callback.Notify_DataItemsChanged`.

### ReadDataItems

This method is called by the IO module to read DataItems of a specific group. If individual DataItem reads fail, this should be indicated by Quality.Bad in the corresponding VTQ entry of the returned array. Any exception is considered an error and leads to adapter restart.

### WriteDataItems

This method is called by the IO module to write DataItems of a specific group. If individual DataItem writes fail, this must be indicated by the `WriteDataItemsResult` return value. Any exception is considered an error and leads to adapter restart.

### BrowseAdapterAddress

This method is called by the IO module to query possible values for the Address property of the Adapter. If Browsing is not supported for Adapter.Address, return a string array of length zero (new string[0]).

### BrowseDataItemAddress

This method is called by the IO module to query possible values for the Address property of a DataItem. If Browsing is not supported for DataItem.Address, return a string array of length zero (new string[0]).

### Shutdown

Called by the IO module to shutdown this Adapter instance. All resources should be freed during shutdown.

## Understanding the adapter configuration

The configuration for the adapter is passed as a parameter to the Initialize method in form of an object of type `Adapter`. Below you can see the members of this class.

```csharp
public class Adapter
{
    public string ID;
    public string Name;
    public string Type;
    public string Address;
    public List<NamedValue> Config;
    public List<Node> Nodes;
    public List<DataItem> DataItems;
    public List<DataItem> GetAllDataItems();
}
```
The adapter configuration is essentially a set of DataItems that are arranged in a tree of Nodes (the Adapter forms the root of this tree). Whether a Node carries relevant information or is used purely for organizing purposes is Adapter specific. For instance, an Adapter that implements a device oriented protocol may represent each device by a Node and read the device address from the Config member.

Below you can see the relevant parts of the `Node` class.

```csharp
public class Node
{
    public string ID;
    public string Name;
    public List<NamedValue> Config;
    public List<Node> Nodes;
    public List<DataItem> DataItems;
    public List<DataItem> GetAllDataItems();
}
```
Each `DataItem` in the **IO** module configuration corresponds to one `Mediator.Variable`. The Address property shall contain the primary information needed to identify this data item / variable within the Adapter specific protocol. For instance, an Adapter implementing OPC DA would expect the OPC tag name. If additional configuration information is required, the Config property can be used for this.

```csharp
public class DataItem
{
    public string ID;
    public string Name;
    public string Unit;
    public DataType Type;
    public string TypeConstraints;
    public int Dimension;
    public string[] DimensionNames;
    public bool Read;
    public bool Write;
    public string Address;
    public List<NamedValue> Config;
    public DataValue GetDefaultValue();
}
```

## Eventing

If the adapter shall support event-based data reading with minimal delays (instead of just polling via ReadDataItems), the `Notify_DataItemsChanged` method of the `AdapterCallback` object can be used (passed as parameter to Initialize method). This object also allows to emit alarms and events using `Notify_AlarmOrEvent` method, e.g. in order to provide detailed information about a communication failure.

The `Notify_DataItemsChanged` method can also be used for batch reporting value changes, i.e. the same DataItem id but different values with different timestamps.

```csharp
public interface AdapterCallback
{
    void Notify_DataItemsChanged(DataItemValue[] values);
    void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo);
}
```

## Making the Adapter available to the IO Module

The IO module can only use the adapter implementation if the corresponding class can be found. Therefore, you need to add the file name of the assembly to the **adapter-assemblies** config item of the IO module in the global AppConfig.xml (several files are separated by semicolon).

## Example configuration for Module IO

The example configuration below defines a single adapter instance for OPC DA with three data items for reading. No nodes are used in this case because this is not required by the OPC adapter implementation. Note that the name attribute of a DataItem is optional (the value of the id attribute will be used instead if no name is specified).

```xml
<IO_Model xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
          xmlns:xsd="http://www.w3.org/2001/XMLSchema"
          xmlns="Module_IO">

  <Scheduling mode="Interval" interval="5 s" useTimestampFromSource="false" />
  <History    mode="Interval" interval="1 min" />
  <Adapters>
    <Adapter id="opc" name="OPC" type="OPC" address="Matrikon.OPC.Simulation">
      <Config>
        <NamedValue name="SourceCache"  value="true" />
      </Config>
      <DataItems>
        <DataItem id="A"
                  name="Real4"
                  type="Float32"
                  read="true"
                  address="Bucket Brigade.Real4" />
        <DataItem id="B"
                  name="UInt2"
                  type="Int"
                  read="true"
                  address="Bucket Brigade.UInt2" />
        <DataItem id="C"
                  name="ArrayOfReal8"
                  type="Float64"
                  read="true"
                  dimension="0"
                  address="Bucket Brigade.ArrayOfReal8" />
      </DataItems>
    </Adapter>
  </Adapters>
</IO_Model>
```
