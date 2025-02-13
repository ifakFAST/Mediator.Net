<template>

  <div class="fast-struct-table" style="overflow-x: auto;">

    <table v-if="isNonEmptyObjectArray && vertical">
      <thead>
        <tr>
          <th v-for="member in objectMembers" :key="`member-${member}`">
            {{ member }}
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(obj, objIdx) in valueAsJson" :key="`obj-${objIdx}`">
          <td v-for="(member, memberIdx) in objectMembers" :key="`td-${objIdx}-${memberIdx}`">
            {{ objMember(obj, member) }}
          </td>
        </tr>
      </tbody>
    </table>

    <table v-if="isNonEmptyObjectArray && !vertical">
      <tbody>
        <tr v-for="member in objectMembers" :key="member">
          <td style="font-weight: bold;"> {{ member }}</td>
          <td v-for="(obj, objIdx) in valueAsJson" :key="`obj-${member}-${objIdx}`">
            {{ objMember(obj, member) }}
          </td>
        </tr>
      </tbody>
    </table>

    <p v-if="!isNonEmptyObjectArray">{{value}}</p>

  </div>


</template>

<script lang="ts">

import { Component, Prop, Vue } from 'vue-property-decorator'

@Component
export default class StructView extends Vue {

  @Prop(String) value: string
  @Prop(Boolean) vertical: boolean

  get valueAsJson(): any {
    const val = this.value
    if (val === undefined) { return '' }
    try {
      const json = JSON.parse(val)
      if (json && json.constructor === Object) {
        return [ json ]
      }
      return json
    }
    catch (error) {
      return ''
    }
  }

  get isNonEmptyObjectArray(): boolean {
    const json = this.valueAsJson
    return Array.isArray(json) && json.length > 0 && json.every((ob) => ob && ob.constructor === Object)
  }

  get objectMembers(): string[] {
    const objects: object[] = this.valueAsJson
    const set = new Set<string>()
    for (const obj of objects) {
      for (const member of Object.keys(obj)) {
        set.add(member)
      }
    }
    return Array.from(set)
  }

  objMember(obj: any, member: string): string {
    const v = obj[member]
    if (v === undefined) { return '' }
    if (v && typeof v === 'string') { return v }
    if (v && v instanceof String) { return v.toString() }
    return JSON.stringify(v)
  }
}

</script>

<style>

  .fast-struct-table table {
    width: auto;
    border-collapse: collapse;
  }

  .fast-struct-table table thead th {
    font-size: 16px;
    font-weight: bold;
    background-color: #f2f2f2;
    height: 30px;
    border-collapse: collapse;
    padding-left: 8px;
    padding-right: 8px;
    text-align:left;
    border-bottom: 1px solid #bbb;
  }

  .fast-struct-table table tbody td {
    font-size: 16px;
    border-collapse: collapse;
    padding-left: 8px;
    padding-right: 8px;
    text-align:left;
    border-bottom: 1px solid #bbb;
  }

</style>