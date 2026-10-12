<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { usePatientStore } from '@/stores/patient'
import BaseButton from '@/components/ui/BaseButton.vue'
import PatientQueueCard from '@/features/patients/components/PatientQueueCard.vue'
import PatientBackendNotice from '@/features/patients/components/PatientBackendNotice.vue'

const patient = usePatientStore()

let timer: ReturnType<typeof setTimeout> | undefined
let isMounted = false

const isFetching = ref(false)
const isError = ref(false)
const lastUpdatedTime = ref<string | null>(null)

const notificationPermission = ref<NotificationPermission>('default')
const notifiedTicket = ref<string | null>(null)

// Format thời gian cập nhật
const formatTime = (date: Date) => {
  return date.toLocaleTimeString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
  })
}

// Gửi Notification bằng Web Notification API
const sendNotification = (title: string, body: string) => {
  if (!('Notification' in window)) return
  if (Notification.permission !== 'granted') return

  try {
    new Notification(title, {
      body,
      icon: '/favicon.ico',
    })
  } catch (error) {
    console.warn('Không thể gửi Notification:', error)
  }
}

// Kiểm tra vị trí hàng đợi và gửi thông báo
const checkAndSendNotification = () => {
  if (!patient.queue) return

  const { position, myTicket: ticket } = patient.queue

  // Sắp đến lượt: vị trí 1 hoặc 2
  if (
    position > 0 &&
    position <= 2 &&
    notifiedTicket.value !== ticket
  ) {
    sendNotification(
      'Sắp đến lượt khám!',
      `Bạn đang ở vị trí thứ #${position}. Vui lòng chuẩn bị vào phòng khám!`
    )
    notifiedTicket.value = ticket
  }

  if (
    (position > 2 || position <= 0) &&
    notifiedTicket.value === ticket
  ) {
    notifiedTicket.value = null
  }
}

// Lên lịch gọi API tiếp theo sau 30 giây
const scheduleNextFetch = () => {
  if (timer) {
    clearTimeout(timer)
    timer = undefined
  }

  if (!isMounted) return

  timer = setTimeout(() => {
    fetchQueue()
  }, 30000)
}

// Gọi API lấy thông tin hàng đợi
const fetchQueue = async () => {
  if (!isMounted || isFetching.value) return

  isFetching.value = true
  isError.value = false

  try {
    await patient.loadQueue()
    if (!isMounted) return

    if (patient.queueError) {
      isError.value = true
      return
    }

    isError.value = false
    lastUpdatedTime.value = formatTime(new Date())
    checkAndSendNotification()
  } catch (error) {
    if (!isMounted) return
    isError.value = true
    console.error('Lỗi khi tải hàng đợi:', error)
  } finally {
    isFetching.value = false
    if (isMounted) {
      scheduleNextFetch()
    }
  }
}

// Xin quyền gửi Notification
const requestNotificationPermission = async () => {
  if (!('Notification' in window)) {
    notificationPermission.value = 'denied'
    return
  }

  try {
    const permission = await Notification.requestPermission()
    notificationPermission.value = permission

    if (permission === 'granted') {
      checkAndSendNotification()
    }
  } catch (error) {
    console.error('Lỗi xin quyền thông báo:', error)
  }
}

// Đếm số người đang Waiting
const waitingCount = computed(() => {
  return (
    patient.queue?.entries.filter(
      (entry) => entry.status === 'Waiting'
    ).length ?? 0
  )
})

onMounted(async () => {
  isMounted = true

  if ('Notification' in window) {
    notificationPermission.value = Notification.permission
  } else {
    notificationPermission.value = 'denied'
  }

  await fetchQueue()
})

onUnmounted(() => {
  isMounted = false
  if (timer) {
    clearTimeout(timer)
    timer = undefined
  }
})

