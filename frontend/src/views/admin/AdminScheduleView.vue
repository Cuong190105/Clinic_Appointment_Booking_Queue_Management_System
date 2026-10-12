<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import { formatSpecialtyName } from '@/features/specialties/specialties.utils'
import { shiftsApi } from '@/features/shifts/shifts.api'
import CreateShiftModal from '@/features/shifts/components/CreateShiftModal.vue'
import DoctorScheduleDetailModal from '@/features/shifts/components/DoctorScheduleDetailModal.vue'
import ShiftSuggestionsApproval from '@/features/shifts/components/ShiftSuggestionsApproval.vue'
import AdminPagination from '@/features/admin/components/AdminPagination.vue'
import type { Doctor } from '@/features/doctors/doctors.types'
import type { Specialty } from '@/features/specialties/specialties.types'
import type { DoctorSchedulePageItem, WorkSchedule } from '@/features/shifts/shifts.types'

// ==========================================
// TABS
// ==========================================
const activeTab = ref<'schedule' | 'suggestions'>('schedule')
const suggestionsApprovalRef = ref<InstanceType<typeof ShiftSuggestionsApproval> | null>(null)

// ==========================================
// WEEK CALCULATION HELPERS
// ==========================================

// Lấy ngày Thứ 2 của tuần chứa date hiện tại
function getMonday(d: Date): Date {
  const date = new Date(d)
  const day = date.getDay()
  const diff = date.getDate() - day + (day === 0 ? -6 : 1) // điều chỉnh cho Chủ nhật (0)
  date.setDate(diff)
  date.setHours(0, 0, 0, 0)
  return date
}

function formatDateIso(d: Date): string {
  const yyyy = d.getFullYear()
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}

function formatDateVn(d: Date): string {
  const dd = String(d.getDate()).padStart(2, '0')
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const yyyy = d.getFullYear()
  return `${dd}/${mm}/${yyyy}`
}

// Current week start (Monday)
const currentMonday = ref<Date>(getMonday(new Date()))

