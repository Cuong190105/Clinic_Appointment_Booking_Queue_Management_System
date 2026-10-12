<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { ApiError } from '@/lib/api/http'
import { doctorsApi } from '@/features/doctors/doctors.api'
import type { AppointmentDetail, DoctorDetail, QueueTicket } from '@/features/doctors/doctors.types'

const auth = useAuthStore()
const doctor = ref<DoctorDetail | null>(null)
const queue = ref<QueueTicket[]>([])
const selected = ref<AppointmentDetail | null>(null)
const loading = ref(true)
const actionLoading = ref(false)
const error = ref('')
const notice = ref('')
let timer: ReturnType<typeof setInterval> | undefined

const doctorId = computed(() => doctor.value?.id ?? '')
const activeTicket = computed(() =>
  queue.value.find((x) => ['Called', 'InProgress'].includes(x.status)),
)
const waiting = computed(() => queue.value.filter((x) => x.status === 'Waiting'))
const completed = computed(() => queue.value.filter((x) => x.status === 'Completed').length)

function messageFromError(e: unknown) {
  return e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Có lỗi xảy ra.'
}

async function loadProfile() {
  doctor.value = await doctorsApi.getOwnProfile()
}

async function loadQueue(silent = false) {
  if (!doctorId.value) return
  if (!silent) loading.value = true
  try {
    queue.value = await doctorsApi.getQueue(doctorId.value)
    if (selected.value) {
      const current = queue.value.find((x) => x.id === selected.value?.id)
      if (current?.status === 'Completed') selected.value = null
    }
  } catch (e) {
    error.value = messageFromError(e)
  } finally {
    if (!silent) loading.value = false
  }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    await loadProfile()
    await loadQueue(true)
  } catch (e) {
    error.value = messageFromError(e)
  } finally {
    loading.value = false
  }
}

async function selectPatient(ticket: QueueTicket) {
  try {
    selected.value = await doctorsApi.getAppointment(ticket.appointmentId)
  } catch (e) {
    error.value = messageFromError(e)
  }
}

async function runAction(action: () => Promise<unknown>, success: string) {
  actionLoading.value = true
  error.value = ''
  notice.value = ''
  try {
    await action()
    notice.value = success
    await loadQueue(true)
  } catch (e) {
    error.value = messageFromError(e)
  } finally {
    actionLoading.value = false
  }
}

async function callNext() {
  if (!doctorId.value) return
  await runAction(() => doctorsApi.callNext(doctorId.value), 'Đã gọi bệnh nhân tiếp theo.')
}

async function startExam() {
  if (!activeTicket.value) return
  await runAction(() => doctorsApi.startExam(activeTicket.value!.id), 'Đã bắt đầu khám.')
}

async function completeExam() {
  if (!activeTicket.value) return
  await runAction(() => doctorsApi.completeExam(activeTicket.value!.id), 'Đã hoàn tất lượt khám.')
  selected.value = null
}

async function skip(ticket: QueueTicket) {
  if (!confirm(`Bỏ qua số ${ticket.queueNumber}?`)) return
  await runAction(() => doctorsApi.skip(ticket.id), `Đã bỏ qua số ${ticket.queueNumber}.`)
}

async function togglePriority(ticket: QueueTicket) {
  await runAction(
    () => doctorsApi.setPriority(ticket.id, !ticket.priority),
    ticket.priority ? 'Đã bỏ ưu tiên.' : 'Đã đặt bệnh nhân vào diện ưu tiên.',
  )
}

function formatDate(value?: string) {
  if (!value) return '—'
  return new Intl.DateTimeFormat('vi-VN').format(new Date(`${value}T00:00:00`))
}

function statusLabel(status: string) {
  return ({ Waiting: 'Đang chờ', Called: 'Đang gọi', InProgress: 'Đang khám', Completed: 'Đã khám', Skipped: 'Bỏ qua' } as Record<string, string>)[status] ?? status
}

function statusClass(status: string) {
  return ({ Waiting: 'bg-amber-50 text-amber-700', Called: 'bg-blue-50 text-blue-700', InProgress: 'bg-violet-50 text-violet-700', Completed: 'bg-emerald-50 text-emerald-700', Skipped: 'bg-slate-100 text-slate-600' } as Record<string, string>)[status] ?? 'bg-slate-100 text-slate-600'
}

onMounted(async () => {
  await load()
  timer = setInterval(() => loadQueue(true), 10000)
})

