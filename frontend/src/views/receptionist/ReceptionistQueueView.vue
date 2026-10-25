<script setup lang="ts">
import { onMounted } from 'vue'
import { storeToRefs } from 'pinia'

import QueueTable from '@/features/receptionist/components/QueueTable.vue'
import { useReceptionistStore } from '@/stores/receptionist'

const receptionistStore = useReceptionistStore()

const { queue, queueLoading, queueError } = storeToRefs(receptionistStore)

onMounted(() => {
  receptionistStore.fetchQueue()
})

async function callPatient(ticketId: string) {
  await receptionistStore.callPatient(ticketId)
}

async function skipPatient(ticketId: string) {
  await receptionistStore.skipPatient(ticketId)
}

async function retryPatient(ticketId: string) {
  await receptionistStore.retryPatient(ticketId)
}

async function setPriority(ticketId: string, value: boolean) {
  await receptionistStore.setPatientPriority(ticketId, value)
}
</script>

<template>
  <div class="space-y-5 max-w-6xl">

    <!-- TITLE -->
    <div>
      <h1 class="text-xl font-semibold text-slate-800">
        Quản lý hàng đợi
      </h1>

      <p class="mt-1 text-sm text-slate-500">
        Theo dõi và quản lý bệnh nhân đang chờ khám
      </p>
    </div>

    <!-- TABLE CARD -->
    <div
      class="bg-white border border-slate-200
             rounded-xl p-6"
    >

      <h2
        class="text-sm font-semibold
               text-slate-800 mb-4"
      >
        Danh sách hàng đợi
      </h2>

      <div
        v-if="queueError"
        class="mb-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-600"
      >
        {{ queueError }}
      </div>

      <div
        v-if="queueLoading"
        class="mb-4 text-sm text-slate-500"
      >
        Đang tải hàng đợi...
      </div>

      <QueueTable
        :queue="queue"
        :loading="queueLoading"
        @call="callPatient"
        @skip="skipPatient"
        @retry="retryPatient"
        @priority="setPriority"
      />

    </div>

  </div>
</template>
