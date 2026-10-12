<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'

import { doctorsApi } from '@/features/doctors/doctors.api'
import type {
  DoctorSchedule,
  RequestedShift,
} from '@/features/doctors/doctors.types'

function isoDate(date: Date) {
  return date.toISOString().slice(0, 10)
}

function formatTime(time: string) {
  // 08:00 -> 08:00:00
  if (/^\d{2}:\d{2}$/.test(time)) {
    return `${time}:00`
  }

  return time
}

const now = new Date()

const end = new Date(now)
end.setDate(end.getDate() + 27)

const startDate = ref(isoDate(now))
const endDate = ref(isoDate(end))

const data = ref<DoctorSchedule<RequestedShift> | null>(null)

const loading = ref(false)
const saving = ref(false)

const error = ref('')
const notice = ref('')

const editingId = ref<string | null>(null)

const form = reactive({
  date: isoDate(now),
  startTime: '08:00',
  endTime: '12:00',
  patientLimit: 20,
  reason: '',
})

function getErrorMessage(e: unknown) {
  const error = e as {
    message?: string
    response?: {
      data?: {
        detail?: string
        title?: string
        message?: string
        errorMessages?: string[]
      }
    }
  }

  const responseData = error.response?.data

  if (responseData?.errorMessages?.length) {
    return responseData.errorMessages.join(', ')
  }

  if (responseData?.detail) {
    return responseData.detail
  }

  if (responseData?.title) {
    return responseData.title
  }

  if (responseData?.message) {
    return responseData.message
  }

  if (error.message) {
    return error.message
  }

  return 'Có lỗi xảy ra.'
}

function reset() {
  editingId.value = null

  form.date = isoDate(new Date())
  form.startTime = '08:00'
  form.endTime = '12:00'
  form.patientLimit = 20
  form.reason = ''
}

async function load() {
  if (startDate.value && endDate.value) {
    const s = new Date(startDate.value)
    const e = new Date(endDate.value)
    const maxEnd = new Date(s)
    maxEnd.setMonth(maxEnd.getMonth() + 1)
    if (e > maxEnd) {
      error.value = 'Khoảng thời gian tra cứu không được vượt quá 1 tháng theo quy định hệ thống.'
      return
    }
  }

  loading.value = true
  error.value = ''

  try {
    const res = await doctorsApi.getOwnShiftRequests(
      startDate.value,
      endDate.value,
    )
    data.value = (res as any)?.result ?? res
  } catch (e) {
    console.error('GET SHIFT SUGGESTIONS ERROR:', e)

    error.value = getErrorMessage(e)
  } finally {
    loading.value = false
  }
}

function edit(item: RequestedShift) {
  editingId.value = item.id

  form.date = item.date

  // Ví dụ:
  // 08:00:00 -> 08:00
  // 08:00:00.000Z -> 08:00
  form.startTime = item.startTime.slice(0, 5)
  form.endTime = item.endTime.slice(0, 5)

  form.patientLimit = item.patientLimit
  form.reason = item.reason ?? ''

  window.scrollTo({
    top: 0,
    behavior: 'smooth',
  })
}

async function submit() {
  saving.value = true
  error.value = ''
  notice.value = ''

  try {
    if (editingId.value) {
      await doctorsApi.updateShiftRequest(editingId.value, {
        date: form.date,

        startTime: formatTime(form.startTime),
        endTime: formatTime(form.endTime),

        patientLimitPerSlot: Number(form.patientLimit),

        reason: form.reason,
      })

      notice.value = 'Đã cập nhật yêu cầu.'
    } else {
      await doctorsApi.createShiftRequest({
        date: form.date,

        startTime: formatTime(form.startTime),
        endTime: formatTime(form.endTime),

        patientLimit: Number(form.patientLimit),

        reason: form.reason,
      })

      notice.value = 'Đã gửi yêu cầu ca làm việc.'
    }

    reset()

    await load()
  } catch (e) {
    console.error('CREATE / UPDATE SHIFT ERROR:', e)

    const axiosError = e as {
      response?: {
        status?: number
        data?: unknown
      }
    }

    console.error(
      'STATUS:',
      axiosError.response?.status,
    )

    console.error(
      'RESPONSE DATA:',
      axiosError.response?.data,
    )

    error.value = getErrorMessage(e)
  } finally {
    saving.value = false
  }
}

async function cancel(item: RequestedShift) {
  const confirmed = window.confirm(
    `Huỷ yêu cầu ca ngày ${item.date}?`,
  )

  if (!confirmed) {
    return
  }

  saving.value = true
  error.value = ''
  notice.value = ''

  try {
    await doctorsApi.cancelShiftRequest(item.id)

    notice.value = 'Đã huỷ yêu cầu.'

    await load()
  } catch (e) {
    console.error('CANCEL SHIFT ERROR:', e)

    const axiosError = e as {
      response?: {
        status?: number
        data?: unknown
      }
    }

    console.error(
      'STATUS:',
      axiosError.response?.status,
    )

    console.error(
      'RESPONSE DATA:',
      axiosError.response?.data,
    )

    error.value = getErrorMessage(e)
  } finally {
    saving.value = false
  }
}

