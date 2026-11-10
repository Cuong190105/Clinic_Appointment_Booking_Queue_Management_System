<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { shiftsApi } from '../shifts.api'
import type { ShiftSuggestion } from '../shifts.types'
import type { Doctor } from '@/features/doctors/doctors.types'
import type { Specialty } from '@/features/specialties/specialties.types'
import { formatSpecialtyName, matchesSpecialty } from '@/features/specialties/specialties.utils'
import BaseAlert from '@/components/ui/BaseAlert.vue'

const props = defineProps<{
  doctors: Doctor[]
  specialties: Specialty[]
}>()

const emit = defineEmits<{
  (e: 'approved'): void
  (e: 'rejected'): void
}>()

// Helper dates
function isoDate(date: Date) {
  return date.toISOString().slice(0, 10)
}

const now = new Date()
const startDefault = new Date(now)
startDefault.setDate(startDefault.getDate() - 7)
const endDefault = new Date(now)
endDefault.setDate(endDefault.getDate() + 20)

const startDate = ref(isoDate(startDefault))
const endDate = ref(isoDate(endDefault))

const selectedDoctorId = ref('all')
const selectedSpecialty = ref('all')
const selectedStatus = ref('all')
const search = ref('')

const suggestions = ref<ShiftSuggestion[]>([])
const loading = ref(false)
const actionLoading = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

async function loadAllSuggestions() {
  if (startDate.value && endDate.value) {
    const s = new Date(startDate.value)
    const e = new Date(endDate.value)
    const maxEnd = new Date(s)
    maxEnd.setMonth(maxEnd.getMonth() + 1)
    if (e > maxEnd) {
      errorMessage.value =
        'Khoảng thời gian tra cứu không được vượt quá 1 tháng theo quy định hệ thống.'
      return
    }
  }

  loading.value = true
  errorMessage.value = null

  try {
    const doctorList = props.doctors.length > 0 ? props.doctors : []
    if (doctorList.length === 0) {
      suggestions.value = []
      return
    }

    // Fetch suggestions for all doctors in parallel
    const results = await Promise.allSettled(
      doctorList.map(async (doc) => {
        try {
          const list = await shiftsApi.getDoctorSuggestions(doc.id, startDate.value, endDate.value)
          return list.map((item) => ({
            ...item,
            doctorName: doc.fullName || doc.name || item.doctorName,
            specialty: doc.specialty || '',
          }))
        } catch {
          return []
        }
      }),
    )

    const aggregated: ShiftSuggestion[] = []
    for (const r of results) {
      if (r.status === 'fulfilled' && Array.isArray(r.value)) {
        aggregated.push(...r.value)
      }
    }

    // Sort: Pending first, then by date descending
    aggregated.sort((a, b) => {
      if (a.status === 'Pending' && b.status !== 'Pending') return -1
      if (a.status !== 'Pending' && b.status === 'Pending') return 1
      return b.date.localeCompare(a.date)
    })

    suggestions.value = aggregated
  } catch (err: any) {
    errorMessage.value = err.message || 'Không thể tải danh sách đề xuất ca trực.'
  } finally {
    loading.value = false
  }
}

watch([startDate, endDate], async () => {
  await loadAllSuggestions()
})

watch(
  () => props.doctors,
  async (newVal) => {
    if (newVal.length > 0) {
      await loadAllSuggestions()
    }
  },
  { immediate: true },
)

onMounted(async () => {
  if (props.doctors.length > 0) {
    await loadAllSuggestions()
  }
})

// Filtered suggestions
const filteredSuggestions = computed(() => {
  const keyword = search.value.trim().toLowerCase()
  const selectedSpecObj = props.specialties.find(
    (s) =>
      s.id === selectedSpecialty.value ||
      s.name.toLowerCase() === selectedSpecialty.value.toLowerCase(),
  )
  const targetSpecName = selectedSpecObj ? selectedSpecObj.name : selectedSpecialty.value

  return suggestions.value.filter((item) => {
    // Search keyword
    const matchesKeyword =
      !keyword ||
      (item.doctorName || '').toLowerCase().includes(keyword) ||
      (item.reason || '').toLowerCase().includes(keyword) ||
      item.date.includes(keyword)

    // Doctor filter
    const matchesDoctor =
      selectedDoctorId.value === 'all' || item.doctorId === selectedDoctorId.value

    // Specialty filter
    const matchesSpec =
      selectedSpecialty.value === 'all' ||
      matchesSpecialty(item.specialty, targetSpecName) ||
      matchesSpecialty(item.specialty, selectedSpecialty.value)

    // Status filter
    const matchesStat = selectedStatus.value === 'all' || item.status === selectedStatus.value

    return matchesKeyword && matchesDoctor && matchesSpec && matchesStat
  })
})

