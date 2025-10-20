# Creating Widgets for the Pages View Dashboard

## Architecture Overview

The Pages view uses a **client-server widget architecture** where:
- **Backend (C#)**: Widget business logic, data access, event subscriptions
- **Frontend (Vue.js 3)**: Widget UI rendering and user interaction
- **Communication**: Async request/response pattern via `backendAsync` function

---

## Backend Implementation (C#)

### 1. Create Widget Class

Location: `Mediator.Net\Module_Dashboard\Pages\Widgets\`

```csharp
[IdentifyWidget(id: "YourWidgetName")]
public class YourWidget : WidgetBaseWithConfig<YourWidgetConfig>
{
    public override string DefaultHeight => "400px";  // or "" for auto
    public override string DefaultWidth => "100%";    // or "" for auto

    // Access configuration
    YourWidgetConfig configuration => Config;
}
```

**Key points:**
- Must inherit from `WidgetBaseWithConfig<T>` where T is your config class
- Use `[IdentifyWidget(id: "...")]` attribute with unique ID
- Override `DefaultHeight` and `DefaultWidth` (empty string = auto size)

### 2. Implement Lifecycle Methods

```csharp
public override async Task OnActivate()
{
    // Called when widget becomes active (page is opened)
    // Subscribe to variable changes, history changes, etc.

    if (configuration.Variable.HasValue) {
        await Connection.EnableVariableValueChangedEvents(
            SubOptions.AllUpdates(sendValueWithEvent: true),
            configuration.Variable.Value
        );
    }
}

public override Task OnDeactivate()
{
    // Called when widget is deactivated (page is closed)
    // Clean up resources if needed
    return Task.FromResult(true);
}
```

### 3. Handle UI Requests

Methods prefixed with `UiReq_` are automatically exposed to the frontend:

```csharp
public async Task<ReqResult> UiReq_SaveConfig(string text, string mode)
{
    // Update configuration
    configuration.Text = text;
    configuration.Mode = mode;

    // Persist changes
    await Context.SaveWidgetConfiguration(configuration);

    return ReqResult.OK();
}

public async Task<ReqResult> UiReq_LoadData()
{
    // Return data to frontend
    var data = await LoadSomeData();
    return ReqResult.OK(data);
}
```

### 4. Handle Events from Mediator

```csharp
public override async Task OnVariableValueChanged(List<VariableValue> variables)
{
    // Process variable changes
    var payload = ProcessVariables(variables);

    // Send event to UI
    await Context.SendEventToUI("OnVarChanged", payload);
}

public override Task OnVariableHistoryChanged(List<HistoryChange> changes)
{
    // Handle history changes
}

public override Task OnConfigChanged(List<ObjectRef> changedObjects)
{
    // Handle config object changes
}

public override Task OnAlarmOrEvents(List<AlarmOrEvent> alarmOrEvents)
{
    // Handle alarms/events
}
```

### 5. Define Configuration Class

```csharp
public class YourWidgetConfig
{
    public string Text { get; set; } = "Default value";
    public VariableRef? Variable { get; set; } = null;
    public int MaxItems { get; set; } = 10;

    // Optional: Control serialization
    public bool ShouldSerializeVariable() => Variable.HasValue;
}
```

### 6. Context Utilities

Available through `Context` property:

```csharp
// Save widget configuration
await Context.SaveWidgetConfiguration(configuration);

// Send events to UI
await Context.SendEventToUI("EventName", payloadObject);

// Log page actions
await Context.LogPageAction("User clicked button");

// Get logged actions
LogAction[] actions = await Context.GetLoggedPageActions(limit: 100);

// Save web assets (images, etc.)
string url = await Context.SaveWebAsset(".png", byteData);

// Resolve variable references with config variables
VariableRef resolved = Context.ResolveVariableRef(unresolvedVarRef);
```

---

## Frontend Implementation (Vue.js 3)

### 1. Create Widget Component

Location: `WebDashboard\App\src\view_pages\widgets\`

```vue
<template>
  <div>
    <!-- Your widget UI -->
    <v-btn @click="onSaveConfig">Save</v-btn>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'

interface Config {
  Text: string
  Mode: 'Markdown' | 'HTML'
}

const props = defineProps<{
  id: string
  width: string
  height: string
  config: Config
  backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  eventName: string
  eventPayload: object
  timeRange: TimeRange
  resize: number
  dateWindow: number[] | null
}>()

// Your component logic here
</script>
```

**Required Props:**
- `id`: Widget instance ID
- `width`, `height`: Widget dimensions
- `config`: Widget configuration (matches backend config class)
- `backendAsync`: Function to call backend methods
- `eventName`, `eventPayload`: Events from backend
- `timeRange`, `resize`, `dateWindow`: Optional features

### 2. Call Backend Methods

```typescript
const onSaveConfig = async (): Promise<void> => {
  const para = {
    text: text.value,
    mode: textMode.value,
  }

  try {
    // Calls UiReq_SaveConfig on backend
    await props.backendAsync('SaveConfig', para)
  } catch (err: any) {
    alert(err.message)
  }
}

// Load data on mount
onMounted(async () => {
  const response = await props.backendAsync('LoadData', {})
  data.value = response
})
```

### 3. Handle Backend Events

```typescript
watch(
  () => props.eventPayload,
  (newVal: any) => {
    if (props.eventName === 'OnVarChanged') {
      // Handle variable change event from backend
      updateUI(newVal)
    }
  }
)
```

### 4. Register Widget in WidgetWrapper

Edit `WebDashboard\App\src\view_pages\WidgetWrapper.vue`:

```vue
<script setup lang="ts">
import YourWidget from './widgets/YourWidget.vue'
</script>

<template>
  <!-- Add your widget -->
  <your-widget
    v-if="type === 'YourWidgetName'"
    :id="id"
    :backend-async="backendAsync"
    :config="config as any"
    :event-name="eventName"
    :event-payload="eventPayload"
    :height="height"
    :width="width"
    :time-range="timeRange"
    :resize="resize"
    :date-window="dateWindowForComponents as any"
  ></your-widget>
</template>
```

---

## Data Model

### Page Structure

Located in `Mediator.Net\Module_Dashboard\Pages\Config.cs`:

```
Config
└── Pages[]
    └── Page
        ├── ID, Name
        └── Rows[]
            └── Row
                └── Columns[]
                    └── Column
                        ├── Width (Fill, Auto, 1-12 of twelve)
                        └── Widgets[]
                            └── Widget
                                ├── ID (instance ID)
                                ├── Type (widget type)
                                ├── Title, Width, Height
                                └── Config (JObject)
```

---

## Examples

### Simple Widget (TextDisplay)

**Backend**: `Mediator.Net\Module_Dashboard\Pages\Widgets\TextDisplay.cs`
**Frontend**: `WebDashboard\App\src\view_pages\widgets\TextDisplay.vue`

- Displays Markdown or HTML
- Single `UiReq_SaveConfig` method
- No event subscriptions

### Complex Widget (VarTable)

**Backend**: `Mediator.Net\Module_Dashboard\Pages\Widgets\VarTable.cs`
**Frontend**: `WebDashboard\App\src\view_pages\widgets\VarTable.vue`

- Subscribes to variable value and history changes
- Multiple UI request methods
- Event handling for real-time updates
- Trend calculation

---

## Best Practices

1. **Naming Convention**: Use `UiReq_` prefix for backend methods callable from UI
2. **Error Handling**: Always return `ReqResult.OK(data)` or `ReqResult.Bad(message)`
3. **Event Pattern**: Backend sends events via `Context.SendEventToUI()`, frontend watches `eventPayload`
4. **Configuration**: Always persist config changes via `Context.SaveWidgetConfiguration()`
5. **Lifecycle**: Subscribe to events in `OnActivate()`, clean up in `OnDeactivate()`
6. **Type Safety**: Match TypeScript config interface with C# config class properties

---

This architecture allows for flexible, real-time dashboard widgets with clear separation between business logic (backend) and presentation (frontend).
