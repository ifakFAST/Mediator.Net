from Ifak.Fast.Mediator.Calc.Adapter_Python import PyInputBase, PyOutputBase, PyStateBase
from Ifak.Fast.Mediator import Quality, Duration, Timestamp, QualityFilter
from Ifak.Fast.Mediator.Calc import Aggregation
import Ifak.Fast.Mediator
import json
from System.Collections.Generic import List
from typing import Optional
from datetime import datetime, timezone, timedelta


def _wrapStepCall(step, _t, _dt):
    t = datetime.fromtimestamp(_t, timezone.utc)
    dt = timedelta(seconds=_dt)
    step(t, dt)

def _datetime2str(dt: datetime) -> str:
    return datetime.strftime(dt, "%Y-%m-%dT%H:%M:%S.%fZ")

def _optionalDatetime2DataValue(value: Optional[datetime]) -> Ifak.Fast.Mediator.DataValue:
    if value is not None:
        return Ifak.Fast.Mediator.DataValue.FromString(_datetime2str(value))
    return Ifak.Fast.Mediator.DataValue.Empty

def _DataValue2OptionalDatetime(value: Ifak.Fast.Mediator.DataValue) -> Optional[datetime]:
    ts = value.GetTimestampOrNull()
    if ts is None:
        return None
    timestamp = ts.JavaTicks / 1000.0
    return datetime.fromtimestamp(timestamp, timezone.utc)

def _convertDotNetListOfList(dotnet_list: List) -> list[list]:
    """Convert .NET List<List<T>> to Python list[list[T]]"""
    python_result = []
    for dotnet_sub_list in dotnet_list:
        python_list = []
        for x in dotnet_sub_list:
            python_list.append(x)
        python_result.append(python_list)
    return python_result

class TimeseriesEntry:
    
    def __init__(self, time: datetime, value):
        self.Time = time
        self.Value = value

    def __eq__(self, other):
        if isinstance(other, TimeseriesEntry):
            return self.Time == other.Time and self.Value == other.Value
        return False

    def __hash__(self):
        return hash((self.Time, self.Value))

    def __str__(self):
        return f"{self.Time}={self.Value}"
    
    def to_dict(self):
        return {
            "Time": _datetime2str(self.Time),
            "Value": self.Value
        }
  
def _verifyOptionalDict(name: str, value: Optional[dict]) -> None:
    if value is not None and not isinstance(value, dict):
        raise Exception(f"{name} must be a dict or None but is {type(value).__name__}")

def _verifyOptionalFloat(name: str, value: Optional[float]) -> Optional[float]:
    if value is not None and not isinstance(value, float) and not isinstance(value, int):
        raise Exception(f"{name} must be a float or None but is {type(value).__name__}")
    if isinstance(value, int):
        return float(value)
    return value

def _verifyOptionalInt(name: str, value: Optional[int]) -> Optional[int]:
    if value is not None and not isinstance(value, float) and not isinstance(value, int):
        raise Exception(f"{name} must be a int or None but is {type(value).__name__}")
    if isinstance(value, float):
        return int(value)
    return value

def _verifyString(name: str, value: str) -> None:
    if not isinstance(value, str):
        raise Exception(f"{name} must be a str but is {type(value).__name__}")

def _verifyOptionalString(name: str, value: Optional[str]) -> None:
    if value is not None and not isinstance(value, str):
        raise Exception(f"{name} must be a str or None but is {type(value).__name__}")
    
def _verifyOptionalDatetime(name: str, value: Optional[datetime]) -> None:
    if value is not None and not isinstance(value, datetime):
        raise Exception(f"{name} must be a datetime or None but is {type(value).__name__}")    
    
def _verifyOptionalFloatList(name: str, value: Optional[list[float]]) -> None:
    if value is not None and not isinstance(value, list):
        raise Exception(f"{name} must be a list of float or None but is {type(value).__name__}")
    if value is not None:
        for i in range(len(value)):
            if not isinstance(value[i], float) and not isinstance(value[i], int):
                raise Exception(f"{name}[{i}] must be a float or int but is {type(value[i]).__name__}")

