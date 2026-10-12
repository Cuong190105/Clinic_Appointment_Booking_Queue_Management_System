<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ApiError } from '@/lib/api/http'
import { doctorsApi } from '@/features/doctors/doctors.api'
import type { DoctorSchedule, WorkSchedule } from '@/features/doctors/doctors.types'

function isoDate(date: Date) { return date.toISOString().slice(0, 10) }
const today = new Date()
const startDate = ref(isoDate(today))
const end = new Date(today); end.setDate(end.getDate() + 13)
const endDate = ref(isoDate(end))
const data = ref<DoctorSchedule<WorkSchedule> | null>(null)
const loading = ref(false)
const error = ref('')

const activeSchedules = computed(() => data.value?.schedules.filter((s) => s.status === 'Active') ?? [])
function msg(e: unknown) { return e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Không thể tải lịch.' }
function formatDate(v: string) { return new Intl.DateTimeFormat('vi-VN', { weekday: 'short', day: '2-digit', month: '2-digit' }).format(new Date(`${v}T00:00:00`)) }
async function load() { loading.value = true; error.value = ''; try { const res = await doctorsApi.getOwnSchedule(startDate.value, endDate.value); data.value = (res as any)?.result ?? res } catch (e) { error.value = msg(e) } finally { loading.value = false } }
onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div
      class="flex flex-col justify-between gap-4 rounded-2xl border border-violet-100 bg-white p-5 shadow-sm sm:flex-row sm:items-end"
    >
      <div>
        <p class="text-sm font-semibold text-violet-600">Bác sĩ</p>

        <h1 class="mt-1 text-2xl font-bold text-slate-900">
          Lịch làm việc
        </h1>

        <p class="mt-1 text-sm text-slate-500">
          Lịch làm việc đã được phòng khám phê duyệt.
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
          class="rounded-lg bg-violet-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-violet-700 disabled:cursor-not-allowed disabled:opacity-50"
          :disabled="loading"
          @click="load"
        >
          {{ loading ? 'Đang tải...' : 'Xem lịch' }}
        </button>
      </div>
    </div>

    <!-- Error -->
    <div
      v-if="error"
      class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm font-medium text-red-700"
    >
      {{ error }}
    </div>

    <!-- Stats -->
    <div class="grid gap-4 sm:grid-cols-3">
      <!-- Total -->
      <div
        class="rounded-2xl border border-blue-100 bg-blue-50 p-5 shadow-sm"
      >
        <p class="text-sm font-medium text-blue-600">
          Tổng ca làm việc
        </p>

        <p class="mt-2 text-3xl font-bold text-blue-900">
          {{ data?.schedules.length ?? 0 }}
        </p>
      </div>

      <!-- Active -->
      <div
        class="rounded-2xl border border-emerald-100 bg-emerald-50 p-5 shadow-sm"
      >
        <p class="text-sm font-medium text-emerald-600">
          Ca đang hoạt động
        </p>

        <p class="mt-2 text-3xl font-bold text-emerald-700">
          {{ activeSchedules.length }}
        </p>
      </div>

      <!-- Doctor -->
      <div
        class="rounded-2xl border border-violet-100 bg-violet-50 p-5 shadow-sm"
      >
        <p class="text-sm font-medium text-violet-600">
          Bác sĩ
        </p>

        <p class="mt-2 truncate text-lg font-bold text-violet-900">
          {{ data?.doctorName || '—' }}
        </p>
      </div>
    </div>

    <!-- Schedule Table -->
    <section
      class="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm"
    >
      <div
        class="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-5 py-4"
      >
        <div>
          <h2 class="font-semibold text-slate-900">
            Danh sách ca khám
          </h2>

          <p class="mt-1 text-sm text-slate-500">
            Các ca làm việc trong khoảng thời gian đã chọn.
          </p>
        </div>

        <span
          class="rounded-full bg-violet-100 px-3 py-1 text-xs font-semibold text-violet-700"
        >
          {{ data?.schedules.length ?? 0 }} ca
        </span>
      </div>

      <div class="overflow-x-auto">
        <table class="min-w-full text-sm">
          <thead
            class="bg-slate-100 text-left text-xs font-semibold uppercase tracking-wide text-slate-600"
          >
            <tr>
              <th class="px-5 py-3">Ngày</th>
              <th class="px-5 py-3">Thời gian</th>
              <th class="px-5 py-3 text-center">
                Số bệnh nhân
              </th>
              <th class="px-5 py-3 text-center">
                Trạng thái
              </th>
            </tr>
          </thead>

          <tbody class="divide-y divide-slate-100">
            <tr
              v-for="shift in data?.schedules"
              :key="shift.id"
              class="transition hover:bg-violet-50"
            >
              <td class="px-5 py-4 font-semibold text-slate-800">
                {{ formatDate(shift.date) }}
              </td>

              <td class="px-5 py-4 font-medium text-slate-700">
                <span
                  class="rounded-md bg-slate-100 px-2.5 py-1 text-slate-700"
                >
                  {{ shift.startTime.slice(0, 5) }}
                </span>

                <span class="mx-2 text-slate-400">→</span>

                <span
                  class="rounded-md bg-slate-100 px-2.5 py-1 text-slate-700"
                >
                  {{ shift.endTime.slice(0, 5) }}
                </span>
              </td>

              <td class="px-5 py-4 text-center">
                <span
                  class="inline-flex min-w-10 justify-center rounded-lg bg-blue-50 px-3 py-1 font-semibold text-blue-700"
                >
                  {{ shift.patientLimit }}
                </span>
              </td>

              <td class="px-5 py-4 text-center">
                <span
                  class="inline-flex rounded-full px-3 py-1 text-xs font-semibold"
                  :class="
                    shift.status === 'Active'
                      ? 'bg-emerald-100 text-emerald-700'
                      : 'bg-slate-100 text-slate-600'
                  "
                >
                  {{
                    shift.status === 'Active'
                      ? 'Đang hoạt động'
                      : shift.status
                  }}
                </span>
              </td>
            </tr>

            <!-- Loading -->
            <tr v-if="loading">
              <td
                colspan="4"
                class="px-5 py-12 text-center text-slate-500"
              >
                Đang tải lịch làm việc...
              </td>
            </tr>

            <!-- Empty -->
            <tr
              v-if="!loading && !data?.schedules.length"
            >
              <td
                colspan="4"
                class="px-5 py-16 text-center"
              >
                <p class="font-medium text-slate-700">
                  Chưa có ca làm việc
                </p>

                <p class="mt-1 text-sm text-slate-400">
                  Không có ca trong khoảng thời gian bạn đã chọn.
                </p>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>
