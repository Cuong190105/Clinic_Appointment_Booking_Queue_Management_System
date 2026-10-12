<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { storeToRefs } from 'pinia'

import StatCard from '@/features/receptionist/components/StatCard.vue'
import QueuePreview from '@/features/receptionist/components/QueuePreview.vue'

import { useReceptionistStore } from '@/stores/receptionist'

const receptionistStore = useReceptionistStore()

const {
  queue,
  waitingCount,
  examiningCount,
  completedCount,
  queueError,
  queueLoading,
} = storeToRefs(receptionistStore)

onMounted(() => {
  receptionistStore.fetchQueue()
})

const stats = computed(() => [
  {
    label: 'Trong hàng chờ',
    value: queue.value.length,
    color: '#7C3AED',
  },
  {
    label: 'Đang chờ',
    value: waitingCount.value,
    color: '#D97706',
  },
  {
    label: 'Đang khám',
    value: examiningCount.value,
    color: '#0E4D92',
  },
  {
    label: 'Đã khám xong',
    value: completedCount.value,
    color: '#00A878',
  },
])
</script>

<template>
  <div class="space-y-5 max-w-5xl">

    <!-- ================= TITLE ================= -->
    <div>
      <h1 class="text-xl font-semibold text-slate-800">
        Tổng quan
      </h1>

      <p class="mt-1 text-sm text-slate-500">
        Tổng quan hoạt động khám bệnh trong ngày
      </p>
    </div>

    <!-- ================= STATISTICS ================= -->
    <div v-if="queueLoading" class="rounded-lg border border-blue-200 bg-blue-50 px-3 py-2 text-sm text-blue-700">
      Đang tải dữ liệu hàng chờ...
    </div>

    <div v-else-if="queueError" class="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-600">
      {{ queueError }}
    </div>

    <div class="grid grid-cols-4 gap-4">

      <StatCard
        v-for="stat in stats"
        :key="stat.label"
        :label="stat.label"
        :value="stat.value"
        :color="stat.color"
      />

    </div>

    <!-- ================= CURRENT QUEUE ================= -->
    <QueuePreview :queue="queue" />

  </div>
</template>
