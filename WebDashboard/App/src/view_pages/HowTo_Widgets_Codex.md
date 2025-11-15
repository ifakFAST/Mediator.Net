# How to Create Widgets for the Pages View

This guide shows how to add a new widget that appears in the **Pages** dashboard view. It covers both the backend (.NET) implementation and the Vue-based frontend that renders inside the dashboard frame.

---

## Prerequisites

- Working build of `Mediator.Net` with the Dashboard module enabled.
- Familiarity with C# and .NET 7 (widgets are compiled into the dashboard backend).
- Working knowledge of Vue 3 + TypeScript (frontend widgets live under `WebDashboard/App/src/view_pages`).
- Ability to rebuild the dashboard backend and run `npm run dev` (or `npm run build`) for the frontend bundle.

---

## High-Level Workflow

1. **Backend:** Implement a C# widget class deriving from `WidgetBase` (or `WidgetBaseWithConfig<T>`) inside `Mediator.Net/Module_Dashboard/Pages/Widgets`. Decorate it with `[IdentifyWidget]` and expose any UI commands via `UiReq_` methods.
2. **Frontend:** Create a Vue component under `WebDashboard/App/src/view_pages/widgets` that matches the widget ID, consumes the shared props contract, and calls back to the backend through the provided `backendAsync` delegate.
3. **Expose in UI:** Import the component in `WidgetWrapper.vue` and add a `type === 'MyWidget'` branch so the renderer can instantiate it. No further registration is needed; the backend reflection picks it up automatically.
4. **Test:** Add the widget through the Page editor, adjust config, and ensure live data flows as expected.

---

## Backend Implementation

Backend widgets live in `Mediator.Net/Module_Dashboard/Pages/Widgets`. They are compiled into the Dashboard module and loaded via reflection when the view activates (`View.cs:38-76`).

### 1. Create the Widget Class

```csharp
using Ifak.Fast.Mediator.Dashboard.Pages;

[IdentifyWidget(id: "MyWidget")]
public class MyWidget : WidgetBaseWithConfig<MyWidgetConfig>
{
    public override string DefaultHeight => "300px";
    public override string DefaultWidth => "100%";

    public override Task OnActivate() {
        // Subscribe to variables, preload data, etc.
        return Task.CompletedTask;
    }

    public async Task<ReqResult> UiReq_SaveConfig(string title)
    {
        Config.Title = title;
        await Context.SaveWidgetConfiguration(Config);
        return ReqResult.OK();
    }
}

public class MyWidgetConfig
{
    public string Title { get; set; } = "";
}
```

Key points:

- `IdentifyWidget` ID must match the frontend type string.
- `WidgetBaseWithConfig<T>` auto-deserializes `widget.Config` into the strongly typed `Config` property.
- Override `DefaultHeight` and `DefaultWidth` so the Page layout has sensible defaults when the widget is added (`View.cs:700-708`).

### 2. Use the Widget Context

`WidgetBase` provides a `Context` (`WidgetBase.cs:22-68`) with helpers used throughout existing widgets:

- `SaveWidgetConfiguration(object config)` persists the config and notifies the UI (`View.cs:482-504`).
- `SendEventToUI(string name, object payload)` pushes events that `ViewPages.vue` will route to the active page (`ViewPages.vue:137-160`).
- `LogPageAction`, `GetLoggedPageActions`, and `GetPageActionLogVariable` integrate with the page action log widget.
- `ResolveVariableRef` converts unresolved references into a concrete `VariableRef` once the backend has access to the session.

### 3. Handle Requests from the Frontend

- Any public method named `UiReq_*` becomes remotely callable (`WidgetBase.cs:118-165`).
- The frontend calls these via `props.backendAsync('SaveConfig', payload)`; the request reaches `View.cs:469-480` and is dispatched to your widget through `PageState.ForwardWidgetRequest`.

### 4. Optional Event Hooks

Override these methods if your widget needs live data:

| Method                     | Purpose                                                 |
| -------------------------- | ------------------------------------------------------- |
| `OnVariableValueChanged`   | Receive value updates for subscribed variables.         |
| `OnVariableHistoryChanged` | Handle historian changes for trend-oriented widgets.    |
| `OnConfigChanged`          | Respond to configuration changes elsewhere in Mediator. |
| `OnAlarmOrEvents`          | Process alarms/events.                                  |

Each hook is fanned out by `PageState` (`View.cs:762-821`) to every instanced widget.

### 5. Configuration Schema

Widget layouts and configs are serialized into the dashboard view configuration file using the DTO in `Config.cs:79-118`. When your widget saves config data:

1. The object is converted to `JObject` and stored on the widget (`View.cs:482-504`).
2. The full View config is persisted through `Context.SaveViewConfiguration`.
3. A `WidgetConfigChanged` event is sent to the frontend so the live component updates.

Ensure your config type is JSON-friendly (properties with getters and setters, no circular references).

---

## Frontend Implementation

Frontend components live in `WebDashboard/App/src/view_pages/widgets`. They are rendered within the Page editor/runtime by `Page.vue` and `WidgetWrapper.vue`.

