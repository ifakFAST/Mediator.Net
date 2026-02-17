export const publishModeTooltip = `Cyclic: 
  IF PublishInterval = 0 THEN: publish on variable value update with an additional 1 min cycle. 
  ELSE: Cyclic publish based on PublishInterval & PublishOffset

OnVarValueUpdate: Publish on variable value update (+ additional cycle publish if PublishInterval > 0).

OnVarHistoryUpdate: Publish on variable history update (+ additional cycle publish if PublishInterval > 0).`
