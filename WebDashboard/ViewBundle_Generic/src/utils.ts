

export function findUniqueID(baseStr: string, existingIDs: Set<string>): string {
  const idLen = 6
  console.info(existingIDs)
  let id = baseStr + '_' + generateId(idLen)
  while (existingIDs.has(id)) {
    id = baseStr + '_' + generateId(idLen)
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
