<script setup lang="ts">
import type { PatientSearchItem } from '../receptionist.types'

defineProps<{
  patient: PatientSearchItem
}>()

const emit = defineEmits<{
  (e: 'change-patient'): void
}>()
</script>

<template>
  <div class="rounded-2xl border border-blue-200 bg-blue-50/40 p-5 shadow-2xs space-y-3">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2.5">
        <div class="flex h-9 w-9 items-center justify-center rounded-xl bg-[#0E4D92] text-sm text-white font-bold">
          👤
        </div>
        <div>
          <h2 class="text-sm font-bold text-slate-800">
            2. Thông tin bệnh nhân đã chọn
          </h2>
          <p class="text-xs text-slate-500">
            Hồ sơ y tế liên kết trong hệ thống
          </p>
        </div>
      </div>

      <button
        type="button"
        class="text-xs font-semibold text-[#0E4D92] hover:underline cursor-pointer"
        @click="emit('change-patient')"
      >
        Đổi bệnh nhân
      </button>
    </div>

    <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2 text-xs border-t border-blue-100">
      <div>
        <span class="text-slate-500">Họ và tên:</span>
        <span class="ml-1.5 font-bold text-slate-800 text-sm">{{ patient.fullName }}</span>
      </div>
      <div>
        <span class="text-slate-500">Số điện thoại:</span>
        <span class="ml-1.5 font-semibold text-slate-700">{{ patient.phoneNumber || '—' }}</span>
      </div>
      <div>
        <span class="text-slate-500">Mã BHYT:</span>
        <span class="ml-1.5 font-semibold text-slate-700">{{ patient.insuranceNumber || '—' }}</span>
      </div>
      <div>
        <span class="text-slate-500">Email:</span>
        <span class="ml-1.5 font-semibold text-slate-700">{{ patient.email || '—' }}</span>
      </div>
    </div>
  </div>
</template>