const pendingCount = computed(() => suggestions.value.filter((s) => s.status === 'Pending').length)

// Actions
async function handleApprove(item: ShiftSuggestion) {
  if (
    !confirm(
      `Phê duyệt ca trực ngày ${item.date} (${item.startTime} - ${item.endTime}) của BS. ${item.doctorName}?`,
    )
  ) {
    return
  }

  actionLoading.value = item.id
  errorMessage.value = null
  successMessage.value = null

  try {
    await shiftsApi.approveShiftSuggestion(item.id)
    successMessage.value = `Đã phê duyệt đề xuất ca trực của BS. ${item.doctorName} thành công!`
    await loadAllSuggestions()
    emit('approved')
  } catch (err: any) {
    errorMessage.value =
      err.response?.data?.message || err.message || 'Không thể phê duyệt đề xuất.'
  } finally {
    actionLoading.value = null
  }
}

async function handleReject(item: ShiftSuggestion) {
  if (!confirm(`Từ chối đề xuất ca trực ngày ${item.date} của BS. ${item.doctorName}?`)) {
    return
  }

  actionLoading.value = item.id
  errorMessage.value = null
  successMessage.value = null

  try {
    await shiftsApi.rejectShiftSuggestion(item.id)
    successMessage.value = `Đã từ chối đề xuất ca trực của BS. ${item.doctorName}.`
    await loadAllSuggestions()
    emit('rejected')
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || err.message || 'Không thể từ chối đề xuất.'
  } finally {
    actionLoading.value = null
  }
}

function statusBadgeClass(status: string) {
  const classes: Record<string, string> = {
    Pending: 'bg-amber-50 text-amber-700 border-amber-200',
    Approved: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    Rejected: 'bg-red-50 text-red-700 border-red-200',
    Cancelled: 'bg-slate-100 text-slate-600 border-slate-200',
  }
  return classes[status] ?? 'bg-slate-100 text-slate-600 border-slate-200'
}

function statusLabel(status: string) {
  const labels: Record<string, string> = {
    Pending: 'Chờ duyệt',
    Approved: 'Đã duyệt',
    Rejected: 'Từ chối',
    Cancelled: 'Đã hủy',
  }
  return labels[status] ?? status
}

function resetFilters() {
  search.value = ''
  selectedDoctorId.value = 'all'
  selectedSpecialty.value = 'all'
  selectedStatus.value = 'all'
}

defineExpose({
  pendingCount,
  loadAllSuggestions,
})
</script>

