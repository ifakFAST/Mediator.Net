<template>
  <div
    class="fast-struct-table"
    style="overflow-x: auto"
  >
    <v-table v-if="isNonEmptyObjectArray && vertical">
      <thead>
        <tr>
          <th
            v-for="member in objectMembers"
            :key="`member-${member}`"
            style="height: 34px; padding-top: 0px !important; padding-bottom: 0px !important"
          >
            {{ member }}
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="(obj, objIdx) in valueAsJson"
          :key="`obj-${objIdx}`"
        >
          <td
            v-for="(member, memberIdx) in objectMembers"
            :key="`td-${objIdx}-${memberIdx}`"
            style="padding-top: 0px !important; padding-bottom: 0px !important"
          >
            {{ objMember(obj, member) }}
          </td>
        </tr>
      </tbody>
    </v-table>
    <v-table v-if="isNonEmptyObjectArray && !vertical">
      <tbody>
        <tr
          v-for="member in objectMembers"
          :key="member"
        >
          <td class="font-weight-bold">
            {{ member }}
          </td>
          <td
            v-for="(obj, objIdx) in valueAsJson"
            :key="`obj-${member}-${objIdx}`"
          >
            {{ objMember(obj, member) }}
          </td>
        </tr>
      </tbody>
    </v-table>
    <p v-if="!isNonEmptyObjectArray">
      {{ value }}
    </p>
  </div>
</template>

<script lang="ts" setup>
import { computed } from 'vue'

interface Props {
  value: string
  vertical?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  vertical: false,
})

const valueAsJson = computed(() => {
  const val = props.value
  if (val === undefined) return ''
  try {
    const json = JSON.parse(val)
    if (json && json.constructor === Object) {
      return [json]
    }
    return json
  } catch {
    return ''
  }
})

const isNonEmptyObjectArray = computed(() => {
  const json = valueAsJson.value
  return Array.isArray(json) && json.length > 0 && json.every((ob) => ob && ob.constructor === Object)
})

const objectMembers = computed(() => {
  const objects: object[] = valueAsJson.value
  const set = new Set<string>()
  for (const obj of objects) {
    for (const member of Object.keys(obj)) {
      set.add(member)
    }
  }
  return Array.from(set)
})

const objMember = (obj: object, member: string): string => {
  // @ts-ignore
  const v = obj[member]
  if (v === undefined) return ''
  if (v && typeof v === 'string') return v
  if (v && v instanceof String) return v.toString()
  return JSON.stringify(v)
}
</script>

<style>
.fast-struct-table table {
  width: auto;
  border-collapse: collapse;
}
</style>