def _verifyOptionalListOfDict(name: str, value: Optional[list[dict]]) -> None:
    if value is not None and not isinstance(value, list):
        raise Exception(f"{name} must be a list of dict or None but is {type(value).__name__}")
    if value is not None:
        for i in range(len(value)):
            if not isinstance(value[i], dict):
                raise Exception(f"{name}[{i}] must be a dict but is {type(value[i]).__name__}")

def _verifyOptionalListOfTimeseriesEntry(name: str, value: Optional[list[TimeseriesEntry]]) -> None:
    if value is not None and not isinstance(value, list):
        raise Exception(f"{name} must be a list of TimeseriesEntry or None but is {type(value).__name__}")
    if value is not None:
        for i in range(len(value)):
            if not isinstance(value[i], TimeseriesEntry):
                raise Exception(f"{name}[{i}] must be a TimeseriesEntry but is {type(value[i]).__name__}")
    
########### Inputs #############

class MyInputBase(PyInputBase):

    @property
    def HasValidValue(self) -> bool:
        try:
            return self.Value != None
        except:
            return False

    @property
    def Time(self) -> datetime:
        return datetime.fromtimestamp(self.GetTimestamp(), timezone.utc)


class InputFloat64(MyInputBase):

    def __init__(self, name: str, unit: str = "", defaultValue: Optional[float] = 0.0) -> None:
        defaultValue = _verifyOptionalFloat(f"Input {name}: defaultValue", defaultValue)
        super().__init__(name, unit, Ifak.Fast.Mediator.DataType.Float64, 1, Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(defaultValue)))
        self._defaultValue: Optional[float] = defaultValue

    @property
    def DefaultValue(self) -> Optional[float]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[float]:
        return self.VTQ.V.AsDouble()

    def ValueOrElse(self, default: float) -> float:
        if self.HasValidValue:
            return self.Value
        return default


Input = InputFloat64


class InputFloat64Array(MyInputBase):

    def __init__(self, name: str, defaultValue: Optional[list[float]]) -> None:
        _verifyOptionalFloatList(f"Input {name}: defaultValue", defaultValue)
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Float64, 0, Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(defaultValue)))
        self._defaultValue: Optional[list[float]] = defaultValue

    @property
    def DefaultValue(self) -> Optional[list[float]]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[list[float]]:
        value = json.loads(self.VTQ.V.JSON)
        _verifyOptionalFloatList(f"Input {self.ID}: Value", value)
        return value

    def ValueOrElse(self, default: list[float]) -> list[float]:
        if self.HasValidValue:
            return self.Value
        return default


class InputString(MyInputBase):

    def __init__(self, name: str, defaultValue: Optional[str]) -> None:
        _verifyOptionalString(f"Input {name}: defaultValue", defaultValue)
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.String, 1, Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(defaultValue)))
        self._defaultValue: Optional[str] = defaultValue

    @property
    def DefaultValue(self) -> Optional[str]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[str]:
        return self.VTQ.V.GetString()

    def ValueOrElse(self, default: str) -> str:
        if self.HasValidValue:
            return self.Value
        return default


class InputJson(MyInputBase):

    def __init__(self, name: str, defaultValue: str) -> None:
        _verifyString(f"Input {name}: defaultValue", defaultValue)
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.JSON, 1, Ifak.Fast.Mediator.DataValue.FromJSON(defaultValue))
        self._defaultValue: str = defaultValue

    @property
    def DefaultValue(self) -> str:
        return self._defaultValue

    @property
    def Value(self) -> str:
        return self.VTQ.V.JSON


class InputTimestamp(MyInputBase):

    def __init__(self, name: str, defaultValue: Optional[datetime]) -> None:
        _verifyOptionalDatetime(f"Input {name}: defaultValue", defaultValue)
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Timestamp, 1, _optionalDatetime2DataValue(defaultValue))
        self._defaultValue: Optional[datetime] = defaultValue

    @property
    def DefaultValue(self) -> Optional[datetime]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[datetime]:
        return _DataValue2OptionalDatetime(self.VTQ.V)
    
    def ValueOrElse(self, default: datetime) -> datetime:
        if self.HasValidValue:
            return self.Value
        return default