// 7 days of current week (Monday -> Sunday)
const weekDays = computed(() => {
  const days: { name: string; date: string; displayDate: string; fullDate: Date }[] = []
  const dayNames = ['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'Chủ nhật']

  for (let i = 0; i < 7; i++) {
    const d = new Date(currentMonday.value)
    d.setDate(d.getDate() + i)
    days.push({
      name: dayNames[i] ?? `Ngày ${i + 1}`,
      date: formatDateIso(d),
      displayDate: `${d.getDate()}/${d.getMonth() + 1}`,
      fullDate: d,
    })
  }
  return days
})

const weekStartDate = computed(() =>
  weekDays.value.length > 0 && weekDays.value[0] ? weekDays.value[0].date : '',
)
const weekEndDate = computed(() =>
  weekDays.value.length > 6 && weekDays.value[6] ? weekDays.value[6].date : '',
)
const selectedWeekLabel = computed(() => {
  const first = weekDays.value[0]
  const last = weekDays.value[6]
  if (!first || !last) return ''
  return `${formatDateVn(first.fullDate)} - ${formatDateVn(last.fullDate)}`
})

// ==========================================
// STATE
// ==========================================

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

const scheduleRows = ref<DoctorSchedulePageItem[]>([])
const specialties = ref<Specialty[]>([])

const search = ref('')
const selectedSpecialty = ref('all')
const currentPage = ref(1)
const pageSize = ref(10)
const totalPages = ref(1)
const totalCount = ref(0)

const doctors = computed<Doctor[]>(() =>
  scheduleRows.value.map((row) => ({
    id: row.doctorId,
    fullName: row.doctorName,
    name: row.doctorName,
    phoneNumber: '',
    email: '',
    gender: 0,
    specialty: row.specialtyName,
    specialtyId: row.specialtyId,
    licenseNumber: '',
    qualification: '',
    experienceYears: 0,
    biography: '',
    status: 'Active',
  })),
)

// Modals
const showCreateModal = ref(false)
const showDetailModal = ref(false)
const selectedDoctorForDetail = ref<Doctor | null>(null)
const defaultDateForCreate = ref<string | undefined>(undefined)
const defaultDoctorIdForCreate = ref<string | undefined>(undefined)

// ==========================================
// DATA LOADING
// ==========================================

async function loadInitialData() {
  await loadWeekSlots()
}

async function loadWeekSlots() {
  if (!weekStartDate.value || !weekEndDate.value) return
  loading.value = true
  errorMessage.value = null
  try {
    const result = await shiftsApi.getAllSlots(
      weekStartDate.value,
      weekEndDate.value,
      currentPage.value,
      pageSize.value,
      selectedSpecialty.value,
      search.value,
    )
    if (result.totalPages > 0 && currentPage.value > result.totalPages) {
      currentPage.value = result.totalPages
      return
    }
    scheduleRows.value = result.items
    totalPages.value = result.totalPages
    totalCount.value = result.totalCount

    const specialtyMap = new Map(specialties.value.map((specialty) => [specialty.id, specialty]))
    result.items.forEach((row) => {
      if (row.specialtyId && !specialtyMap.has(row.specialtyId)) {
        specialtyMap.set(row.specialtyId, {
          id: row.specialtyId,
          name: row.specialtyName,
          establishedDate: '',
        })
      }
    })
    specialties.value = Array.from(specialtyMap.values())
  } catch (err: any) {
    scheduleRows.value = []
    totalPages.value = 1
    totalCount.value = 0
    errorMessage.value = err.message || 'Không thể tải lịch làm việc trong tuần.'
  } finally {
    loading.value = false
  }
}

// Watch week changes to reload slots
watch([weekStartDate, weekEndDate], async () => {
  currentPage.value = 1
  await loadWeekSlots()
})

watch([search, selectedSpecialty], async () => {
  if (currentPage.value !== 1) {
    currentPage.value = 1
    return
  }
  await loadWeekSlots()
})

watch([currentPage, pageSize], async () => {
  await loadWeekSlots()
})

onMounted(async () => {
  await loadInitialData()
})

// ==========================================
// COMPUTED ROWS
// ==========================================

interface DoctorRowView {
  doctor: Doctor
  // 7 days array
  days: {
    date: string
    shifts: WorkSchedule[]
  }[]
}

const filteredDoctorRows = computed<DoctorRowView[]>(() => {
  // Search and specialty filtering are performed by the paginated endpoint.
  return doctors.value.map((doc) => {
    const docShifts = scheduleRows.value.find((row) => row.doctorId === doc.id)?.schedules || []

    const days = weekDays.value.map((day) => {
      const shiftsOnDay = docShifts.filter((s) => {
        const sDate = s.date.split('T')[0]
        return sDate === day.date
      })
      return {
        date: day.date,
        shifts: shiftsOnDay,
      }
    })

    return {
      doctor: doc,
      days,
    }
  })
})

// ==========================================
// ACTIONS
// ==========================================

function previousWeek() {
  const prev = new Date(currentMonday.value)
  prev.setDate(prev.getDate() - 7)
  currentMonday.value = prev
}

function nextWeek() {
  const next = new Date(currentMonday.value)
  next.setDate(next.getDate() + 7)
  currentMonday.value = next
}

function currentWeekNow() {
  currentMonday.value = getMonday(new Date())
}

function resetFilters() {
  search.value = ''
  selectedSpecialty.value = 'all'
}

function openCreateModal(doctorId?: string, date?: string) {
  defaultDoctorIdForCreate.value = doctorId
  defaultDateForCreate.value = date || weekStartDate.value
  showCreateModal.value = true
}

function openDoctorDetail(doc: Doctor) {
  selectedDoctorForDetail.value = doc
  showDetailModal.value = true
}

async function onShiftCreated() {
  successMessage.value = 'Tạo ca làm việc cho bác sĩ thành công!'
  setTimeout(() => {
    successMessage.value = null
  }, 4000)
  await loadWeekSlots()
}

async function onShiftUpdated() {
  await loadWeekSlots()
}

async function onSuggestionApproved() {
  await loadWeekSlots()
}

async function onSuggestionRejected() {
  await loadWeekSlots()
}
</script>

<template>
  <div class="space-y-5">
    <!-- TITLE & TOP ACTION -->
    <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-xl font-bold text-slate-800">Lịch làm việc & Đề xuất ca trực</h1>
        <p class="mt-1 text-xs text-slate-500 sm:text-sm">
          Quản lý ca trực, phân công lịch khám theo tuần và duyệt đề xuất từ bác sĩ
        </p>
      </div>

      <div class="flex items-center gap-3">
        <BaseButton v-if="activeTab === 'schedule'" variant="primary" @click="openCreateModal()">
          <span>➕</span>
          <span>Thêm ca làm việc</span>
        </BaseButton>
      </div>
    </div>

    <!-- NAVIGATION TABS -->
    <div
      class="flex items-center gap-2 rounded-t-2xl border-b border-slate-200 bg-white px-2 pt-2 shadow-2xs"
    >
      <button
        type="button"
        class="flex cursor-pointer items-center gap-2 border-b-2 px-5 py-3 text-sm font-semibold transition-colors"
        :class="
          activeTab === 'schedule'
            ? 'border-[#0E4D92] text-[#0E4D92]'
            : 'border-transparent text-slate-500 hover:text-slate-700'
        "
        @click="activeTab = 'schedule'"
      >
        <span>📅</span>
        <span>Lịch làm việc theo tuần</span>
      </button>

      <button
        type="button"
        class="flex cursor-pointer items-center gap-2 border-b-2 px-5 py-3 text-sm font-semibold transition-colors"
        :class="
          activeTab === 'suggestions'
            ? 'border-[#0E4D92] text-[#0E4D92]'
            : 'border-transparent text-slate-500 hover:text-slate-700'
        "
        @click="activeTab = 'suggestions'"
      >
        <span>📝</span>
        <span>Duyệt đề xuất ca trực</span>
      </button>
    </div>

    <!-- TAB 1: SCHEDULE GRID -->
    <div v-if="activeTab === 'schedule'" class="space-y-5">
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

      <!-- TOOLBAR -->
      <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-2xs sm:p-5">
        <div class="flex flex-col gap-3 lg:flex-row lg:items-center">
          <!-- SEARCH -->
          <div class="relative flex-1">
            <input
              v-model="search"
              type="text"
              placeholder="Tìm kiếm theo tên bác sĩ hoặc chuyên khoa..."
              class="w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 pl-10 text-sm text-slate-800 placeholder:text-slate-400 focus:border-[#0E4D92] focus:ring-1 focus:ring-[#0E4D92] focus:outline-none"
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

          <!-- SPECIALTY -->
          <select
            v-model="selectedSpecialty"
            class="rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm font-medium text-slate-700 focus:border-[#0E4D92] focus:outline-none"
          >
            <option value="all">Tất cả chuyên khoa</option>
            <option v-for="sp in specialties" :key="sp.id" :value="sp.id">
              {{ formatSpecialtyName(sp.name) }}
            </option>
          </select>

          <!-- RESET BUTTON -->
          <button
            type="button"
            class="cursor-pointer rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50"
            @click="resetFilters"
          >
            Đặt lại lọc
          </button>
        </div>
      </div>

      <!-- WEEK NAVIGATION -->
      <div
        class="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-slate-200 bg-white px-5 py-4 shadow-2xs"
      >
        <div class="flex items-center gap-2">
          <button
            type="button"
            class="cursor-pointer rounded-xl border border-slate-200 px-3.5 py-2 text-xs font-semibold text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50 sm:text-sm"
            @click="previousWeek"
          >
            ← Tuần trước
          </button>
          <button
            type="button"
            class="cursor-pointer rounded-xl border border-slate-200 px-3 py-2 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-50 sm:text-sm"
            @click="currentWeekNow"
          >
            Tuần này
          </button>
        </div>

        <div class="text-center">
          <div class="text-xs font-bold tracking-wider text-slate-400 uppercase">
            Khoảng thời gian
          </div>
          <div class="text-sm font-bold text-slate-800">
            {{ selectedWeekLabel }}
          </div>
        </div>

        <div class="flex items-center gap-2">
          <button
            type="button"
            class="cursor-pointer rounded-xl border border-slate-200 px-3.5 py-2 text-xs font-semibold text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50 sm:text-sm"
            @click="nextWeek"
          >
            Tuần sau →
          </button>
        </div>
      </div>

      <!-- MAIN SCHEDULE MATRIX -->
      <div class="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xs">
        <div class="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <div>
            <h2 class="text-base font-bold text-slate-800">Bảng phân bổ ca trực</h2>
            <p class="mt-0.5 text-xs text-slate-500">
              Hiển thị {{ filteredDoctorRows.length }} bác sĩ
            </p>
          </div>

          <div v-if="loading" class="flex items-center gap-2 text-xs font-semibold text-violet-600">
            <span class="animate-spin">🌀</span>
            <span>Đang tải dữ liệu...</span>
          </div>
        </div>

        <div
          class="flex flex-col gap-3 border-b border-slate-200 px-5 py-3 sm:flex-row sm:items-center sm:justify-between"
        >
          <div class="flex items-center gap-2 text-xs font-medium text-slate-600">
            <label for="schedule-page-size">Số bác sĩ mỗi trang</label>
            <select
              id="schedule-page-size"
              v-model.number="pageSize"
              class="rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs font-semibold text-slate-700 focus:border-[#0E4D92] focus:outline-none"
            >
              <option :value="10">10</option>
              <option :value="25">25</option>
              <option :value="50">50</option>
              <option :value="100">100</option>
            </select>
          </div>

          <AdminPagination
            v-model:current-page="currentPage"
            :total-pages="totalPages"
            :total-count="totalCount"
          />
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[1050px] text-sm">
            <!-- HEADER -->
            <thead>
              <tr class="border-b border-slate-200 bg-slate-50 text-slate-600">
                <th class="w-60 px-5 py-4 text-left text-xs font-bold tracking-wider uppercase">
                  Bác sĩ & Chuyên khoa
                </th>

                <th
                  v-for="day in weekDays"
                  :key="day.date"
                  class="px-2 py-4 text-center text-xs font-bold tracking-wider uppercase"
                >
                  <div>{{ day.name }}</div>
                  <div class="mt-0.5 text-[11px] font-normal text-slate-400">
                    {{ day.displayDate }}
                  </div>
                </th>

                <th class="w-28 px-4 py-4 text-right text-xs font-bold tracking-wider uppercase">
                  Thao tác
                </th>
              </tr>
            </thead>

            <!-- BODY -->
            <tbody class="divide-y divide-slate-100">
              <tr
                v-for="row in filteredDoctorRows"
                :key="row.doctor.id"
                class="transition-colors hover:bg-slate-50/60"
              >
                <!-- DOCTOR INFO -->
                <td class="px-5 py-4">
                  <div class="text-sm font-bold text-slate-800">
                    {{ row.doctor.fullName || row.doctor.name }}
                  </div>
                  <div class="mt-0.5 text-xs font-medium text-slate-500">
                    {{ formatSpecialtyName(row.doctor.specialty) || 'Chưa phân khoa' }}
                  </div>
                </td>

                <!-- 7 DAYS SLOTS -->
                <td
                  v-for="(dayItem, dIndex) in row.days"
                  :key="`${row.doctor.id}-${dIndex}`"
                  class="px-1.5 py-3 text-center align-top"
                >
                  <div class="space-y-1.5">
                    <div
                      v-for="s in dayItem.shifts.slice(0, 4)"
                      :key="s.id"
                      class="cursor-pointer rounded-lg border border-blue-200 bg-blue-50/80 px-2 py-1.5 text-left text-xs font-bold text-[#0E4D92] shadow-2xs transition-colors hover:bg-blue-100/80"
                      title="Bấm để xem chi tiết lịch của bác sĩ"
                      @click="openDoctorDetail(row.doctor)"
                    >
                      <div class="flex items-center justify-between">
                        <span>🕒 {{ s.startTime }} - {{ s.endTime }}</span>
                      </div>
                      <div class="mt-0.5 text-[10px] font-normal text-slate-500">
                        Tối đa: {{ s.patientLimit }} BN
                      </div>
                    </div>
                    <div
                      v-if="dayItem.shifts.length < 4"
                      class="flex min-h-[36px] items-center justify-center"
                    >
                      <button
                        type="button"
                        class="flex h-8 w-8 cursor-pointer items-center justify-center rounded-lg border border-dashed border-slate-200 text-xs font-bold text-slate-300 transition-colors hover:border-[#0E4D92] hover:bg-blue-50/50 hover:text-[#0E4D92]"
                        title="Thêm ca trực ngày này"
                        @click="openCreateModal(row.doctor.id, dayItem.date)"
                      >
                        +
                      </button>
                    </div>
                  </div>
                </td>

                <!-- ACTIONS -->
                <td class="px-4 py-4 text-right align-middle">
                  <button
                    type="button"
                    class="cursor-pointer rounded-lg border border-slate-200 px-2.5 py-1.5 text-xs font-semibold text-[#0E4D92] transition-colors hover:border-[#0E4D92] hover:bg-slate-50"
                    @click="openDoctorDetail(row.doctor)"
                  >
                    Chi tiết
                  </button>
                </td>
              </tr>

              <!-- EMPTY STATE -->
              <tr v-if="filteredDoctorRows.length === 0">
                <td colspan="9" class="px-5 py-12 text-center text-sm text-slate-400">
                  Không tìm thấy bác sĩ hoặc lịch làm việc phù hợp với bộ lọc.
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- LEGEND -->
      <div
        class="flex flex-wrap items-center gap-6 rounded-2xl border border-slate-200 bg-white px-5 py-4 shadow-2xs"
      >
        <span class="text-xs font-bold tracking-wider text-slate-700 uppercase"> Hướng dẫn: </span>
        <div class="flex items-center gap-2">
          <span class="h-3.5 w-3.5 rounded border border-blue-300 bg-blue-100" />
          <span class="text-xs font-medium text-slate-600">Ca trực đã được phân công</span>
        </div>
        <div class="flex items-center gap-2">
          <span class="h-3.5 w-3.5 rounded border border-dashed border-slate-300 bg-slate-50" />
          <span class="text-xs font-medium text-slate-600"
            >Ô trống (Bấm vào ô để tạo nhanh ca trực cho ngày đó)</span
          >
        </div>
      </div>
    </div>

    <!-- TAB 2: SUGGESTIONS APPROVAL -->
    <div v-else-if="activeTab === 'suggestions'">
      <ShiftSuggestionsApproval
        ref="suggestionsApprovalRef"
        :doctors="doctors"
        :specialties="specialties"
        @approved="onSuggestionApproved"
        @rejected="onSuggestionRejected"
      />
    </div>

    <!-- MODAL TẠO CA TRỰC -->
    <CreateShiftModal
      :show="showCreateModal"
      :doctors="doctors"
      :default-doctor-id="defaultDoctorIdForCreate"
      :default-date="defaultDateForCreate"
      @close="showCreateModal = false"
      @saved="onShiftCreated"
    />

    <!-- MODAL XEM CHI TIẾT LỊCH BÁC SĨ -->
    <DoctorScheduleDetailModal
      :show="showDetailModal"
      :doctor="selectedDoctorForDetail"
      :start-date="weekStartDate"
      :end-date="weekEndDate"
      @close="showDetailModal = false"
      @updated="onShiftUpdated"
    />
  </div>
</template>
