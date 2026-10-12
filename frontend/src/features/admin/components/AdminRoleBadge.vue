<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  role: string
}>()

const normalizedRole = computed(() => {
  const r = (props.role || '').toLowerCase()
  if (r === 'admin') return 'admin'
  if (r === 'doctor') return 'doctor'
  if (r === 'receptionist') return 'receptionist'
  return 'patient'
})

const roleText: Record<string, string> = {
  patient: 'Bệnh nhân',
  doctor: 'Bác sĩ',
  receptionist: 'Lễ tân',
  admin: 'Quản trị viên',
}

const roleClass: Record<string, string> = {
  patient: 'bg-slate-100 text-slate-600',
  doctor: 'bg-blue-50 text-blue-700',
  receptionist: 'bg-indigo-50 text-indigo-700',
  admin: 'bg-amber-50 text-amber-700',
}
</script>

<template>
  <span
    class="inline-flex rounded-md px-2.5 py-1 text-xs font-medium"
    :class="roleClass[normalizedRole] || 'bg-slate-100 text-slate-600'"
  >
    {{ roleText[normalizedRole] || role }}
  </span>
</template>
