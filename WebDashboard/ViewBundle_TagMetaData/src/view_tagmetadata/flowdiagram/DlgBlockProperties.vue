<template>
  <v-dialog
    v-model="show"
    scrollable
    max-width="560px"
    @keydown="onKeyDown"
  >
    <v-card>
      <v-card-title>
        <span class="headline">Block Properties of {{ blockName }}</span>
      </v-card-title>

      <v-card-text>
        <table v-if="copy !== null">
          <tbody>
            <tr>
              <td><div class="label-check">Draw name</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-checkbox
                  v-model="copy.drawName"
                  class="small-check"
                ></v-checkbox>
              </td>
            </tr>

            <tr>
              <td><div class="label-check">Flip name</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-checkbox
                  v-model="copy.flipName"
                  class="small-check"
                ></v-checkbox>
              </td>
            </tr>

            <tr>
              <td><div class="label-check">Draw port label</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-checkbox
                  v-model="copy.drawPortLabel"
                  class="small-check"
                ></v-checkbox>
              </td>
            </tr>

            <tr>
              <td><div class="label-text">Font</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <table>
                  <tbody>
                    <tr>
                      <td>
                        <v-text-field
                          v-model="copy.font!.family"
                          style="width: 12ex"
                          variant="solo"
                        ></v-text-field>
                      </td>
                      <td>
                        <v-text-field
                          v-model="copy.font!.size"
                          style="width: 6ex"
                          variant="solo"
                          :rules="[(v: string) => checkNumeric(v)]"
                        ></v-text-field>
                      </td>
                      <td>
                        <v-select
                          v-model="copy.font!.style"
                          style="width: 12ex"
                          :items="fontStyles"
                          variant="solo"
                        ></v-select>
                      </td>
                      <td>
                        <v-select
                          v-model="copy.font!.weight"
                          style="width: 12ex"
                          :items="fontWeights"
                          variant="solo"
                        ></v-select>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </td>
            </tr>

            <tr>
              <td><div class="label-text">Icon file name</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-text-field
                  v-model="copy.icon!.name"
                  variant="solo"
                ></v-text-field>
              </td>
            </tr>

            <tr>
              <td><div class="label-text">Icon pos and size</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <table>
                  <tbody>
                    <tr>
                      <td><div class="label-text">x:&nbsp;</div></td>
                      <td>
                        <v-text-field
                          v-model="copy.icon!.x"
                          style="width: 7ex"
                          variant="solo"
                        ></v-text-field>
                      </td>
                      <td><div class="label-text">&nbsp;&nbsp;y:&nbsp;</div></td>
                      <td>
                        <v-text-field
                          v-model="copy.icon!.y"
                          style="width: 7ex"
                          variant="solo"
                        ></v-text-field>
                      </td>
                      <td><div class="label-text">&nbsp;&nbsp;w:&nbsp;</div></td>
                      <td>
                        <v-text-field
                          v-model="copy.icon!.w"
                          style="width: 7ex"
                          variant="solo"
                        ></v-text-field>
                      </td>
                      <td><div class="label-text">&nbsp;&nbsp;h:&nbsp;</div></td>
                      <td>
                        <v-text-field
                          v-model="copy.icon!.h"
                          style="width: 7ex"
                          variant="solo"
                        ></v-text-field>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </td>
            </tr>

            <tr>
              <td><div class="label-text">Rotate Icon</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-checkbox
                  v-model="copy.icon!.rotate"
                  class="small-check"
                ></v-checkbox>
              </td>
            </tr>

            <tr>
              <td><div class="label-text">Colors</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <table>
                  <tbody>
                    <tr>
                      <td><div class="label-colorpick">Foreground:&nbsp;</div></td>
                      <td>
                        <ColorPicker
                          :modelValue="copy && copy.colorForeground !== undefined ? copy.colorForeground : ColorBlack"
                          @update:modelValue="
                            (val) => {
                              if (copy) copy.colorForeground = val
                            }
                          "
                          style="width: 12ex"
                        ></ColorPicker>
                      </td>
                      <td><div class="label-colorpick">&nbsp;&nbsp;Back:&nbsp;</div></td>
                      <td>
                        <ColorPicker
                          :modelValue="copy && copy.colorBackground !== undefined ? copy.colorBackground : ColorWhite"
                          @update:modelValue="
                            (val) => {
                              if (copy) copy.colorBackground = val
                            }
                          "
                          style="width: 12ex"
                        ></ColorPicker>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </td>
            </tr>

            <tr>
              <td><div class="label-check">Draw frame</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-checkbox
                  v-model="copy.drawFrame"
                  class="small-check"
                ></v-checkbox>
              </td>
            </tr>

            <tr>
              <td><div class="label-text">Frame shape</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-select
                  v-model="copy.frame!.shape"
                  :items="frameShapes"
                  variant="solo"
                ></v-select>
              </td>
            </tr>

            <tr>
              <td><div class="label-text">Frame line width</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-text-field
                  v-model="copy.frame!.strokeWidth"
                  variant="solo"
                  :rules="[(v: string) => checkNumeric(v)]"
                ></v-text-field>
              </td>
            </tr>

            <tr>
              <td><div class="label-text">Frame colors</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <table>
                  <tbody>
                    <tr>
                      <td><div class="label-colorpick">Line:&nbsp;</div></td>
                      <td>
                        <ColorPicker
                          v-model="copy!.frame!.strokeColor"
                          style="width: 13ex"
                        ></ColorPicker>
                      </td>
                      <td><div class="label-colorpick">&nbsp;&nbsp;Fill:&nbsp;</div></td>
                      <td>
                        <ColorPicker
                          v-model="copy!.frame!.fillColor"
                          style="width: 13ex"
                        ></ColorPicker>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </td>
            </tr>

            <tr v-if="shapeParam1 !== ''">
              <td>
                <div class="label-text">{{ shapeParam1 }}</div>
              </td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-text-field
                  v-model.number="copy.frame!.var1"
                  variant="solo"
                  type="number"
                  :rules="[(v: string) => checkNumeric(v)]"
                ></v-text-field>
              </td>
            </tr>

            <tr>
              <td><div class="label-check">Frame shadow</div></td>
              <td>&nbsp;&nbsp;</td>
              <td>
                <v-checkbox
                  v-model="copy.frame!.shadow"
                  class="small-check"
                ></v-checkbox>
              </td>
            </tr>
          </tbody>
        </table>
      </v-card-text>

      <v-card-actions>
        <v-spacer></v-spacer>
        <v-btn
          color="red-darken-1"
          variant="text"
          @click="close"
          >Cancel</v-btn
        >
        <v-btn
          color="blue-darken-1"
          variant="text"
          :disabled="!allValuesValid"
          @click="onOK"
          >OK</v-btn
        >
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import * as simu from './model_flow'
import ColorPicker from './ColorPicker.vue'