defineExpose({
  isError,
  isFetching,
  lastUpdatedTime,
  waitingCount,
  requestNotificationPermission,
  fetchQueue,
})
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-5">
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-slate-800">
          Trạng thái hàng đợi
        </h1>
        <p v-if="lastUpdatedTime" class="text-xs text-slate-500 mt-0.5">
          Cập nhật lúc: <span class="font-medium text-slate-700">{{ lastUpdatedTime }}</span>
        </p>
      </div>

      <div class="text-right">
        <span class="inline-flex items-center gap-1.5 rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-[#0E4D92] border border-blue-200">
          <span class="h-2 w-2 rounded-full bg-[#0E4D92] animate-pulse" />
          Đang chờ: {{ waitingCount }}
        </span>
      </div>
    </div>

    <!-- Notice if backend is offline -->
    <PatientBackendNotice @reconnected="fetchQueue" />

    <div v-if="isFetching && !patient.queue" class="py-12 text-center text-sm text-slate-500 rounded-2xl border border-slate-200 bg-white">
      <div class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-[#0E4D92] border-t-transparent mb-2" />
      <p>Đang tải thông tin hàng đợi...</p>
    </div>

    <div v-else-if="isError || patient.queueError" class="rounded-2xl border border-red-200 bg-red-50 p-6 text-center text-sm text-red-700">
      <p class="font-bold mb-1">Không thể tải hàng đợi</p>
      <p class="text-xs text-red-600 mb-3">{{ patient.queueError || 'Lỗi kết nối máy chủ' }}</p>
      <button
        type="button"
        class="px-4 py-2 rounded-xl bg-red-600 text-white text-xs font-semibold hover:bg-red-700"
        @click="fetchQueue"
      >
        Thử lại
      </button>
    </div>

    <div v-else class="space-y-4">
      <!-- Your Ticket Status Card -->
      <PatientQueueCard :queue="patient.queue" />

      <!-- Notification button if not yet granted -->
      <div
        v-if="notificationPermission !== 'granted' && patient.queue?.myTicket"
        class="rounded-xl border border-slate-200 bg-white p-4 flex items-center justify-between gap-3 shadow-2xs"
      >
        <div class="text-xs text-slate-600">
          <p class="font-semibold text-slate-800">Nhận thông báo khi sắp đến lượt?</p>
          <p class="text-slate-400 mt-0.5">Hệ thống sẽ gửi thông báo đến thiết bị của bạn khi vị trí còn 1-2 người.</p>
        </div>
        <BaseButton
          size="sm"
          variant="outline"
          @click="requestNotificationPermission"
        >
          🔔 Bật thông báo
        </BaseButton>
      </div>

      <!-- Queue list -->
      <div v-if="patient.queue?.entries?.length" class="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
        <h2 class="font-bold text-slate-800 text-base mb-3">Danh sách lượt khám hiện tại</h2>

        <div class="space-y-2.5">
          <div
            v-for="entry in patient.queue.entries"
            :key="entry.ticket"
            class="flex items-center justify-between rounded-xl border p-3.5 transition-colors"
            :class="
              entry.ticket === patient.queue.myTicket
                ? 'border-[#0E4D92] bg-blue-50/40 ring-1 ring-[#0E4D92]'
                : 'border-slate-100 bg-slate-50/50 hover:bg-slate-50'
            "
          >
            <div>
              <div class="flex items-center gap-2">
                <span class="text-sm font-bold text-slate-800">{{ entry.patientName }}</span>
                <span
                  v-if="entry.ticket === patient.queue.myTicket"
                  class="rounded-md bg-[#0E4D92] px-1.5 py-0.5 text-[10px] font-bold text-white uppercase"
                >
                  Bạn
                </span>
              </div>
              <p class="text-xs text-slate-500 mt-0.5">
                {{ entry.doctorName }} <span v-if="entry.appointmentTime">· {{ entry.appointmentTime }}</span>
              </p>
            </div>

            <div class="text-right">
              <span class="text-sm font-extrabold text-[#0E4D92]">{{ entry.ticket }}</span>
              <p class="text-xs font-medium text-slate-500">Vị trí: <span class="font-bold text-slate-700">#{{ entry.position }}</span></p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