class InputObject(MyInputBase):

    def __init__(self, name: str, defaultValue: Optional[dict]) -> None:
        _verifyOptionalDict(f"Input {name}: defaultValue", defaultValue)
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Struct, 1, Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(defaultValue)))
        self._defaultValue: Optional[dict] = defaultValue

    @property
    def DefaultValue(self) -> Optional[dict]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[dict]:
        return json.loads(self.VTQ.V.JSON)


class InputObjectArray(MyInputBase):

    def __init__(self, name: str, defaultValue: Optional[list[dict]]) -> None:
        _verifyOptionalListOfDict(f"Input {name}: defaultValue", defaultValue)
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Struct, 0, Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(defaultValue)))
        self._defaultValue: Optional[list[dict]] = defaultValue

    @property
    def DefaultValue(self) -> Optional[list[dict]]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[list[dict]]:
        return json.loads(self.VTQ.V.JSON)

   
class InputTimeseries(MyInputBase):

    def __init__(self, name: str, defaultValue: Optional[list[TimeseriesEntry]] = None) -> None:
        _verifyOptionalListOfTimeseriesEntry(f"Input {name}: defaultValue", defaultValue)
        defaultValueForJSON = defaultValue
        if defaultValue is not None:
            defaultValueForJSON = [entry.to_dict() for entry in defaultValue]
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Timeseries, 1, Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(defaultValueForJSON)))
        self._defaultValue: Optional[list[TimeseriesEntry]] = defaultValue

    @property
    def DefaultValue(self) -> Optional[list[TimeseriesEntry]]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[list[TimeseriesEntry]]:
        timeseries = json.loads(self.VTQ.V.JSON)
        _verifyOptionalListOfDict(f"Input {self.ID}: Value", timeseries)
        if timeseries is None:
            return None
        result = []
        for i in range(len(timeseries)):
            entry = timeseries[i]
            if "Time" not in entry:
                raise Exception(f"Input {self.ID}: Value[{i}]: missing 'Time' field")
            if "Value" not in entry:
                raise Exception(f"Input {self.ID}: Value[{i}]: missing 'Value' field")            
            timestamp = Ifak.Fast.Mediator.Timestamp.FromISO8601(entry["Time"]).JavaTicks / 1000.0
            entry["Time"] = datetime.fromtimestamp(timestamp, timezone.utc)
            result.append(TimeseriesEntry(entry["Time"], entry["Value"]))
        return result
    
    
########### States #############


class StateFloat64(PyStateBase):

    def __init__(self, name: str, unit: str = "", defaultValue: Optional[float] = 0.0) -> None:
        defaultValue = _verifyOptionalFloat(f"State {name}: defaultValue", defaultValue)
        super().__init__(name, unit, Ifak.Fast.Mediator.DataType.Float64, 1, Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(defaultValue)))
        self._defaultValue: Optional[float] = defaultValue

    @property
    def DefaultValue(self) -> Optional[float]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[float]:
        return json.loads(self.theValue.JSON)

    @Value.setter
    def Value(self, value: Optional[float]) -> None:
        value = _verifyOptionalFloat(f"State {self.ID}: value", value)
        if value is not None:
            self.theValue = Ifak.Fast.Mediator.DataValue.FromDouble(value)
        else:
            self.theValue = Ifak.Fast.Mediator.DataValue.Empty

State = StateFloat64


class StateFloat64Array(PyStateBase):
    
    def __init__(self, name: str, defaultValue: Optional[list[float]], dimension: int = 0) -> None:
        _verifyOptionalFloatList(f"State {name}: defaultValue", defaultValue)
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Float64, dimension, Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(defaultValue)))
        if dimension < 0:
            raise Exception(f"State {self.ID}: dimension must be >= 0 but is {dimension}")
        self._defaultValue: Optional[list[float]] = defaultValue

    @property
    def DefaultValue(self) -> Optional[list[float]]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[list[float]]:
        return json.loads(self.theValue.JSON)

    @Value.setter
    def Value(self, value: Optional[list[float]]) -> None:
        _verifyOptionalFloatList(f"State {self.ID}: Value", value)
        self.theValue = Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(value))


