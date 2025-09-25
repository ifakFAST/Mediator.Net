import { drawInteractiveElement } from './util.js'

const TagTrashBlock = {
  id: 'tag_trash',
  parameters: [],
  defineIOs() { return [] },
  customDraw(ctx) { drawInteractiveElement(ctx) },
  supportedDropTypes: ['data-tag'],
}

export default TagTrashBlock

