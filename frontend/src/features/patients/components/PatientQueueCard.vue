<script setup lang="ts">
import type { QueueStatus } from '../patient.types'

const props = defineProps<{
  queue: QueueStatus | null
  showDetailsLink?: boolean
}>()
</script>

<template>
  <div v-if="queue && queue.myTicket" class="rounded-2xl border border-blue-200 bg-blue-50/60 p-5 shadow-sm">
    <div class="flex items-center gap-5">
      <!-- Số thứ tự của bạn -->
      <div class="text-center shrink-0 min-w-[90px] border-r border-blue-200 pr-5">
        <p class="text-xs font-semibold text-slate-500">Số của bạn</p>
        <p class="text-3xl font-extrabold text-[#0E4D92] tracking-tight mt-0.5">{{ queue.myTicket }}</p>
      </div>

      <!-- Vị trí & Thời gian chờ -->
      <div class="grid flex-1 grid-cols-2 gap-3">
        <div class="rounded-xl bg-white p-3 border border-slate-100 shadow-2xs">
          <p class="text-xs font-medium text-slate-400">Vị trí hàng đợi</p>
          <p class="text-lg font-bold text-slate-800">#{{ queue.position }}</p>
        </div>

        <div class="rounded-xl bg-white p-3 border border-slate-100 shadow-2xs">
          <p class="text-xs font-medium text-slate-400">Ước tính chờ</p>
          <p class="text-lg font-bold text-amber-600">~{{ queue.estimatedWaitMinutes }} <span class="text-xs font-normal text-slate-500">phút</span></p>
        </div>
      </div>
    </div>

    <!-- Tên BS nếu có -->
    <div v-if="queue.doctorName && queue.doctorName !== 'BS. Chưa xác định'" class="mt-3.5 pt-3 border-t border-blue-100 flex items-center justify-between text-xs">
      <span class="text-slate-500 font-medium">Bác sĩ phụ trách:</span>
      <span class="font-bold text-slate-800">{{ queue.doctorName }}</span>
    </div>

    <RouterLink
      v-if="showDetailsLink"
      to="/patient/queue"
      class="mt-4 flex items-center justify-center gap-1.5 w-full rounded-xl border-2 border-[#0E4D92] py-2.5 text-center text-sm font-semibold text-[#0E4D92] transition-colors hover:bg-blue-50"
    >
      <span>Xem chi tiết hàng đợi</span>
      <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <polyline points="9 18 15 12 9 6"/>
      </svg>
    </RouterLink>
  </div>

  <div v-else class="rounded-2xl border border-slate-200 bg-white p-6 text-center text-sm text-slate-500 shadow-sm">
    <p class="font-medium text-slate-600">Hiện tại bạn không có trong hàng đợi khám.</p>
    <p class="text-xs text-slate-400 mt-1">Vui lòng đặt lịch hoặc check-in tại quầy lễ tân khi đến phòng khám.</p>
  </div>
</template>