class StateString(PyStateBase):

    def __init__(self, name: str, defaultValue: Optional[str]) -> None:
        _verifyOptionalString(f"State {name}: defaultValue", defaultValue)
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.String, 1, Ifak.Fast.Mediator.DataValue.FromString(defaultValue))
        self._defaultValue: Optional[str] = defaultValue

    @property
    def DefaultValue(self) -> Optional[str]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[str]:
        return json.loads(self.theValue.JSON)

    @Value.setter
    def Value(self, value: Optional[str]) -> None:
        _verifyOptionalString(f"State {self.ID}: value", value)
        self.theValue = Ifak.Fast.Mediator.DataValue.FromString(value)        


class StateTimestamp(PyStateBase):

    def __init__(self, name: str, defaultValue: Optional[datetime]) -> None:
        _verifyOptionalDatetime(f"State {name}: defaultValue", defaultValue)
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Timestamp, 1, _optionalDatetime2DataValue(defaultValue))
        self._defaultValue: Optional[datetime] = defaultValue

    @property
    def DefaultValue(self) -> Optional[datetime]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[datetime]:
        return _DataValue2OptionalDatetime(self.theValue)

    @Value.setter
    def Value(self, value: Optional[datetime]) -> None:
        _verifyOptionalDatetime(f"State {self.ID}: value", value)
        self.theValue = _optionalDatetime2DataValue(value)


class StateObject(PyStateBase):

    def __init__(self, name: str, defaultValue: Optional[dict]) -> None:
        _verifyOptionalDict(f"State {name}: defaultValue", defaultValue)
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Struct, 1, Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(defaultValue)))
        self._defaultValue: Optional[dict] = defaultValue

    @property
    def DefaultValue(self) -> Optional[dict]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[dict]:
        return json.loads(self.theValue.JSON)

    @Value.setter
    def Value(self, value: Optional[dict]) -> None:
        _verifyOptionalDict(f"State {self.ID}: Value", value)
        self.theValue = Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(value))


class StateObjectArray(PyStateBase):

    def __init__(self, name: str, defaultValue: Optional[list[dict]]) -> None:
        _verifyOptionalListOfDict(f"State {name}: defaultValue", defaultValue)
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Struct, 0, Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(defaultValue)))
        self._defaultValue: Optional[list[dict]] = defaultValue

    @property
    def DefaultValue(self) -> Optional[list[dict]]:
        return self._defaultValue

    @property
    def Value(self) -> Optional[list[dict]]:
        return json.loads(self.theValue.JSON)

    @Value.setter
    def Value(self, value: Optional[list[dict]]) -> None:
        _verifyOptionalListOfDict(f"State {self.ID}: Value", value)
        self.theValue = Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(value))



########### Outputs #############


class MyOutputBase(PyOutputBase):

    @property
    def Time(self) -> datetime:
        raise Exception(f"Output {self.ID}: Time is not readable")

    @Time.setter
    def Time(self, value: datetime) -> None:
        if not isinstance(value, datetime):
            typeName = type(value).__name__
            raise Exception(f"Output {self.ID}: Time must be a datetime but is {typeName}")
        self.SetTime(value.timestamp())


class OutputFloat64(MyOutputBase):

    def __init__(self, name: str, unit: str = "", roundDigits: Optional[int] = 6) -> None:
        super().__init__(name, unit, Ifak.Fast.Mediator.DataType.Float64, 1)
        self._roundingDigits: Optional[int] = roundDigits

    @property
    def Value(self) -> Optional[float]:
        raise Exception(f"Output {self.ID}: Value is not readable")

    @Value.setter
    def Value(self, value: Optional[float]) -> None:
        value = _verifyOptionalFloat(f"Output {self.ID}: Value", value)
        if value is not None:
            if self._roundingDigits is not None:
                try:
                    value = round(value, self._roundingDigits)
                except:
                    pass
            newValue = Ifak.Fast.Mediator.DataValue.FromDouble(value)
        else:
            newValue = Ifak.Fast.Mediator.DataValue.Empty
        self.SetValue(newValue)

Output = OutputFloat64


