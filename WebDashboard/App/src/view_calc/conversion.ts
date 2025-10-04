import * as calcmodel from './model'
import * as fast from '../fast_types'

export interface TreeItem {
  id: string
  title: string
  parentID: string | null
  first: boolean
  last: boolean
  children: TreeItem[]
  object: calcmodel.Folder | calcmodel.Signal | calcmodel.Calculation
  objectVariables: SignalVariables | CalculationVariables | null
  objectType: ObjType
}

export type ObjType = 'Folder' | 'Signal' | 'Calculation'

export interface SignalVariables {
  Value: fast.VTQ
}

export interface CalculationVariables {
  Inputs: IoVar[]
  Outputs: IoVar[]
  States: IoVar[]
}

export interface IoVar {
  Key: string
  Value: fast.VTQ
}

export function model2TreeItems(model: calcmodel.CalcModel, map: Map<string, fast.VTQ>): TreeItem {
  return folder2TreeItem(model.RootFolder, null, true, true, map)
}

function folder2TreeItem(folder: calcmodel.Folder, parentID: string | null, isFirst: boolean, isLast: boolean, map: Map<string, fast.VTQ>): TreeItem {
  const mkCalc = (calculation: calcmodel.Calculation, first: boolean, last: boolean) => {
    const inputs: IoVar[] = calculation.Inputs.map((input) => {
      const valueVTQ: fast.VTQ = { V: '', T: '', Q: 'Bad' }
      map.set(calculation.ID + '.In.' + input.ID, valueVTQ)
      return { Key: input.ID, Value: valueVTQ }
    })
    const outputs: IoVar[] = calculation.Outputs.map((output) => {
      const valueVTQ: fast.VTQ = { V: '', T: '', Q: 'Bad' }
      map.set(calculation.ID + '.Out.' + output.ID, valueVTQ)
      return { Key: output.ID, Value: valueVTQ }
    })
    const states: IoVar[] = calculation.States.map((state) => {
      const valueVTQ: fast.VTQ = { V: '', T: '', Q: 'Bad' }
      map.set(calculation.ID + '.State.' + state.ID, valueVTQ)
      return { Key: state.ID, Value: valueVTQ }
    })
    const calcItem: TreeItem = {
      id: calculation.ID,
      title: calculation.Name,
      parentID: folder.ID,
      first,
      last,
      children: [],
      object: calculation,
      objectVariables: { Inputs: inputs, Outputs: outputs, States: states },
      objectType: 'Calculation',
    }
    return calcItem
  }

  const mkSignal = (signal: calcmodel.Signal, first: boolean, last: boolean) => {
    const valueVTQ: fast.VTQ = { V: '', T: '', Q: 'Bad' }
    map.set(signal.ID, valueVTQ)
    const signalItem: TreeItem = {
      id: signal.ID,
      title: signal.Name,
      parentID: folder.ID,
      first,
      last,
      children: [],
      object: signal,
      objectVariables: { Value: valueVTQ },
      objectType: 'Signal',
    }
    return signalItem
  }

  const subFolders = folder.Folders.map((f, idx) => folder2TreeItem(f, folder.ID, idx === 0, idx === folder.Folders.length - 1, map))
  const calculations = folder.Calculations.map((c, idx) => mkCalc(c, idx === 0, idx === folder.Calculations.length - 1))
  const signals = folder.Signals.map((s, idx) => mkSignal(s, idx === 0, idx === folder.Signals.length - 1))

  const folderNoChildren: calcmodel.Folder = { ...folder }
  folderNoChildren.Folders = []
  folderNoChildren.Signals = []
  folderNoChildren.Calculations = []

  const item: TreeItem = {
    id: folder.ID,
    title: folder.Name,
    parentID,
    first: isFirst,
    last: isLast,
    children: subFolders.concat(calculations).concat(signals),
    object: folderNoChildren,
    objectVariables: null,
    objectType: 'Folder',
  }
  return item
}