function statusClass(status: string) {
  const classes: Record<string, string> = {
    Pending: 'bg-amber-100 text-amber-700',
    Approved: 'bg-emerald-100 text-emerald-700',
    Rejected: 'bg-red-100 text-red-700',
    Cancelled: 'bg-slate-200 text-slate-700',
  }

  return classes[status] ?? 'bg-slate-100 text-slate-600'
}

onMounted(() => {
  load()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="rounded-2xl border border-violet-100 bg-white p-5 shadow-sm">
      <p class="text-sm font-semibold text-violet-600">Bác sĩ</p>

      <h1 class="mt-1 text-2xl font-bold text-slate-900">
        Đề xuất ca làm việc
      </h1>

      <p class="mt-1 text-sm text-slate-500">
        Bác sĩ gửi yêu cầu, quản trị viên sẽ phê duyệt hoặc từ chối.
      </p>
    </div>

    <!-- Error -->
    <div
      v-if="error"
      class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm font-medium text-red-700"
    >
      {{ error }}
    </div>

    <!-- Notice -->
    <div
      v-if="notice"
      class="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm font-medium text-emerald-700"
    >
      {{ notice }}
    </div>

    <!-- Form -->
    <section
      class="rounded-2xl border border-violet-100 bg-white p-5 shadow-sm"
    >
      <div class="border-b border-slate-100 pb-4">
        <h2 class="font-semibold text-slate-900">
          {{ editingId ? 'Chỉnh sửa yêu cầu' : 'Tạo yêu cầu mới' }}
        </h2>

        <p class="mt-1 text-sm text-slate-500">
          Chọn thời gian và số lượng bệnh nhân cho ca làm việc.
        </p>
      </div>

      <form
        class="mt-5 grid gap-4 md:grid-cols-2 lg:grid-cols-5"
        @submit.prevent="submit"
      >
        <!-- Date -->
        <label class="text-sm font-medium text-slate-700">
          Ngày

          <input
            v-model="form.date"
            type="date"
            required
            class="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 outline-none transition focus:border-violet-500 focus:ring-2 focus:ring-violet-100"
          />
        </label>

        <!-- Start -->
        <label class="text-sm font-medium text-slate-700">
          Bắt đầu

          <input
            v-model="form.startTime"
            type="time"
            required
            class="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 outline-none transition focus:border-violet-500 focus:ring-2 focus:ring-violet-100"
          />
        </label>

        <!-- End -->
        <label class="text-sm font-medium text-slate-700">
          Kết thúc

          <input
            v-model="form.endTime"
            type="time"
            required
            class="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 outline-none transition focus:border-violet-500 focus:ring-2 focus:ring-violet-100"
          />
        </label>

        <!-- Limit -->
        <label class="text-sm font-medium text-slate-700">
          Giới hạn bệnh nhân

          <input
            v-model.number="form.patientLimit"
            type="number"
            min="1"
            max="50"
            required
            class="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 outline-none transition focus:border-violet-500 focus:ring-2 focus:ring-violet-100"
          />
        </label>

        <!-- Reason -->
        <label
          class="text-sm font-medium text-slate-700 md:col-span-2 lg:col-span-1"
        >
          Lý do

          <input
            v-model="form.reason"
            type="text"
            required
            placeholder="Ví dụ: tăng ca"
            class="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 outline-none transition placeholder:text-slate-400 focus:border-violet-500 focus:ring-2 focus:ring-violet-100"
          />
        </label>

        <!-- Buttons -->
        <div class="flex items-center gap-3 lg:col-span-5">
          <button
            class="rounded-lg bg-violet-600 px-5 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-violet-700 disabled:cursor-not-allowed disabled:opacity-50"
            :disabled="saving"
          >
            {{
              saving
                ? 'Đang xử lý...'
                : editingId
                  ? 'Lưu thay đổi'
                  : 'Gửi yêu cầu'
            }}
          </button>

          <button
            v-if="editingId"
            type="button"
            class="rounded-lg border border-slate-300 bg-white px-5 py-2.5 text-sm font-semibold text-slate-700 transition hover:bg-slate-50"
            @click="reset"
          >
            Huỷ sửa
          </button>
        </div>
      </form>
    </section>

    <!-- History -->
    <section
      class="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm"
    >
      <!-- Section header -->
      <div
        class="flex flex-col justify-between gap-4 border-b border-slate-200 bg-slate-50 p-5 lg:flex-row lg:items-end"
      >
        <div>
          <h2 class="font-semibold text-slate-900">
            Lịch sử yêu cầu
          </h2>

          <p class="mt-1 text-sm text-slate-500">
            {{ data?.schedules.length ?? 0 }} yêu cầu trong khoảng thời gian đã chọn
          </p>
        </div>

        <div class="flex flex-wrap gap-2">
          <input
            v-model="startDate"
            type="date"
            class="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-violet-500 focus:ring-2 focus:ring-violet-100"
          />

          <input
            v-model="endDate"
            type="date"
            class="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-violet-500 focus:ring-2 focus:ring-violet-100"
          />

          <button
            class="rounded-lg border border-violet-200 bg-violet-50 px-4 py-2 text-sm font-semibold text-violet-700 transition hover:bg-violet-100"
            :disabled="loading"
            @click="load"
          >
            {{ loading ? 'Đang tải...' : 'Lọc' }}
          </button>
        </div>
      </div>

      <!-- Table -->
      <div class="overflow-x-auto">
        <table class="min-w-full text-sm">
          <thead
            class="bg-slate-100 text-left text-xs font-semibold uppercase tracking-wide text-slate-600"
          >
            <tr>
              <th class="px-5 py-3">Ngày</th>
              <th class="px-5 py-3">Thời gian</th>
              <th class="px-5 py-3 text-center">Giới hạn</th>
              <th class="px-5 py-3">Lý do</th>
              <th class="px-5 py-3 text-center">Trạng thái</th>
              <th class="px-5 py-3 text-right">Thao tác</th>
            </tr>
          </thead>

          <tbody class="divide-y divide-slate-100">
            <tr
              v-for="item in data?.schedules"
              :key="item.id"
              class="transition hover:bg-violet-50"
            >
              <!-- Date -->
              <td class="px-5 py-4 font-semibold text-slate-800">
                {{ item.date }}
              </td>

              <!-- Time -->
              <td class="px-5 py-4">
                <span
                  class="rounded-md bg-slate-100 px-2.5 py-1 font-medium text-slate-700"
                >
                  {{ item.startTime.slice(0, 5) }}
                </span>

                <span class="mx-2 text-slate-400">→</span>

                <span
                  class="rounded-md bg-slate-100 px-2.5 py-1 font-medium text-slate-700"
                >
                  {{ item.endTime.slice(0, 5) }}
                </span>
              </td>

              <!-- Limit -->
              <td class="px-5 py-4 text-center">
                <span
                  class="inline-flex min-w-10 justify-center rounded-lg bg-blue-50 px-3 py-1 font-semibold text-blue-700"
                >
                  {{ item.patientLimit }}
                </span>
              </td>

              <!-- Reason -->
              <td class="max-w-64 px-5 py-4 text-slate-600">
                {{ item.reason || '—' }}
              </td>

              <!-- Status -->
              <td class="px-5 py-4 text-center">
                <span
                  class="rounded-full px-3 py-1 text-xs font-semibold"
                  :class="statusClass(item.status)"
                >
                  {{
                    item.status === 'Pending'
                      ? 'Đang chờ'
                      : item.status === 'Approved'
                        ? 'Đã phê duyệt'
                        : item.status === 'Rejected'
                          ? 'Đã từ chối'
                          : item.status === 'Cancelled'
                            ? 'Đã huỷ'
                            : item.status
                  }}
                </span>
              </td>

              <!-- Actions -->
              <td class="px-5 py-4 text-right">
                <div
                  v-if="item.status === 'Pending'"
                  class="flex justify-end gap-2"
                >
                  <button
                    class="rounded-md border border-blue-200 bg-blue-50 px-3 py-1.5 text-xs font-semibold text-blue-700 transition hover:bg-blue-100 disabled:opacity-50"
                    :disabled="saving"
                    @click="edit(item)"
                  >
                    Sửa
                  </button>

                  <button
                    class="rounded-md border border-red-200 bg-red-50 px-3 py-1.5 text-xs font-semibold text-red-600 transition hover:bg-red-100 disabled:opacity-50"
                    :disabled="saving"
                    @click="cancel(item)"
                  >
                    Huỷ
                  </button>
                </div>

                <span
                  v-else
                  class="text-xs text-slate-400"
                >
                  —
                </span>
              </td>
            </tr>

            <!-- Loading -->
            <tr v-if="loading">
              <td
                colspan="6"
                class="px-5 py-12 text-center text-slate-500"
              >
                Đang tải yêu cầu...
              </td>
            </tr>

            <!-- Empty -->
            <tr v-if="!loading && !data?.schedules.length">
              <td
                colspan="6"
                class="px-5 py-16 text-center"
              >
                <p class="font-medium text-slate-700">
                  Chưa có yêu cầu nào
                </p>

                <p class="mt-1 text-sm text-slate-400">
                  Bạn chưa gửi yêu cầu thay đổi hoặc đề xuất ca làm việc.
                </p>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>