onBeforeUnmount(() => timer && clearInterval(timer))
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
      <div>
        <p class="text-sm font-semibold text-violet-600">
          Bảng điều khiển bác sĩ
        </p>

        <h1 class="text-2xl font-bold text-slate-900">
          Khám bệnh & hàng đợi
        </h1>

        <p class="mt-1 text-sm text-slate-600">
          Xin chào {{ doctor?.fullName || auth.user?.name }}.
          Hàng đợi tự làm mới mỗi 10 giây.
        </p>
      </div>

      <button
        class="rounded-lg bg-violet-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-violet-700 disabled:opacity-50"
        :disabled="loading || actionLoading"
        @click="load"
      >
        Làm mới
      </button>
    </div>

    <!-- Messages -->
    <div
      v-if="error"
      class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm font-medium text-red-700"
    >
      {{ error }}
    </div>

    <div
      v-if="notice"
      class="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm font-medium text-emerald-700"
    >
      {{ notice }}
    </div>

    <!-- Stats -->
    <div class="grid gap-4 sm:grid-cols-3">
      <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <p class="text-sm font-medium text-slate-600">
          Đang chờ
        </p>

        <p class="mt-2 text-3xl font-bold text-amber-600">
          {{ waiting.length }}
        </p>
      </div>

      <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <p class="text-sm font-medium text-slate-600">
          Đang khám
        </p>

        <p class="mt-2 text-3xl font-bold text-violet-600">
          {{ activeTicket ? 1 : 0 }}
        </p>
      </div>

      <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <p class="text-sm font-medium text-slate-600">
          Đã hoàn tất hôm nay
        </p>

        <p class="mt-2 text-3xl font-bold text-emerald-600">
          {{ completed }}
        </p>
      </div>
    </div>

    <!-- Main -->
    <div class="grid gap-6 xl:grid-cols-[1.4fr_1fr]">

      <!-- Queue -->
      <section class="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div class="flex items-center justify-between border-b border-slate-200 p-5">
          <div>
            <h2 class="font-bold text-slate-900">
              Hàng đợi của tôi
            </h2>

            <p class="mt-1 text-sm text-slate-600">
              Các bệnh nhân đã check-in cho bác sĩ.
            </p>
          </div>

          <button
            class="rounded-lg bg-violet-600 px-4 py-2 text-sm font-semibold text-white hover:bg-violet-700 disabled:opacity-50"
            :disabled="!!activeTicket || waiting.length === 0 || actionLoading"
            @click="callNext"
          >
            Gọi số tiếp theo
          </button>
        </div>

        <div class="overflow-x-auto">
          <table class="min-w-full text-sm">

            <thead
              class="bg-slate-100 text-left text-xs font-bold uppercase tracking-wide text-slate-600"
            >
              <tr>
                <th class="px-5 py-3">Số</th>
                <th class="px-5 py-3">Bệnh nhân</th>
                <th class="px-5 py-3">Trạng thái</th>
                <th class="px-5 py-3">Ưu tiên</th>
                <th class="px-5 py-3 text-right">Thao tác</th>
              </tr>
            </thead>

            <tbody class="divide-y divide-slate-200">
              <tr
                v-for="ticket in queue"
                :key="ticket.id"
                class="text-slate-700 hover:bg-slate-50"
              >
                <!-- Queue number -->
                <td class="px-5 py-4 font-bold text-violet-700">
                  {{ ticket.queueNumber }}
                </td>

                <!-- Patient -->
                <td class="px-5 py-4">
                  <button
                    class="font-semibold text-slate-900 hover:text-violet-700"
                    @click="selectPatient(ticket)"
                  >
                    {{ ticket.patientName || 'Chưa có tên' }}
                  </button>

                  <p class="mt-1 text-xs text-slate-500">
                    {{
                      new Date(ticket.checkInTime).toLocaleTimeString(
                        'vi-VN',
                        {
                          hour: '2-digit',
                          minute: '2-digit',
                        },
                      )
                    }}
                  </p>
                </td>

                <!-- Status -->
                <td class="px-5 py-4">
                  <span
                    class="rounded-full px-2.5 py-1 text-xs font-semibold"
                    :class="statusClass(ticket.status)"
                  >
                    {{ statusLabel(ticket.status) }}
                  </span>
                </td>

                <!-- Priority -->
                <td class="px-5 py-4">
                  <button
                    class="text-xs font-semibold"
                    :class="
                      ticket.priority
                        ? 'text-red-600'
                        : 'text-slate-500 hover:text-slate-700'
                    "
                    :disabled="
                      actionLoading ||
                      ticket.status !== 'Waiting'
                    "
                    @click="togglePriority(ticket)"
                  >
                    {{
                      ticket.priority
                        ? '★ Ưu tiên'
                        : '☆ Đặt ưu tiên'
                    }}
                  </button>
                </td>

                <!-- Actions -->
                <td class="px-5 py-4 text-right">
                  <div class="flex justify-end gap-2">

                    <button
                      v-if="ticket.status === 'Waiting'"
                      class="rounded-md border border-slate-300 px-2.5 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-100"
                      :disabled="actionLoading"
                      @click="skip(ticket)"
                    >
                      Bỏ qua
                    </button>

                    <button
                      v-if="ticket.status === 'Called'"
                      class="rounded-md bg-violet-600 px-2.5 py-1.5 text-xs font-semibold text-white hover:bg-violet-700"
                      :disabled="actionLoading"
                      @click="startExam"
                    >
                      Bắt đầu khám
                    </button>

                    <button
                      v-if="ticket.status === 'InProgress'"
                      class="rounded-md bg-emerald-600 px-2.5 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700"
                      :disabled="actionLoading"
                      @click="completeExam"
                    >
                      Hoàn tất
                    </button>

                  </div>
                </td>
              </tr>

              <tr v-if="!loading && queue.length === 0">
                <td
                  colspan="5"
                  class="px-5 py-12 text-center text-slate-600"
                >
                  Hôm nay chưa có bệnh nhân trong hàng đợi.
                </td>
              </tr>
            </tbody>

          </table>
        </div>
      </section>

      <!-- Appointment detail -->
      <section
        class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"
      >
        <h2 class="font-bold text-slate-900">
          Thông tin lượt khám
        </h2>

        <p class="mt-1 text-sm text-slate-600">
          Chọn bệnh nhân trong hàng đợi để xem lịch hẹn.
        </p>

        <div
          v-if="selected"
          class="mt-5 space-y-4"
        >
          <!-- Patient -->
          <div class="rounded-xl bg-violet-50 p-4">
            <p
              class="text-xs font-bold uppercase tracking-wide text-violet-600"
            >
              Bệnh nhân
            </p>

            <p class="mt-1 text-lg font-bold text-slate-900">
              {{ selected.patientName }}
            </p>
          </div>

          <!-- Info -->
          <dl class="grid grid-cols-2 gap-4 text-sm">

            <div>
              <dt class="font-medium text-slate-500">
                Ngày khám
              </dt>

              <dd class="mt-1 font-semibold text-slate-800">
                {{ formatDate(selected.date) }}
              </dd>
            </div>

            <div>
              <dt class="font-medium text-slate-500">
                Giờ
              </dt>

              <dd class="mt-1 font-semibold text-slate-800">
                {{ selected.timeSlot?.slice(0, 5) }}
              </dd>
            </div>

            <div>
              <dt class="font-medium text-slate-500">
                Trạng thái
              </dt>

              <dd class="mt-1 font-semibold text-slate-800">
                {{ selected.status }}
              </dd>
            </div>

            <div>
              <dt class="font-medium text-slate-500">
                Số hàng đợi
              </dt>

              <dd class="mt-1 font-semibold text-slate-800">
                {{ selected.queueNumber || '—' }}
              </dd>
            </div>

          </dl>

          <!-- Reason -->
          <div>
            <p class="text-sm font-semibold text-slate-800">
              Lý do khám
            </p>

            <p
              class="mt-1 rounded-lg bg-slate-100 p-3 text-sm text-slate-700"
            >
              {{ selected.reason || 'Không có thông tin.' }}
            </p>
          </div>

          <!-- Medical report -->
          <div
            v-if="selected.medicalReport"
            class="rounded-xl border border-slate-200 bg-white p-4"
          >
            <p class="font-bold text-slate-900">
              Hồ sơ khám đã có
            </p>

            <p class="mt-2 text-sm text-slate-700">
              {{
                selected.medicalReport.diagnosis ||
                'Chưa có chẩn đoán.'
              }}
            </p>
          </div>
        </div>

        <!-- Empty -->
        <div
          v-else
          class="mt-10 rounded-xl bg-slate-100 p-8 text-center text-sm font-medium text-slate-600"
        >
          Chưa chọn bệnh nhân.
        </div>
      </section>
    </div>
  </div>
</template>
