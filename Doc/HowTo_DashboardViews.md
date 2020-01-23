# How to create Dashboard views

## Introduction

A Dashboard view consists of two parts:

* A C# class derived from **Ifak.Fast.Mediator.Dashboard.ViewBase** that implements the backend logic
* A web application that implements the frontend by communicating with the backend via HTTP

It is recommended (but not required) to implement the Web frontend using a modern JavaScript web framework like [Vue.js](https://vuejs.org/).

## Implementing the C# Backend

Create a new public class that inherits from **Ifak.Fast.Mediator.Dashboard.ViewBase**. The project containing the class needs to have a reference to **MediatorLib** and target .Net Core 3.1 or .Net 4.6.1 (or higher).

You need to implement at least the methods **OnActivate** and **OnUiRequestAsync**:

```csharp
    using Ifak.Fast.Mediator.Dashboard;

    [Identify(id: "MyView", bundle: "MyBundle", path: "index.html")]
    public class MyView : ViewBase
    {
        private MyViewConfig configuration = new MyViewConfig();

        public override Task OnActivate() {
            if (Config.NonEmpty) {
                configuration = Config.Object<MyViewConfig>();
            }
            return Task.FromResult(true);
        }

        public override async Task<ReqResult> OnUiRequestAsync(string command,
                                                         DataValue parameters) {
            // ...
        }
    }

    public class MyViewConfig { /* TODO: Add members */}
```

You have to attach the attribute **Ifak.Fast.Mediator.Dashboard.Identify** to the view class in order to provide additional information. The **bundle** parameter identifies the view bundle containing the web frontend implementation of the view. A view bundle is a web application that may contain multiple views, e.g. in form of separate HTML files. The **path** parameter specifies which HTML file in the view bundle to use.

The method **OnActivate** is called when the view gets activated in the Dashboard, i.e. the user navigates to the view. You can use it to register for Mediator events or do other initialization steps here, e.g. reading the view instance specific configuration.

The method **OnUiRequestAsync** is called every time a request is made from the View frontend. Use the "command" parameter to check what is requested. Use "parameters" for retrieving the additional information that have been passed from the Web application side. Use the return value **ReqResult.OK(...)** to return the result data structure or **ReqResult.Bad(error_msg)** to indicate an error.

If the view backend needs to notify the view frontend about changed data, it can do so by sending an event (via web socket).
To do so, call **Context.SendEventToUI("EventName", eventObject);**.

## Implementing the Web Frontend

The web frontend of a Dashboard view is a self contained web application that is hosted inside of an **iframe** element in the surrounding Dashboard web application.

The files of the web application (JavaScript, HTML, CSS, etc.) must be contained in a directory with the name **ViewBundle_XYZ**, where **XYZ** is the name of your view bundle as specified with bundle parameter of the **Identify** class attribute. This directory must be located inside the Dashboard web root (usually called **WebRoot_Dashboard**) next to the **App** and **ViewBundle_Generic** directories.

There a specific JavaScript functions available that must be used to facilitate the communication with the view backend and with the Dashboard frame application:

* sendViewRequest
* registerViewEventListener
* showTimeRangeSelector
* getCurrentTimeRange
* registerTimeRangeListener

These methods are available in the global **dashboardApp** object, e.g.:

```javascript
  const dashboard = window.parent['dashboardApp']
  dashboard.showTimeRangeSelector(true)
```

### sendViewRequest(request, payload, successHandler)

This method is used to send requests to the view backend. The request will be received in **OnUiRequestAsync**.

It has three parameters:

* request: A string that corresponds to the command parameter in OnUiRequestAsync
* payload: An object that specifies the parameters of the request if any
* successHandler: The callback function that will receive the result

Example for sending a command 'Init':

```javascript
  const parameters = {
    Param1: 'value of param 1',
    Param2: 'value of param 2',
  }
  const dashboard = window.parent['dashboardApp']
  dashboard.sendViewRequest('Init', parameters, (strResponse) => {
    const response = JSON.parse(strResponse)
    // use response object
  })
```

### registerViewEventListener(listener)

This method is used to enable receiving events from the view backend (via web socket). The event must have
been send by the view backend via **Context.SendEventToUI("MyEvent", eventObject);**.

```javascript
  const dashboard = window.parent['dashboardApp']
  dashboard.registerViewEventListener((eventName, eventPayload) => {
    if (eventName === 'MyEvent') {
      // process the event
    }
  })
```

### showTimeRangeSelector(show)

Use this method to show or hide the time range selector in the upper right corner of the Dashboard frame application.

```javascript
  const dashboard = window.parent['dashboardApp']
  dashboard.showTimeRangeSelector(true)
```

### getCurrentTimeRange()

Use this method to get the currently selected time range from the time range selector in the upper right corner of the Dashboard frame application.

The time range object has five members:

* **type**: either 'Last' or 'Range'
* **lastCount**: the number of time units (if **type** is 'Last')
* **lastUnit**: the time unit, e.g. 'Minutes', 'Hours', 'Days', 'Weeks', 'Months' or 'Years' (if **type** is 'Last')
* **rangeStart**: the start of the time range as a string in ISO 8601 format (if **type** is 'Range')
* **rangeEnd**: the end of the time range as a string in ISO 8601 format (if **type** is 'Range')

Use the helper class **Ifak.Fast.Mediator.Dashboard.TimeRange** in the C# backend to work with time ranges.

### registerTimeRangeListener(listener)

Use this method to receive changes to the selected time range.

```javascript
  const dashboard = window.parent['dashboardApp']
  dashboard.registerTimeRangeListener((timeRange) => {
    // respond to changed time range
  })
```

## Using the view in the Dashboard

You have to create an instance of the new view in the config model of the Dashboard module
(usually named Config/Model_Dashboard.xml). The Config child element may contain a view specific
configuration object in form of a JSON string.

```xml
<View id="uniqueID" name="Display Name" type="The Identify.id of the view">
  <Config />
</View>
```

The Dashboard module needs to know where to look for the implementation of the view backend class.
Therefore, you have to add the file name of the assembly to the **view-assemblies** parameter of the
Dashboard module instance in the Mediator configuration (Config/AppConfig.xml):

```xml
<Module id="Dashboard" name="Dashboard" enabled="true" concurrentInit="false">
  <!-- ... -->
  <Config>
    <NamedValue name="view-assemblies"
                value="./Bin/Module_EventLog/Module_EventLog.dll;./Bin/MyView/MyViewAssembly.dll"/>
    <!-- other parameters -->
  </Config>
</Module>
```