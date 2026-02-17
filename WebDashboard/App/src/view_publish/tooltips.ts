export const publishModeTooltip = `Cyclic: 
  IF PublishInterval = 0 THEN: publish on variable value update with an additional 1 min cycle. 
  ELSE: Cyclic publish based on PublishInterval & PublishOffset

OnVarValueUpdate: Publish on variable value update (+ additional cycle publish if PublishInterval > 0).

OnVarHistoryUpdate: Publish on variable history update (+ additional cycle publish if PublishInterval > 0).`

export const sqlQueryTagID2IdentifierTooltip = `Optional lookup query used to resolve @identifier for each variable.
Executed as scalar query (first value returned is used).

Use this if Query Publish references @identifier or @identifier_numeric.
Typical placeholders: @module, @id_string, @variable_name, @identifier.`

export const sqlQueryRegisterTagTooltip = `Optional one-time registration query per variable before normal publishing.
Useful for upsert/insert into metadata tables.

Can use the same placeholders as publish query, including @identifier if available.
Executed only once per variable (until config changes/restart).`

export const sqlQueryPublishTooltip = `Main query executed for each value to publish.
Use dynamic parameters as @name (safe DB parameters), e.g. @value_numeric, @value_json, @time_utc, @quality_numeric.

Use @@name only for literal text replacement in the SQL string.
If @identifier or @identifier_numeric is used, unresolved identifiers will skip publishing for that variable.`

export const sqlConnectionStringTooltip = `Database connection string used by SQL publishing.
For PostgreSQL (Npgsql), include at least host, database, username, and password.

Example:
Host=localhost;Port=5432;Database=mydb;Username=myuser;Password=mypass;`

export const opcUaServerCertificateFileTooltip = `Optional path to the OPC UA server certificate file.
This file is used as the server identity presented to OPC UA clients.

Use a stable certificate in production so clients can trust and persist it.`

export const opcUaLocalObjectIDsForVariablesTooltip = `Controls how OPC UA node IDs are built for published variables.
Enabled: uses local object IDs (shorter, local naming).
Disabled: uses full encoded object IDs (globally unique, more stable across modules).`

export const opcUaHostTooltip = `Host/interface for the OPC UA server endpoint.
Use localhost (or 127.0.0.1) to accept only local connections.
Leave empty or use 0.0.0.0 to listen externally on all interfaces.`

export const mqttVarTopicTooltip = `Base MQTT topic for variable value messages.
Final topic is TopicRoot + "/" + Topic (if TopicRoot is set).

Used in Bulk mode.
In TopicPerVariable mode, TopicTemplate is used instead.`

export const mqttVarTopicRegistrationTooltip = `Optional MQTT topic for registration messages (first publish per variable).
Leave empty to disable registration publishing.

Final topic is TopicRoot + "/" + TopicRegistration (if TopicRoot is set).`

export const mqttVarTopicTemplateTooltip = `Template for per-variable MQTT topics (used only in TopicPerVariable mode).
Use {ID} as placeholder for the variable ID.

Example:
devices/{ID}

Final topic is TopicRoot + "/" + TopicTemplate, with {ID} URL-escaped for MQTT topic safety.`

export const mqttBulkPubFormatTooltip = `Payload format for Bulk mode variable messages.
Array: publishes a JSON array of tag objects.
Object: publishes an object with wrapper fields:
  { "now": "<timestamp>", "tags": [ ... ] }`

export const mqttTopicRootTooltip = `Optional prefix added to all MQTT publish topics.
If set, final topics become TopicRoot + "/" + <topic>.

Example:
TopicRoot = plant1/lineA
Topic = telemetry
Final topic = plant1/lineA/telemetry`

export const mqttEndpointTooltip = `MQTT broker endpoint as host[:port] or URI.
Examples:
broker.local
broker.local:1883
mqtt://broker.local:1883
mqtts://broker.local

TLS behavior is controlled by Use TLS.`

export const mqttUseTLSTooltip = `Controls whether TLS is used for the MQTT connection.
Auto: use TLS if CertFileCA is set OR endpoint port is not 1883
Always: always use TLS.
Never: never use TLS (certificate settings are ignored).`
