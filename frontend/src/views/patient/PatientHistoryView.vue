<script setup lang="ts">
import { onMounted } from 'vue'
import { usePatientStore } from '@/stores/patient'
import PatientHistoryCard from '@/features/patients/components/PatientHistoryCard.vue'
import PatientBackendNotice from '@/features/patients/components/PatientBackendNotice.vue'

const patient = usePatientStore()

// Tải lịch sử khám bệnh khi component được mount
onMounted(() => {
  patient.loadHistory()
})

async function reloadHistory() {
  await patient.loadHistory()
}
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-5">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-slate-800">
          Lịch sử khám bệnh
        </h1>
        <p class="mt-1 text-sm text-slate-500">
          {{ (patient.sortedHistory ?? patient.history)?.length || 0 }} lần khám đã ghi nhận
        </p>
      </div>

      <button
        v-if="!patient.historyLoading"
        type="button"
        class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-slate-200 bg-white text-xs font-semibold text-slate-700 hover:bg-slate-50 shadow-2xs"
        @click="reloadHistory"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67"/>
        </svg>
        <span>Làm mới</span>
      </button>
    </div>

    <!-- Backend notice nếu bị tạm ngắt kết nối -->
    <PatientBackendNotice @reconnected="reloadHistory" />

    <!-- Trạng thái 1: Đang tải -->
    <div
      v-if="patient.historyLoading"
      class="py-12 text-center text-sm text-slate-500 rounded-2xl border border-slate-200 bg-white"
    >
      <div class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-[#0E4D92] border-t-transparent mb-2" />
      <p>Đang tải lịch sử khám...</p>
    </div>

    <!-- Trạng thái 2: Lỗi -->
    <div
      v-else-if="patient.historyError"
      class="rounded-2xl border border-red-200 bg-red-50 p-6 text-center text-sm text-red-700"
    >
      <p class="font-bold mb-1">Không thể tải lịch sử khám</p>
      <p class="text-xs text-red-600 mb-3">{{ patient.historyError }}</p>
      <button
        type="button"
        class="px-4 py-2 rounded-xl bg-red-600 text-white text-xs font-semibold hover:bg-red-700"
        @click="reloadHistory"
      >
        Thử lại
      </button>
    </div>

    <!-- Trạng thái 3: Trống dữ liệu -->
    <div
      v-else-if="!(patient.sortedHistory?.length || patient.history?.length)"
      class="rounded-2xl border border-slate-200 bg-white p-10 text-center text-sm text-slate-500 shadow-xs"
    >
      <div class="text-4xl mb-2">📋</div>
      <p class="font-bold text-slate-700">Chưa có lịch sử khám.</p>
      <p class="text-xs text-slate-400 mt-1">Các kết quả khám và đơn thuốc từ bác sĩ sẽ được hiển thị tại đây sau khi hoàn tất buổi khám.</p>
    </div>

    <!-- Trạng thái 4: Hiển thị danh sách -->
    <div v-else class="space-y-4">
      <PatientHistoryCard
        v-for="record in (patient.sortedHistory ?? patient.history)"
        :key="record.id"
        :record="record"
      />
    </div>
  </div>
</template>