class OutputInt32(MyOutputBase):

    def __init__(self, name: str, unit: str = "") -> None:
        super().__init__(name, unit, Ifak.Fast.Mediator.DataType.Int32, 1)

    @property
    def Value(self) -> Optional[int]:
        raise Exception(f"Output {self.ID}: Value is not readable")

    @Value.setter
    def Value(self, value: Optional[int]) -> None:
        value = _verifyOptionalInt(f"Output {self.ID}: Value", value)
        if value is not None:
            newValue = Ifak.Fast.Mediator.DataValue.FromInt(value)
        else:
            newValue = Ifak.Fast.Mediator.DataValue.Empty
        self.SetValue(newValue)


class OutputFloat64Array(MyOutputBase):

    def __init__(self, name: str, dimension: int = 0) -> None:
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Float64, 0)
        if dimension < 0:
            raise Exception(f"Output {self.ID}: dimension must be >= 0 but is {dimension}")

    @property
    def Value(self) -> list[float]:
        raise Exception(f"Output {self.ID}: Value is not readable")

    @Value.setter
    def Value(self, value: Optional[list[float]]) -> None:
        _verifyOptionalFloatList(f"Output {self.ID}: Value", value)
        newValue = Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(value))
        self.SetValue(newValue)


class OutputString(MyOutputBase):

    def __init__(self, name: str) -> None:
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.String, 1)

    @property
    def Value(self) -> Optional[str]:
        raise Exception(f"Output {self.ID}: Value is not readable")

    @Value.setter
    def Value(self, value: Optional[str]) -> None:
        _verifyOptionalString(f"Output {self.ID}: Value", value)
        newValue = Ifak.Fast.Mediator.DataValue.FromString(value)
        self.SetValue(newValue)


class OutputTimestamp(MyOutputBase):

    def __init__(self, name: str) -> None:
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Timestamp, 1)

    @property
    def Value(self) -> Optional[datetime]:
        raise Exception(f"Output {self.ID}: Value is not readable")

    @Value.setter
    def Value(self, value: Optional[datetime]) -> None:
        _verifyOptionalDatetime(f"Output {self.ID}: Value", value)
        self.SetValue(_optionalDatetime2DataValue(value))


class OutputTimeseries(MyOutputBase):

    def __init__(self, name: str) -> None:
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Timeseries, 1)

    @property
    def Value(self) -> Optional[datetime]:
        raise Exception(f"Output {self.ID}: Value is not readable")

    @Value.setter
    def Value(self, value: Optional[list[TimeseriesEntry]]) -> None:
        _verifyOptionalListOfTimeseriesEntry(f"Output {self.ID}: Value", value)
        newValue = Ifak.Fast.Mediator.DataValue.Empty
        if value is not None:
            valueForJSON = [entry.to_dict() for entry in value]
            newValue = Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(valueForJSON))
        self.SetValue(newValue)


class OutputObject(MyOutputBase):

    def __init__(self, name: str) -> None:
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Struct, 1)

    @property
    def Value(self) -> Optional[dict]:
        raise Exception(f"Output {self.ID}: Value is not readable")

    @Value.setter
    def Value(self, value: Optional[dict]) -> None:
        _verifyOptionalDict(f"Output {self.ID}: Value", value)
        newValue = Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(value))
        self.SetValue(newValue)


class OutputObjectArray(MyOutputBase):

    def __init__(self, name: str) -> None:
        super().__init__(name, "", Ifak.Fast.Mediator.DataType.Struct, 0)

    @property
    def Value(self) -> list[dict]:
        raise Exception(f"Output {self.ID}: Value is not readable")

    @Value.setter
    def Value(self, value: Optional[list[dict]]) -> None:
        _verifyOptionalListOfDict(f"Output {self.ID}: Value", value)
        newValue = Ifak.Fast.Mediator.DataValue.FromJSON(json.dumps(value))
        self.SetValue(newValue)

