

export function findUniqueID(baseStr: string, numLen: number, existingIDs: Set<string>): string {
  console.info(existingIDs)
  let id = baseStr + '_' + generateId(numLen)
  while (existingIDs.has(id)) {
    id = baseStr + '_' + generateId(numLen)
  }
  return id
}

function generateId(len: number): string {
  const dec2hex = (dec: number) => {
    return ('0' + dec.toString(16)).substr(-2)
  }
  const arr = new Uint8Array((len || 8) / 2)
  window.crypto.getRandomValues(arr)
  return Array.from(arr, dec2hex).join('')
}

export interface TimeRange {
  type: TimeType,
  lastCount: number,
  lastUnit: TimeUnit,
  rangeStart: string,
  rangeEnd: string,
}

export type TimeType = 'Last' | 'Range'

export type TimeUnit = 'Minutes' | 'Hours' | 'Days' | 'Weeks' | 'Months' | 'Years'
export const TimeUnitValues: TimeUnit[] = ['Minutes', 'Hours', 'Days', 'Weeks', 'Months', 'Years']

export function getMillisecondsFromTimeRange(range: TimeRange): number {
  const n = range.lastCount
  const Minute = 60 * 1000
  const Hour = 60 * Minute
  const Day = 24 * Hour
  const Week = 7 * Day
  const Month = 30 * Day
  const Year = 365 * Day
  switch (range.lastUnit) {
    case 'Minutes': return n * Minute
    case 'Hours':   return n * Hour
    case 'Days':    return n * Day
    case 'Weeks':   return n * Week
    case 'Months':  return n * Month
    case 'Years':   return n * Year
  }
  return Hour
}

export function getLocalDateIsoStringFromTimestamp(timestamp: number): string {
  const date = new Date(timestamp)
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0'); // Months are zero-based
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};

export function timeWindowFromTimeRange(range: TimeRange): { left: number, right: number } {

  const Now = Math.trunc(new Date().getTime() / 1000) * 1000 + 1000
  const Day = 24 * 60 * 60 * 1000

  if (range.type === 'Last') {

    return {
      left:  Now - getMillisecondsFromTimeRange(range),
      right: Now,
    }

  }
  else if (range.type === 'Range') {

    return {
      left:  parseDateISOString(range.rangeStart).getTime(),
      right: parseDateISOString(range.rangeEnd).getTime(),
    }

  }
  else {

    return { left: Now - Day, right: Now }

  }
}

export function parseDateISOString(str: string): Date {
  str = str.trim().toUpperCase()
  if (str.endsWith('Z')) { return new Date(str) }
  const ds = str.split(/\D+/).map((s) => parseInt(s, 10))
  switch (ds.length) {
    case 3: return new Date(ds[0], ds[1] - 1, ds[2]) // e.g. 2021-01-01
    case 4: return new Date(ds[0], ds[1] - 1, ds[2], ds[3]) // e.g. 2021-01-01T00
    case 5: return new Date(ds[0], ds[1] - 1, ds[2], ds[3], ds[4]) // e.g. 2021-01-01T00:00
    case 6: return new Date(ds[0], ds[1] - 1, ds[2], ds[3], ds[4], ds[5]) // e.g. 2021-01-01T00:00:00
    case 7: return new Date(ds[0], ds[1] - 1, ds[2], ds[3], ds[4], ds[5], ds[6]) // e.g. 2021-01-01T00:00:00.000
    default: throw new Error('Invalid ISO date string: ' + str)
  }
}