<template>
  <div class="space-y-5">
    <!-- NOTICES -->
    <BaseAlert
      v-if="errorMessage"
      type="error"
      :message="errorMessage"
      dismissible
      @dismiss="errorMessage = null"
    />
    <BaseAlert
      v-if="successMessage"
      type="success"
      :message="successMessage"
      dismissible
      @dismiss="successMessage = null"
    />

    <!-- SUMMARY STATS -->
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-4">
      <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-2xs">
        <p class="text-xs font-semibold tracking-wider text-slate-400 uppercase">Tổng đề xuất</p>
        <p class="mt-1.5 text-2xl font-bold text-slate-800">{{ suggestions.length }}</p>
      </div>
      <div class="rounded-2xl border border-amber-200 bg-amber-50/50 p-4 shadow-2xs">
        <p class="text-xs font-semibold tracking-wider text-amber-600 uppercase">Đang chờ duyệt</p>
        <p class="mt-1.5 text-2xl font-bold text-amber-700">{{ pendingCount }}</p>
      </div>
      <div class="rounded-2xl border border-emerald-200 bg-emerald-50/50 p-4 shadow-2xs">
        <p class="text-xs font-semibold tracking-wider text-emerald-600 uppercase">Đã phê duyệt</p>
        <p class="mt-1.5 text-2xl font-bold text-emerald-700">
          {{ suggestions.filter((s) => s.status === 'Approved').length }}
        </p>
      </div>
      <div class="rounded-2xl border border-red-200 bg-red-50/50 p-4 shadow-2xs">
        <p class="text-xs font-semibold tracking-wider text-red-600 uppercase">Đã từ chối</p>
        <p class="mt-1.5 text-2xl font-bold text-red-700">
          {{ suggestions.filter((s) => s.status === 'Rejected').length }}
        </p>
      </div>
    </div>

    <!-- TOOLBAR -->
    <div class="space-y-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-2xs sm:p-5">
      <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <!-- SEARCH -->
        <div class="relative lg:col-span-2">
          <input
            v-model="search"
            type="text"
            placeholder="Tìm theo tên bác sĩ, lý do..."
            class="w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 pl-10 text-sm text-slate-800 placeholder:text-slate-400 focus:border-[#0E4D92] focus:outline-none"
          />
          <svg
            class="absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-slate-400"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
          >
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.5-3.5" />
          </svg>
        </div>

        <!-- DOCTOR FILTER -->
        <select
          v-model="selectedDoctorId"
          class="rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm font-medium text-slate-700 focus:border-[#0E4D92] focus:outline-none"
        >
          <option value="all">Tất cả bác sĩ</option>
          <option v-for="d in doctors" :key="d.id" :value="d.id">
            {{ d.fullName || d.name }}
          </option>
        </select>

        <!-- SPECIALTY FILTER -->
        <select
          v-model="selectedSpecialty"
          class="rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm font-medium text-slate-700 focus:border-[#0E4D92] focus:outline-none"
        >
          <option value="all">Tất cả chuyên khoa</option>
          <option v-for="sp in specialties" :key="sp.id" :value="sp.id">
            {{ formatSpecialtyName(sp.name) }}
          </option>
        </select>

        <!-- STATUS FILTER -->
        <select
          v-model="selectedStatus"
          class="rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm font-medium text-slate-700 focus:border-[#0E4D92] focus:outline-none"
        >
          <option value="all">Tất cả trạng thái</option>
          <option value="Pending">Chờ duyệt (Pending)</option>
          <option value="Approved">Đã duyệt (Approved)</option>
          <option value="Rejected">Đã từ chối (Rejected)</option>
          <option value="Cancelled">Đã hủy (Cancelled)</option>
        </select>
      </div>

      <!-- DATE RANGE & REFRESH -->
      <div class="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-2">
        <div class="flex flex-wrap items-center gap-2 text-xs font-medium text-slate-600">
          <span>Khoảng ngày:</span>
          <input
            v-model="startDate"
            type="date"
            class="rounded-lg border border-slate-200 px-2.5 py-1.5 text-xs text-slate-700 outline-none focus:border-[#0E4D92]"
          />
          <span>→</span>
          <input
            v-model="endDate"
            type="date"
            class="rounded-lg border border-slate-200 px-2.5 py-1.5 text-xs text-slate-700 outline-none focus:border-[#0E4D92]"
          />
          <button
            type="button"
            class="rounded-lg bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-700 transition-colors hover:bg-slate-200"
            @click="loadAllSuggestions"
          >
            Lọc ngày
          </button>
        </div>

        <div class="flex items-center gap-2">
          <button
            type="button"
            class="rounded-xl border border-slate-200 px-3.5 py-1.5 text-xs font-medium text-slate-600 transition-colors hover:bg-slate-50"
            @click="resetFilters"
          >
            Đặt lại bộ lọc
          </button>
          <button
            type="button"
            class="rounded-xl bg-[#0E4D92] px-3.5 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-[#0c407a] disabled:opacity-50"
            :disabled="loading"
            @click="loadAllSuggestions"
          >
            {{ loading ? 'Đang tải...' : 'Làm mới' }}
          </button>
        </div>
      </div>
    </div>

    <!-- TABLE -->
    <div class="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xs">
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-slate-200 bg-slate-50 text-slate-600">
              <th class="px-4 py-3.5 text-left text-xs font-bold tracking-wider uppercase">
                Bác sĩ & Chuyên khoa
              </th>
              <th class="px-4 py-3.5 text-left text-xs font-bold tracking-wider uppercase">
                Ngày ca trực
              </th>
              <th class="px-4 py-3.5 text-left text-xs font-bold tracking-wider uppercase">
                Thời gian
              </th>
              <th class="px-4 py-3.5 text-center text-xs font-bold tracking-wider uppercase">
                Giới hạn BN
              </th>
              <th class="px-4 py-3.5 text-left text-xs font-bold tracking-wider uppercase">
                Lý do đề xuất
              </th>
              <th class="px-4 py-3.5 text-center text-xs font-bold tracking-wider uppercase">
                Trạng thái
              </th>
              <th class="px-4 py-3.5 text-right text-xs font-bold tracking-wider uppercase">
                Thao tác
              </th>
            </tr>
          </thead>

          <tbody class="divide-y divide-slate-100">
            <tr
              v-for="item in filteredSuggestions"
              :key="item.id"
              class="transition-colors hover:bg-slate-50/70"
            >
              <!-- Doctor -->
              <td class="px-4 py-3.5">
                <div class="font-bold text-slate-800">{{ item.doctorName }}</div>
                <div class="mt-0.5 text-xs font-medium text-slate-500">
                  {{ formatSpecialtyName(item.specialty) || 'Bác sĩ' }}
                </div>
              </td>

              <!-- Date -->
              <td class="px-4 py-3.5 font-medium text-slate-700">
                {{ item.date }}
              </td>

              <!-- Time -->
              <td class="px-4 py-3.5 font-mono text-xs text-slate-700">
                <span
                  class="inline-flex items-center rounded-md bg-slate-100 px-2 py-0.5 font-semibold text-slate-700"
                >
                  {{ item.startTime }} - {{ item.endTime }}
                </span>
              </td>

              <!-- Limit -->
              <td class="px-4 py-3.5 text-center">
                <span
                  class="inline-flex items-center justify-center rounded-lg bg-blue-50 px-2.5 py-1 text-xs font-bold text-[#0E4D92]"
                >
                  {{ item.RecommendedCapacity ?? (item as any).recommendedCapacity ?? 20 }} BN
                </span>
              </td>

              <!-- Reason -->
              <td
                class="max-w-xs truncate px-4 py-3.5 text-xs text-slate-600"
                :title="item.reason || ''"
              >
                {{ item.reason || '—' }}
              </td>

              <!-- Status -->
              <td class="px-4 py-3.5 text-center">
                <span
                  class="inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-semibold"
                  :class="statusBadgeClass(item.status)"
                >
                  {{ statusLabel(item.status) }}
                </span>
              </td>

              <!-- Actions -->
              <td class="px-4 py-3.5 text-right whitespace-nowrap">
                <div v-if="item.status === 'Pending'" class="flex items-center justify-end gap-2">
                  <button
                    type="button"
                    class="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-2.5 py-1.5 text-xs font-semibold text-white shadow-2xs transition-colors hover:bg-emerald-700 disabled:opacity-50"
                    :disabled="actionLoading === item.id"
                    @click="handleApprove(item)"
                  >
                    <span>✓</span>
                    <span>Phê duyệt</span>
                  </button>
                  <button
                    type="button"
                    class="inline-flex items-center gap-1 rounded-lg border border-red-200 bg-red-50 px-2.5 py-1.5 text-xs font-semibold text-red-700 transition-colors hover:bg-red-100 disabled:opacity-50"
                    :disabled="actionLoading === item.id"
                    @click="handleReject(item)"
                  >
                    <span>✕</span>
                    <span>Từ chối</span>
                  </button>
                </div>
                <span v-else class="text-xs text-slate-400 italic"> Đã xử lý </span>
              </td>
            </tr>

            <!-- Empty -->
            <tr v-if="filteredSuggestions.length === 0 && !loading">
              <td colspan="7" class="py-12 text-center text-slate-400">
                <div class="mb-2 text-3xl">📋</div>
                <div class="font-medium text-slate-600">Không tìm thấy đề xuất ca làm việc nào</div>
                <div class="mt-1 text-xs text-slate-400">
                  Các đề xuất đăng ký ca trực của bác sĩ sẽ xuất hiện tại đây
                </div>
              </td>
            </tr>

            <!-- Loading -->
            <tr v-if="loading">
              <td colspan="7" class="py-12 text-center text-slate-500">
                <span class="mb-1 inline-block animate-spin text-2xl">🌀</span>
                <div class="text-xs font-semibold text-violet-600">
                  Đang tải danh sách đề xuất ca trực...
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