const ColorWhite = '#FFFFFF'
const ColorBlack = '#000000'

// Props
const props = defineProps<{
  show: boolean
  block: simu.Block | null
}>()

// Emits
const emit = defineEmits<{
  'update:show': [value: boolean]
  changed: [block: simu.Block]
}>()

// Utility functions
const defaultFrame = (): simu.Frame => {
  return {
    shape: 'Rectangle',
    strokeWidth: 1,
    strokeColor: ColorBlack,
    fillColor: ColorWhite,
    shadow: false,
    var1: 0,
    var2: 0,
  }
}

const defaultIcon = (): simu.Icon => {
  return {
    name: '',
    x: 0,
    y: 0,
    w: 1,
    h: 1,
    rotate: false,
  }
}

const defaultFont = (): simu.Font => {
  return {
    family: 'Arial',
    size: 10,
    style: 'Normal',
    weight: 'Normal',
  }
}

const deepEquals = (a: object, b: object): boolean => {
  return JSON.stringify(a) === JSON.stringify(b)
}

// Reactive state
const copy = ref<simu.Block | null>(props.block)
const frameShapes = ref<simu.Shape[]>(['Rectangle', 'RoundedRectangle', 'Circle'])
const fontStyles = ref<simu.FontStyle[]>(['Normal', 'Italic', 'Oblique'])
const fontWeights = ref<simu.FontWeight[]>(['Normal', 'Bold', 'Thin'])

// Computed
const show = computed({
  get: () => props.show,
  set: (value: boolean) => emit('update:show', value),
})

const blockName = computed((): string => {
  return props.block === null ? '???' : props.block.name
})

const shapeParam1 = computed((): string => {
  if (copy.value === null) {
    return ''
  }
  switch (copy.value.frame!.shape) {
    case 'Rectangle':
      return ''
    case 'RoundedRectangle':
      return 'Roundness (0-0.5)'
    case 'Circle':
      return ''
    default:
      return ''
  }
})

const allValuesValid = computed((): boolean => {
  if (copy.value === null) {
    return false
  }
  const b = copy.value
  return (
    checkNumeric(b.frame!.strokeWidth) === true &&
    (checkNumeric(b.frame!.var1) === true || b.frame!.shape !== 'RoundedRectangle') &&
    checkNumeric(b.font!.size) === true
  )
})

// Watchers
watch(
  () => props.block,
  (block: simu.Block | null) => {
    if (block === null) {
      copy.value = null
    } else {
      const b: simu.Block = JSON.parse(JSON.stringify(block))
      if (b.icon === undefined) {
        b.icon = defaultIcon()
      }
      if (b.frame === undefined) {
        b.frame = defaultFrame()
      }
      if (b.font === undefined) {
        b.font = defaultFont()
      }
      if (b.drawPortLabel === undefined) {
        b.drawPortLabel = b.type === 'Macro'
      }
      if (b.colorForeground === undefined) {
        b.colorForeground = ColorBlack
      }
      if (b.colorBackground === undefined) {
        b.colorBackground = ColorWhite
      }
      copy.value = b
    }
  },
)

// Methods
const close = () => {
  emit('update:show', false)
}

const onOK = () => {
  close()

  if (copy.value !== null) {
    const b: simu.Block = JSON.parse(JSON.stringify(copy.value))
    if (deepEquals(b.icon!, defaultIcon())) {
      b.icon = undefined
    }
    if (deepEquals(b.font!, defaultFont())) {
      b.font = undefined
    }
    if (!b.drawFrame && deepEquals(b.frame!, defaultFrame())) {
      b.frame = undefined
    }
    if (b.colorForeground === ColorBlack) {
      b.colorForeground = undefined
    }
    if (b.colorBackground === ColorWhite) {
      b.colorBackground = undefined
    }
    emit('changed', b)
  }
}

const checkNumeric = (value: any): boolean | string => {
  const num: number = Number(value)
  if (value === '' || isNaN(num)) {
    return 'Not numeric!'
  }
  return true
}

const onKeyDown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    close()
  }
}
</script>

<style>
.v-messages {
  min-height: 0px;
}

.v-text-field.v-text-field--solo .v-input__control {
  min-height: 0px;
  padding: 0;
}

.label-check {
  font-size: 16px;
  padding-bottom: 12px;
  margin-top: 0px;
}

.label-text {
  font-size: 16px;
  padding-bottom: 16px;
}

.label-colorpick {
  font-size: 16px;
  padding-bottom: 6px;
}

.small-check {
  font-size: 16px;
  padding-bottom: 0px;
  margin-top: 0px;
}
</style>
