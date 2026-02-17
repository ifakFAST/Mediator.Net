export interface PublishModel {
  ID: string
  Name: string
  MQTT: MqttConfig[]
  SQL: SQLConfig[]
  OPC_UA: OpcUaConfig[]
}

export interface MqttConfig {
  ID: string
  Name: string
  Endpoint: string
  ClientIDPrefix: string
  CertFileCA: string
  CertFileClient: string
  User: string
  Pass: string
  NoCertificateValidation: boolean
  IgnoreCertificateRevocationErrors: boolean
  IgnoreCertificateChainErrors: boolean
  AllowUntrustedCertificates: boolean
  MaxPayloadSize: number
  TopicRoot: string
  VarPublish: MqttVarPub
}

export interface MqttVarPub {
  Enabled: boolean
  Topic: string
  TopicRegistration: string
  PrintPayload: boolean
  RootObjects: string[]
  PubFormat: string
  PubFormatReg: string
  Mode: string
  TopicTemplate: string
  BufferIfOffline: boolean
  SimpleTagsOnly: boolean
  NumericTagsOnly: boolean
  SendTagsWithNull: boolean
  NaN_Handling: string
  TimeAsUnixMilliseconds: boolean
  QualityNumeric: boolean
  PublishInterval: string
  PublishOffset: string
  PublishMode: string
}

export interface SQLConfig {
  ID: string
  Name: string
  DatabaseType: string
  ConnectionString: string
  IgnoreCertificateRevocationErrors: boolean
  IgnoreCertificateChainErrors: boolean
  AllowUntrustedCertificates: boolean
  VarPublish: SQLVarPub
}

export interface SQLVarPub {
  Enabled: boolean
  QueryTagID2Identifier: string
  QueryRegisterTag: string
  QueryPublish: string
  RootObjects: string[]
  BufferIfOffline: boolean
  LogWrites: boolean
  SimpleTagsOnly: boolean
  NumericTagsOnly: boolean
  SendTagsWithNull: boolean
  NaN_Handling: string
  PublishInterval: string
  PublishOffset: string
  PublishMode: string
}

export interface OpcUaConfig {
  ID: string
  Name: string
  Host: string
  Port: number
  LogLevel: string
  AllowAnonym: boolean
  LoginUser: string
  LoginPass: string
  ServerCertificateFile: string
  VarPublish: OpcUaVarPub
}

export interface OpcUaVarPub {
  Enabled: boolean
  RootObjects: string[]
  LocalObjectIDsForVariables: boolean
  SimpleTagsOnly: boolean
  NumericTagsOnly: boolean
  SendTagsWithNull: boolean
  NaN_Handling: string
  PublishInterval: string
  PublishOffset: string
  PublishMode: string
}

export interface ModuleInfo {
  ID: string
  Name: string
}