class Api(Ifak.Fast.Mediator.Calc.Adapter_Python.PyApi):

    def ReadVariablesHistory(self, variables: list[Ifak.Fast.Mediator.VariableRef], startTime: Timestamp, endTime: Timestamp, emptyResultOnError: bool = True, filter: QualityFilter = QualityFilter.ExcludeNone) -> list[list[Ifak.Fast.Mediator.VTQ]]:
        dotnet_variables = List[Ifak.Fast.Mediator.VariableRef]()
        for var in variables:
            dotnet_variables.Add(var)
        result = super().ReadVariablesHistory(dotnet_variables, startTime, endTime, emptyResultOnError, filter)
        return _convertDotNetListOfList(result)

    def ReadVariablesHistoryLastN(self, inputs: list[Ifak.Fast.Mediator.VariableRef], n: int, emptyResultOnError: bool = True) -> list[list[Ifak.Fast.Mediator.VTQ]]:
        dotnet_inputs = List[Ifak.Fast.Mediator.VariableRef]()
        for obj in inputs:
            dotnet_inputs.Add(obj)
        result = super().ReadVariablesHistoryLastN(dotnet_inputs, n, emptyResultOnError)
        return _convertDotNetListOfList(result)

    @classmethod
    def MakeVariableRefs(cls, inputs: list[PyInputBase]) -> list[Ifak.Fast.Mediator.VariableRef]:
        dotnet_inputs = List[PyInputBase]()
        for obj in inputs:
            dotnet_inputs.Add(obj)
        result = Ifak.Fast.Mediator.Calc.Adapter_Python.PyApi.MakeVariableRefs(dotnet_inputs)
        return [var_ref for var_ref in result]

    @classmethod
    def MakeVariableRefsFromObjectIDs(cls, objectIDs: list[str]) -> list[Ifak.Fast.Mediator.VariableRef]:
        dotnet_strings = List[str]()
        for obj_id in objectIDs:
            dotnet_strings.Add(obj_id)
        result = Ifak.Fast.Mediator.Calc.Adapter_Python.PyApi.MakeVariableRefsFromObjectIDs(dotnet_strings)
        return [var_ref for var_ref in result]

    def GetVariableRefsBelow(self, objectIDs: list[str], types: list[str], varNames: list[str]) -> list[Ifak.Fast.Mediator.VariableRef]:
        dotnet_objectIDs = List[str]()
        for obj_id in objectIDs:
            dotnet_objectIDs.Add(obj_id)
        dotnet_types = List[str]()
        for t in types:
            dotnet_types.Add(t)
        dotnet_varNames = List[str]()
        for var_name in varNames:
            dotnet_varNames.Add(var_name)
        result = super().GetVariableRefsBelow(dotnet_objectIDs, dotnet_types, dotnet_varNames)
        return [var_ref for var_ref in result]


class AggregationUtils(Ifak.Fast.Mediator.Calc.AggregationUtils):
    
    @classmethod
    def Aggregate(cls, listHistories: list[list[Ifak.Fast.Mediator.VTQ]], aggregation: Aggregation, resolution: Duration, skipEmptyIntervals: bool) -> list[list[Ifak.Fast.Mediator.VTQ]]:
        dotnet_listHistories = List[List[Ifak.Fast.Mediator.VTQ]]()
        for history in listHistories:
            dotnet_history = List[Ifak.Fast.Mediator.VTQ]()
            for vtq in history:
                dotnet_history.Add(vtq)
            dotnet_listHistories.Add(dotnet_history)
        result = Ifak.Fast.Mediator.Calc.AggregationUtils.Aggregate(dotnet_listHistories, aggregation, resolution, skipEmptyIntervals)
        return _convertDotNetListOfList(result)

    @classmethod
    def ExportToMatrix(cls, listHistories: list[list[Ifak.Fast.Mediator.VTQ]]) -> Ifak.Fast.Mediator.Calc.TimeAlignedMatrix:
        dotnet_listHistories = List[List[Ifak.Fast.Mediator.VTQ]]()
        for history in listHistories:
            dotnet_history = List[Ifak.Fast.Mediator.VTQ]()
            for vtq in history:
                dotnet_history.Add(vtq)
            dotnet_listHistories.Add(dotnet_history)
        result = Ifak.Fast.Mediator.Calc.AggregationUtils.ExportToMatrix(dotnet_listHistories)
        return result