### 1. Create the Vue Component

```vue
<template>
  <div class="my-widget">
    <p class="title">{{ resolvedTitle }}</p>
    <v-btn @click="saveTitle">Save</v-btn>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { TimeRange } from '../../utils'
import * as model from '../model'

interface Config {
  Title: string
}

const props = defineProps<{
  id: string
  width: string
  height: string
  paddingOverride: string
  config: Config
  backendAsync: (request: string, parameters: object, responseType?: 'text' | 'blob') => Promise<any>
  eventName: string
  eventPayload: object
  timeRange: TimeRange
  resize: number
  dateWindow: number[] | null
  configVariables: model.ConfigVariableValues
  setConfigVariableValues: (values: Record<string, string>) => void
}>

const newTitle = ref(props.config.Title)

const resolvedTitle = computed(() => {
  return model.VariableReplacer.replaceVariables(newTitle.value, props.configVariables.VarValues, '?')
})

const saveTitle = async () => {
  await props.backendAsync('SaveConfig', { title: newTitle.value })
}
</script>

<style scoped>
.my-widget {
  min-height: 200px;
}
</style>
```

Implementation notes:

- **Props contract:** Match the existing widgets (`WidgetWrapper.vue:187-246`).
- **Backend requests:** Always use `props.backendAsync`; it automatically routes through `UiReq_RequestFromWidget`.
- **Config variables:** Use `model.VariableReplacer` if your widget supports template titles (`WidgetWrapper.vue:219-234`).
- **Events:** If the backend sends events via `SendEventToUI`, watch `props.eventName` / `props.eventPayload` for updates.

### 2. Register in `WidgetWrapper.vue`

1. Import your component at the top of `WidgetWrapper.vue`.
2. Add a branch similar to the existing ones:

```vue
<my-widget
  v-if="type === 'MyWidget'"
  :id="id"
  :backend-async="backendAsync"
  :config="config as any"
  :date-window="dateWindowForComponents as any"
  :event-name="eventName"
  :event-payload="eventPayload"
  :height="height"
  :resize="resize"
  :time-range="timeRange"
  :width="width"
  :config-variables="configVariables"
  :set-config-variable-values="setConfigVariableValues"
  @date-window-changed="onDateWindowChanged"
></my-widget>
```

Once the backend class compiles and the frontend is rebuilt, the widget type automatically appears in the "Add Widget..." dialog (`Page.vue:566-606`).

### 3. Optional: Context Menus & Permissions

- Use the standard context-menu pattern (see `TextDisplay.vue:12-77`) if your widget needs a settings dialog.
- Check `dashboardApp.canUpdateViewConfig()` in `onMounted` to disable editing for read-only users.
- For long-running operations, leverage Vuetify progress indicators to match existing UX.

---

## Working with Page Layout

The page editor accesses backend layout APIs at `View.cs:206-356`. When your widget is added:

- `ConfigWidgetAdd` creates a new instance, calls `WidgetInit`, and injects defaults.
- The updated page snapshot returns to `Page.vue`, which swaps it into the local state (`Page.vue:314-333`).
- `ViewPages.vue` keeps a map of widgets for the active page and merges config or event updates in-place (`ViewPages.vue:137-214`).

If you need to modify layout data (e.g., custom columns), prefer extending the backend layout DTOs rather than mutating them in the frontend.

---

## Testing Checklist

- Add the widget via **Edit Page Layout -> Add Widget...** and verify the default size.
- Confirm `UiReq_` handlers respond (e.g., save config, load initial data).
- Reload the dashboard: ensure the widget rehydrates from the persisted JSON config.
- If events are used, trigger them server-side and watch `eventName/eventPayload` updates.
- Remove the widget and confirm `ConfigWidgetDelete` runs without errors (check logs for exceptions).

---

## Troubleshooting

| Symptom                                            | Likely Cause                                          | Fix                                                                                                     |
| -------------------------------------------------- | ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Widget type missing in Add dialog                  | `[IdentifyWidget]` attribute missing or duplicate ID. | Ensure the backend class is decorated and rebuild the Dashboard module.                                 |
| Frontend crashes with `Unknown widget type`        | `WidgetWrapper.vue` branch missing.                   | Import the component and add the `v-if` clause with the correct type string.                            |
| Config changes are not persisted                   | `SaveWidgetConfiguration` not called.                 | Use `Context.SaveWidgetConfiguration` in your `UiReq_` method.                                          |
| Frontend backend calls fail with `Unknown command` | Method not named `UiReq_*`.                           | Prefix backend handlers with `UiReq_` so `WidgetBase` registers them.                                   |
| Variable placeholders stay unresolved              | Missing config variable values.                       | Ensure the page config variables are defined and update them via the provided APIs (`View.cs:112-170`). |

---

## Next Steps

- Review existing widgets (e.g., `TextDisplay`, `HistoryPlot`) for advanced patterns such as streaming updates, dialogs, and asset uploads.
- Extend the documentation with widget-specific guides when introducing complex components.
- Consider adding unit/integration tests in the backend to exercise `UiReq_` logic for mission-critical widgets.
