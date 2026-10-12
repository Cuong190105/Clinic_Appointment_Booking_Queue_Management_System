<script setup lang="ts">
import type { MedicalRecord } from '../patient.types'

const props = defineProps<{
  record: MedicalRecord
}>()

const formatDate = (dateString?: string) => {
  if (!dateString) return ''
  return new Date(dateString).toLocaleDateString('vi-VN')
}
</script>

<template>
  <article class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition-all hover:border-slate-300">
    <div class="mb-3 flex items-start justify-between gap-3">
      <div>
        <p class="text-xs font-medium text-slate-400">
          {{ formatDate(record.examinationDate) }}
        </p>
        <h3 class="text-base font-bold text-slate-800">
          {{ record.doctorName }}
        </h3>
      </div>

      <span class="rounded-full border border-blue-200 bg-blue-50 px-2.5 py-1 text-xs font-semibold text-[#0E4D92]">
        {{ record.specialty }}
      </span>
    </div>

    <div class="space-y-3 rounded-xl bg-slate-50 p-4 text-sm text-slate-700">
      <div>
        <p class="text-xs font-semibold uppercase tracking-wider text-slate-400">Chẩn đoán</p>
        <p class="mt-1 font-medium text-slate-800">{{ record.diagnosis || 'Không có' }}</p>
      </div>

      <div class="border-t border-slate-200 pt-3">
        <p class="text-xs font-semibold uppercase tracking-wider text-slate-400">Đơn thuốc</p>
        <p class="mt-1 font-medium text-slate-800">{{ record.prescription || 'Không có' }}</p>
      </div>

      <div v-if="record.note" class="border-t border-slate-200 pt-3">
        <p class="text-xs font-semibold uppercase tracking-wider text-slate-400">Ghi chú của bác sĩ</p>
        <p class="mt-1 italic text-slate-600">{{ record.note }}</p>
      </div>
    </div>
  </article>
</template>
