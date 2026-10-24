<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseInput from '@/components/ui/BaseInput.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseSpinner from '@/components/ui/BaseSpinner.vue'
import BaseCheckbox from '@/components/ui/BaseCheckbox.vue'
import PatientSlotPicker from '@/features/patients/components/PatientSlotPicker.vue'
import { receptionistApi } from '@/features/receptionist/receptionist.api'
import { specialtiesApi } from '@/features/specialties/specialties.api'
import { doctorsApi } from '@/features/doctors/doctors.api'
import { usePatientStore } from '@/stores/patient'
import { formatSpecialtyName, getSpecialtyDisplay, matchesSpecialty } from '@/features/specialties/specialties.utils'
import type {
  AvailableSlot,
} from '@/features/patients/patient.types'
import type {
  CreatePatientPayload,
  PatientSearchItem,
  WalkInResult,
  WorkingDoctor,
} from '@/features/receptionist/receptionist.types'

const router = useRouter()
const patientStore = usePatientStore()

// Step state: 1 -> 2 -> 3 -> 4
const currentStep = ref(1)

// ==========================================
// STEP 1: CHỌN HOẶC TẠO HỒ SƠ BỆNH NHÂN
// ==========================================
const patientMode = ref<'search' | 'create'>('search')

// Search existing patients
const searchKeyword = ref('')
const searchingPatients = ref(false)
const searchedPatients = ref<PatientSearchItem[]>([])
const patientPageNumber = ref(1)
const patientTotalCount = ref(0)
const selectedPatient = ref<PatientSearchItem | null>(null)

// Create patient form
const creatingPatient = ref(false)
const newPatientForm = ref<CreatePatientPayload>({
  fullName: '',
  phoneNumber: '',
  email: '',
  dateOfBirth: '',
  gender: 0, // 0: Nam, 1: Nữ, 2: Khác
  address: '',
  insuranceNumber: '',
  emergencyContact: '',
})

// Error & Success messages
const errorMessage = ref('')
const successMessage = ref('')

// Load existing patients
async function handleSearchPatients() {
  searchingPatients.value = true
  errorMessage.value = ''
  try {
    const res = await receptionistApi.searchPatients(searchKeyword.value.trim(), patientPageNumber.value, 10)
    searchedPatients.value = res.items || []
    patientTotalCount.value = res.totalCount || 0
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || err?.message || 'Không thể tìm kiếm bệnh nhân.'
  } finally {
    searchingPatients.value = false
  }
}

function handleSelectPatient(patient: PatientSearchItem) {
  selectedPatient.value = patient
  errorMessage.value = ''
  // Chuyển sang bước 2 để review thông tin
  currentStep.value = 2
}

async function handleCreatePatientSubmit() {
  if (!newPatientForm.value.fullName.trim()) {
    errorMessage.value = 'Vui lòng nhập họ và tên bệnh nhân.'
    return
  }
  if (!newPatientForm.value.dateOfBirth) {
    errorMessage.value = 'Vui lòng chọn ngày tháng năm sinh.'
    return
  }
  if (!newPatientForm.value.address.trim()) {
    errorMessage.value = 'Vui lòng nhập địa chỉ bệnh nhân.'
    return
  }

  creatingPatient.value = true
  errorMessage.value = ''
  try {
    const created = await receptionistApi.createPatient({
      fullName: newPatientForm.value.fullName.trim(),
      phoneNumber: newPatientForm.value.phoneNumber?.trim() || undefined,
      email: newPatientForm.value.email?.trim() || undefined,
      dateOfBirth: newPatientForm.value.dateOfBirth,
      gender: Number(newPatientForm.value.gender),
      address: newPatientForm.value.address.trim(),
      insuranceNumber: newPatientForm.value.insuranceNumber?.trim() || undefined,
      emergencyContact: newPatientForm.value.emergencyContact?.trim() || undefined,
    })

    selectedPatient.value = created
    successMessage.value = 'Tạo hồ sơ bệnh nhân mới thành công!'
    setTimeout(() => {
      successMessage.value = ''
    }, 3000)

    // Chuyển sang bước 2 để review
    currentStep.value = 2
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || err?.message || 'Lỗi khi tạo hồ sơ bệnh nhân.'
  } finally {
    creatingPatient.value = false
  }
}

// ==========================================
// STEP 2: REVIEW THÔNG TIN BỆNH NHÂN
// ==========================================
function confirmPatientReview() {
  if (!selectedPatient.value) return
  currentStep.value = 3
}

function backToStep1() {
  currentStep.value = 1
}

// ==========================================
// STEP 3: CHỌN HÌNH THỨC & ĐẶT KHÁM
// ==========================================
type BookingType = 'walkin' | 'scheduled'
const bookingType = ref<BookingType>('walkin')

// Shared & Walk-in data
interface SpecialtyOption {
  id: string
  name: string
  label: string
}
const specialties = ref<SpecialtyOption[]>([])
const selectedSpecialtyId = ref<string>('')
const selectedSpecialtyName = computed(() => {
  const found = specialties.value.find((s) => s.id === selectedSpecialtyId.value)
  return found ? found.name : ''
})

// Working doctors for walk-in
const loadingWorkingDoctors = ref(false)
const workingDoctors = ref<WorkingDoctor[]>([])
const selectedDoctorId = ref<string>('') // empty = auto assign
const reason = ref('')
const isUrgent = ref(false)

// Scheduled booking specific data
const scheduledDoctorId = ref<string>('')
const scheduledDate = ref('')
const scheduledTimeSlot = ref('')
const scheduledWorkScheduleId = ref<string>('')
const scheduledReason = ref('')
const todayDateStr = new Date().toLocaleDateString('sv-SE')

// Submitting state
const submitting = ref(false)

// Result state for Step 4
const walkInResult = ref<WalkInResult | null>(null)
const scheduledResult = ref<any | null>(null)
const walkInTicketPending = ref(false)
const retryingWalkInTicket = ref(false)

async function refreshWalkInTicket(retrySync = false) {
  const appointmentId = walkInResult.value?.appointmentId
  if (!appointmentId) return
  retryingWalkInTicket.value = true
  try {
    if (retrySync) {
      await receptionistApi.retryWalkInAppointmentSync(appointmentId, walkInResult.value?.isUrgent ?? false)
    }
    const ticket = await receptionistApi.pollQueueTicketByAppointmentId(appointmentId)
    if (!ticket) {
      walkInTicketPending.value = true
      return
    }
    walkInResult.value!.queueNumber = Number(ticket.queueNumber)
    walkInResult.value!.queueTicketId = ticket.id
    walkInTicketPending.value = false
  } catch (error: any) {
    walkInTicketPending.value = true
    errorMessage.value = error?.response?.data?.message || error?.message || 'Không thể đồng bộ phiếu khám. Vui lòng thử lại.'
  } finally {
    retryingWalkInTicket.value = false
  }
}

async function loadSpecialties() {
  try {
    const res = await specialtiesApi.list({ pageSize: 100 })
    const items = res?.items || []
    specialties.value = items.map((s) => ({
      id: s.id,
      name: s.name,
      label: getSpecialtyDisplay(s.name) || s.name,
    }))
    const first = specialties.value[0]
    if (first && !selectedSpecialtyId.value) {
      selectedSpecialtyId.value = first.id
    }
  } catch {
    // fallback
  }
}

async function loadWorkingDoctors(specialtyId?: string) {
  if (!specialtyId) {
    workingDoctors.value = []
    return
  }
  loadingWorkingDoctors.value = true
  try {
    workingDoctors.value = await receptionistApi.getWorkingDoctors(specialtyId)
  } catch {
    workingDoctors.value = []
  } finally {
    loadingWorkingDoctors.value = false
  }
}

watch(selectedSpecialtyId, (newSpecId) => {
  selectedDoctorId.value = ''
  scheduledDoctorId.value = ''
  scheduledTimeSlot.value = ''
  scheduledWorkScheduleId.value = ''
  patientStore.clearSlots()
  if (bookingType.value === 'walkin') {
    loadWorkingDoctors(newSpecId)
  }
})

watch(bookingType, (type) => {
  if (type === 'walkin' && selectedSpecialtyId.value) {
    loadWorkingDoctors(selectedSpecialtyId.value)
  }
})

// Doctors for scheduled booking
const scheduledFilteredDoctors = computed(() => {
  if (!selectedSpecialtyName.value) return patientStore.doctors
  return patientStore.doctors.filter((d) => matchesSpecialty(d.specialty, selectedSpecialtyName.value))
})

async function onScheduledDoctorChange() {
  scheduledTimeSlot.value = ''
  scheduledWorkScheduleId.value = ''
  patientStore.clearSlots()
  if (scheduledDoctorId.value && scheduledDate.value) {
    await patientStore.loadSlots(scheduledDoctorId.value, scheduledDate.value)
  }
}

async function onScheduledDateChange() {
  scheduledTimeSlot.value = ''
  scheduledWorkScheduleId.value = ''
  patientStore.clearSlots()
  if (scheduledDoctorId.value && scheduledDate.value) {
    await patientStore.loadSlots(scheduledDoctorId.value, scheduledDate.value)
  }
}

function handleSelectSlot(slot: AvailableSlot | { id?: number | string; time: string; workScheduleId?: number | string }) {
  scheduledWorkScheduleId.value = String(slot.workScheduleId ?? slot.id ?? '')
  scheduledTimeSlot.value = slot.time
}

async function submitBooking() {
  if (!selectedPatient.value) return
  submitting.value = true
  errorMessage.value = ''

  try {
    if (bookingType.value === 'walkin') {
      if (!selectedSpecialtyId.value) {
        errorMessage.value = 'Vui lòng chọn chuyên khoa cần khám.'
        submitting.value = false
        return
      }

      const res = await receptionistApi.createWalkInAppointment({
        patientId: selectedPatient.value.id,
        specialtyId: selectedSpecialtyId.value,
        doctorId: selectedDoctorId.value || undefined,
        reason: reason.value.trim() || undefined,
        isUrgent: isUrgent.value,
      })

      walkInResult.value = res

      // Poll Queue Service for the real ticket number
      await refreshWalkInTicket()

      currentStep.value = 4
    } else {
      // Scheduled booking
      if (!scheduledWorkScheduleId.value || !scheduledTimeSlot.value) {
        errorMessage.value = 'Vui lòng chọn ngày và khung giờ hẹn khám.'
        submitting.value = false
        return
      }

      const res = await receptionistApi.createScheduledAppointment({
        patientId: selectedPatient.value.id,
        workScheduleId: scheduledWorkScheduleId.value,
        timeSlot: scheduledTimeSlot.value,
        reason: scheduledReason.value.trim() || 'Đặt lịch hẹn trước',
        isWalkIn: false,
      })

      scheduledResult.value = {
        ...res,
        patientName: selectedPatient.value.fullName,
        doctorName: scheduledFilteredDoctors.value.find((d) => d.id === scheduledDoctorId.value)?.name,
        specialtyName: selectedSpecialtyName.value,
        date: scheduledDate.value,
        timeSlot: scheduledTimeSlot.value,
      }
      currentStep.value = 4
    }
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || err?.response?.data?.title || err?.message || 'Lỗi khi xử lý đặt khám.'
  } finally {
    submitting.value = false
  }
}

// Reset wizard to accept new patient
function resetWizard() {
  currentStep.value = 1
  selectedPatient.value = null
  searchKeyword.value = ''
  searchedPatients.value = []
  newPatientForm.value = {
    fullName: '',
    phoneNumber: '',
    email: '',
    dateOfBirth: '',
    gender: 0,
    address: '',
    insuranceNumber: '',
    emergencyContact: '',
  }
  reason.value = ''
  isUrgent.value = false
  walkInResult.value = null
  walkInTicketPending.value = false
  scheduledResult.value = null
  errorMessage.value = ''
  successMessage.value = ''
  handleSearchPatients()
}

function handlePrintTicket() {
  window.print()
}

function formatGender(gender?: number | string | null): string {
  if (gender === 1 || gender === 'Female' || gender === 'Nữ') return 'Nữ'
  if (gender === 0 || gender === 'Male' || gender === 'Nam') return 'Nam'
  return 'Khác'
}

function formatDate(dateStr?: string | null): string {
  if (!dateStr) return '-'
  try {
    const d = new Date(dateStr)
    if (isNaN(d.getTime())) return dateStr
    return d.toLocaleDateString('vi-VN')
  } catch {
    return dateStr
  }
}

onMounted(async () => {
  await loadSpecialties()
  await patientStore.loadDoctors()
  await handleSearchPatients()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="no-print flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">
          Đặt khám &amp; Đăng ký khám tại quầy
        </h1>
        <p class="text-sm text-slate-500">
          Quy trình tiếp nhận, chọn hoặc tạo hồ sơ bệnh nhân, đặt lịch hoặc cấp số khám ngay (Walk-in)
        </p>
      </div>

      <div v-if="currentStep > 1" class="flex items-center gap-2">
        <BaseButton
          variant="outline"
          size="sm"
          @click="resetWizard"
        >
          Hủy &amp; Bắt đầu lại
        </BaseButton>
      </div>
    </div>

    <!-- Stepper Indicator -->
    <div class="no-print rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div class="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <!-- Step 1 -->
        <div
          class="flex items-center gap-3 rounded-lg p-2.5 transition-colors"
          :class="currentStep === 1 ? 'bg-violet-50 text-violet-700' : currentStep > 1 ? 'text-emerald-700' : 'text-slate-400'"
        >
          <div
            class="flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold"
            :class="currentStep === 1 ? 'bg-violet-600 text-white' : currentStep > 1 ? 'bg-emerald-600 text-white' : 'bg-slate-200 text-slate-600'"
          >
            <span v-if="currentStep <= 1">1</span>
            <svg v-else class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
              <polyline points="20 6 9 17 4 12" />
            </svg>
          </div>
          <div class="text-xs font-semibold leading-tight">
            1. Tiếp nhận bệnh nhân
          </div>
        </div>

        <!-- Step 2 -->
        <div
          class="flex items-center gap-3 rounded-lg p-2.5 transition-colors"
          :class="currentStep === 2 ? 'bg-violet-50 text-violet-700' : currentStep > 2 ? 'text-emerald-700' : 'text-slate-400'"
        >
          <div
            class="flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold"
            :class="currentStep === 2 ? 'bg-violet-600 text-white' : currentStep > 2 ? 'bg-emerald-600 text-white' : 'bg-slate-200 text-slate-600'"
          >
            <span v-if="currentStep <= 2">2</span>
            <svg v-else class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
              <polyline points="20 6 9 17 4 12" />
            </svg>
          </div>
          <div class="text-xs font-semibold leading-tight">
            2. Xác nhận thông tin
          </div>
        </div>

        <!-- Step 3 -->
        <div
          class="flex items-center gap-3 rounded-lg p-2.5 transition-colors"
          :class="currentStep === 3 ? 'bg-violet-50 text-violet-700' : currentStep > 3 ? 'text-emerald-700' : 'text-slate-400'"
        >
          <div
            class="flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold"
            :class="currentStep === 3 ? 'bg-violet-600 text-white' : currentStep > 3 ? 'bg-emerald-600 text-white' : 'bg-slate-200 text-slate-600'"
          >
            <span v-if="currentStep <= 3">3</span>
            <svg v-else class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
              <polyline points="20 6 9 17 4 12" />
            </svg>
          </div>
          <div class="text-xs font-semibold leading-tight">
            3. Chọn hình thức &amp; Đặt khám
          </div>
        </div>

        <!-- Step 4 -->
        <div
          class="flex items-center gap-3 rounded-lg p-2.5 transition-colors"
          :class="currentStep === 4 ? 'bg-emerald-50 text-emerald-700' : 'text-slate-400'"
        >
          <div
            class="flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold"
            :class="currentStep === 4 ? 'bg-emerald-600 text-white' : 'bg-slate-200 text-slate-600'"
          >
            4
          </div>
          <div class="text-xs font-semibold leading-tight">
            4. Hoàn tất &amp; Cấp số
          </div>
        </div>
      </div>
    </div>

    <!-- Error / Success Alerts -->
    <BaseAlert
      v-if="errorMessage"
      variant="danger"
      :message="errorMessage"
      class="no-print"
    />
    <BaseAlert
      v-if="successMessage"
      variant="success"
      :message="successMessage"
      class="no-print"
    />

    <!-- ============================================================== -->
    <!-- BƯỚC 1: CHỌN HOẶC TẠO HỒ SƠ BỆNH NHÂN -->
    <!-- ============================================================== -->
    <div v-if="currentStep === 1" class="space-y-6">
      <!-- Toggle Tabs -->
      <div class="flex rounded-xl border border-slate-200 bg-slate-100 p-1">
        <button
          type="button"
          class="flex-1 rounded-lg py-2.5 text-sm font-semibold transition-all"
          :class="patientMode === 'search' ? 'bg-white text-violet-700 shadow-sm' : 'text-slate-600 hover:text-slate-900'"
          @click="patientMode = 'search'"
        >
          <svg class="mr-1.5 inline-block h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          1. Chọn bệnh nhân đã có hồ sơ
        </button>

        <button
          type="button"
          class="flex-1 rounded-lg py-2.5 text-sm font-semibold transition-all"
          :class="patientMode === 'create' ? 'bg-white text-violet-700 shadow-sm' : 'text-slate-600 hover:text-slate-900'"
          @click="patientMode = 'create'"
        >
          <svg class="mr-1.5 inline-block h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
            <circle cx="8.5" cy="7" r="4" />
            <line x1="20" y1="8" x2="20" y2="14" />
            <line x1="23" y1="11" x2="17" y2="11" />
          </svg>
          2. Tạo hồ sơ người khám mới
        </button>
      </div>

      <!-- Tab A: Search Existing Patient -->
      <div v-if="patientMode === 'search'" class="space-y-4">
        <!-- Search bar -->
        <div class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <label class="mb-1.5 block text-xs font-semibold uppercase tracking-wider text-slate-500">
            Tra cứu thông tin bệnh nhân
          </label>
          <div class="flex gap-2">
            <input
              v-model="searchKeyword"
              type="text"
              placeholder="Nhập tên bệnh nhân, số điện thoại, số BHYT..."
              class="flex-1 rounded-lg border border-slate-300 px-3.5 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              @keyup.enter="handleSearchPatients"
            />
            <BaseButton
              variant="primary"
              :disabled="searchingPatients"
              @click="handleSearchPatients"
            >
              {{ searchingPatients ? 'Đang tìm...' : 'Tìm kiếm' }}
            </BaseButton>
          </div>
        </div>

        <!-- Patient search results table -->
        <div class="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
          <div v-if="searchingPatients" class="flex flex-col items-center justify-center p-12">
            <BaseSpinner class="h-8 w-8 text-violet-600" />
            <p class="mt-2 text-sm text-slate-500">Đang tìm kiếm bệnh nhân...</p>
          </div>

          <div v-else-if="searchedPatients.length === 0" class="flex flex-col items-center justify-center p-12 text-center">
            <p class="text-sm font-semibold text-slate-700">Không tìm thấy bệnh nhân phù hợp</p>
            <p class="mt-1 text-xs text-slate-400">Nếu là bệnh nhân mới, vui lòng chuyển qua tab "Tạo hồ sơ người khám mới".</p>
          </div>

          <div v-else class="overflow-x-auto">
            <table class="w-full text-left text-sm">
              <thead class="border-b border-slate-200 bg-slate-50 text-xs font-semibold uppercase tracking-wider text-slate-500">
                <tr>
                  <th scope="col" class="px-5 py-3.5">Họ và tên</th>
                  <th scope="col" class="px-5 py-3.5">Số điện thoại</th>
                  <th scope="col" class="px-5 py-3.5">Ngày sinh / Giới tính</th>
                  <th scope="col" class="px-5 py-3.5">Địa chỉ</th>
                  <th scope="col" class="px-5 py-3.5">Số BHYT</th>
                  <th scope="col" class="px-5 py-3.5 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                <tr
                  v-for="p in searchedPatients"
                  :key="p.id"
                  class="transition-colors hover:bg-slate-50"
                >
                  <td class="px-5 py-4 font-semibold text-slate-900">
                    {{ p.fullName }}
                    <div class="text-[11px] font-normal text-slate-400">ID: #{{ p.id.slice(0, 8) }}</div>
                  </td>
                  <td class="px-5 py-4 font-mono text-slate-700">
                    {{ p.phoneNumber || '—' }}
                  </td>
                  <td class="px-5 py-4 text-slate-700">
                    {{ formatDate(p.dateOfBirth) }} ({{ formatGender(p.gender) }})
                  </td>
                  <td class="px-5 py-4 text-slate-600 max-w-xs truncate" :title="p.address || ''">
                    {{ p.address || '—' }}
                  </td>
                  <td class="px-5 py-4 font-mono text-xs text-slate-600">
                    {{ p.insuranceNumber || '—' }}
                  </td>
                  <td class="px-5 py-4 text-right">
                    <BaseButton
                      variant="primary"
                      size="sm"
                      class="bg-violet-600 hover:bg-violet-700"
                      @click="handleSelectPatient(p)"
                    >
                      Chọn bệnh nhân
                    </BaseButton>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Tab B: Create New Patient Form -->
      <div v-else class="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h3 class="mb-4 text-base font-bold text-slate-900">
          Thông tin bệnh nhân mới
        </h3>

        <form class="space-y-4" @submit.prevent="handleCreatePatientSubmit">
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <!-- Họ và tên -->
            <div>
              <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-600">
                Họ và tên <span class="text-rose-500">*</span>
              </label>
              <input
                v-model="newPatientForm.fullName"
                type="text"
                required
                placeholder="Ví dụ: Nguyễn Văn A"
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              />
            </div>

            <!-- Số điện thoại -->
            <div>
              <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-600">
                Số điện thoại
              </label>
              <input
                v-model="newPatientForm.phoneNumber"
                type="tel"
                placeholder="Ví dụ: 0912345678"
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              />
            </div>

            <!-- Ngày sinh -->
            <div>
              <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-600">
                Ngày sinh <span class="text-rose-500">*</span>
              </label>
              <input
                v-model="newPatientForm.dateOfBirth"
                type="date"
                required
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              />
            </div>

            <!-- Giới tính -->
            <div>
              <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-600">
                Giới tính <span class="text-rose-500">*</span>
              </label>
              <select
                v-model="newPatientForm.gender"
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              >
                <option :value="0">Nam</option>
                <option :value="1">Nữ</option>
                <option :value="2">Khác</option>
              </select>
            </div>

            <!-- Email -->
            <div>
              <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-600">
                Email
              </label>
              <input
                v-model="newPatientForm.email"
                type="email"
                placeholder="example@gmail.com"
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              />
            </div>

            <!-- Số thẻ BHYT -->
            <div>
              <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-600">
                Số thẻ BHYT (nếu có)
              </label>
              <input
                v-model="newPatientForm.insuranceNumber"
                type="text"
                placeholder="DN479..."
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              />
            </div>

            <!-- Địa chỉ -->
            <div class="sm:col-span-2">
              <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-600">
                Địa chỉ thường trú <span class="text-rose-500">*</span>
              </label>
              <input
                v-model="newPatientForm.address"
                type="text"
                required
                placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/thành"
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              />
            </div>

            <!-- Liên hệ khẩn cấp -->
            <div class="sm:col-span-2">
              <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-600">
                Người liên hệ khẩn cấp (Họ tên - SĐT)
              </label>
              <input
                v-model="newPatientForm.emergencyContact"
                type="text"
                placeholder="Ví dụ: Nguyễn Văn B (Bố) - 0987654321"
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              />
            </div>
          </div>

          <div class="mt-6 flex justify-end gap-3 border-t border-slate-100 pt-4">
            <BaseButton
              type="submit"
              variant="primary"
              :disabled="creatingPatient"
              class="gap-1.5"
            >
              <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <polyline points="20 6 9 17 4 12" />
              </svg>
              {{ creatingPatient ? 'Đang tạo hồ sơ...' : 'Tạo hồ sơ & Tiếp tục bước 2' }}
            </BaseButton>
          </div>
        </form>
      </div>
    </div>

    <!-- ============================================================== -->
    <!-- BƯỚC 2: REVIEW THÔNG TIN BỆNH NHÂN (XÁC NHẬN VỚI NGƯỜI BỆNH) -->
    <!-- ============================================================== -->
    <div v-else-if="currentStep === 2 && selectedPatient" class="space-y-6">
      <div class="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <div class="mb-6 border-b border-slate-100 pb-4">
          <h2 class="text-lg font-bold text-slate-900">
            Bước 2: Xác nhận lại thông tin bệnh nhân
          </h2>
          <p class="text-sm text-slate-500">
            Lễ tân vui lòng đọc lại và đối chiếu các thông tin dưới đây với người bệnh trước khi tiếp tục
          </p>
        </div>

        <div class="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          <div class="rounded-lg bg-slate-50 p-4">
            <div class="text-xs font-semibold uppercase tracking-wider text-slate-400">Họ và tên</div>
            <div class="mt-1 text-lg font-bold text-slate-900">{{ selectedPatient.fullName }}</div>
          </div>

          <div class="rounded-lg bg-slate-50 p-4">
            <div class="text-xs font-semibold uppercase tracking-wider text-slate-400">Số điện thoại</div>
            <div class="mt-1 text-lg font-mono font-semibold text-slate-900">{{ selectedPatient.phoneNumber || 'Chưa cung cấp' }}</div>
          </div>

          <div class="rounded-lg bg-slate-50 p-4">
            <div class="text-xs font-semibold uppercase tracking-wider text-slate-400">Ngày sinh &amp; Giới tính</div>
            <div class="mt-1 text-base font-semibold text-slate-900">
              {{ formatDate(selectedPatient.dateOfBirth) }} ({{ formatGender(selectedPatient.gender) }})
            </div>
          </div>

          <div class="rounded-lg bg-slate-50 p-4">
            <div class="text-xs font-semibold uppercase tracking-wider text-slate-400">Số thẻ BHYT</div>
            <div class="mt-1 font-mono text-base font-semibold text-slate-900">{{ selectedPatient.insuranceNumber || 'Không có' }}</div>
          </div>

          <div class="rounded-lg bg-slate-50 p-4 sm:col-span-2">
            <div class="text-xs font-semibold uppercase tracking-wider text-slate-400">Địa chỉ thường trú</div>
            <div class="mt-1 text-sm font-medium text-slate-900">{{ selectedPatient.address || '—' }}</div>
          </div>

          <div v-if="selectedPatient.emergencyContact" class="rounded-lg bg-slate-50 p-4 sm:col-span-3">
            <div class="text-xs font-semibold uppercase tracking-wider text-slate-400">Liên hệ khẩn cấp</div>
            <div class="mt-1 text-sm font-medium text-slate-900">{{ selectedPatient.emergencyContact }}</div>
          </div>
        </div>

        <div class="mt-8 flex items-center justify-between border-t border-slate-100 pt-5">
          <BaseButton
            variant="outline"
            @click="backToStep1"
          >
            Quay lại chọn bệnh nhân khác
          </BaseButton>

          <BaseButton
            variant="primary"
            class="bg-violet-600 hover:bg-violet-700 gap-1.5"
            @click="confirmPatientReview"
          >
            Thông tin chính xác, tiếp tục đặt khám
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="9 18 15 12 9 6" />
            </svg>
          </BaseButton>
        </div>
      </div>
    </div>

    <!-- ============================================================== -->
    <!-- BƯỚC 3: CHỌN HÌNH THỨC & ĐẶT KHÁM -->
    <!-- ============================================================== -->
    <div v-else-if="currentStep === 3 && selectedPatient" class="space-y-6">
      <!-- Patient Quick Summary -->
      <div class="flex items-center justify-between rounded-xl border border-violet-100 bg-violet-50/60 p-4">
        <div>
          <span class="text-xs font-semibold uppercase tracking-wider text-violet-600">Bệnh nhân đang tiếp nhận:</span>
          <span class="ml-2 text-base font-bold text-slate-900">{{ selectedPatient.fullName }}</span>
          <span class="ml-2 font-mono text-xs text-slate-500">({{ selectedPatient.phoneNumber || 'Không SĐT' }})</span>
        </div>
        <BaseButton
          variant="ghost"
          size="sm"
          class="text-violet-700 hover:bg-violet-100"
          @click="currentStep = 2"
        >
          Xem lại thông tin
        </BaseButton>
      </div>

      <!-- Type selection toggle -->
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <!-- Option 1: Walk-in -->
        <div
          class="cursor-pointer rounded-xl border-2 p-5 transition-all"
          :class="bookingType === 'walkin' ? 'border-violet-600 bg-violet-50/30 ring-2 ring-violet-500/20' : 'border-slate-200 bg-white hover:border-slate-300'"
          @click="bookingType = 'walkin'"
        >
          <div class="flex items-start gap-4">
            <div
              class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg"
              :class="bookingType === 'walkin' ? 'bg-violet-600 text-white' : 'bg-slate-100 text-slate-600'"
            >
              <svg class="h-6 w-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
              </svg>
            </div>
            <div>
              <div class="flex items-center gap-2">
                <h3 class="text-base font-bold text-slate-900">Đăng ký khám ngay (Walk-in)</h3>
                <span class="rounded-full bg-violet-100 px-2 py-0.5 text-[11px] font-semibold text-violet-700">Khám trong ngày</span>
              </div>
              <p class="mt-1 text-xs text-slate-500">
                Bệnh nhân đến trực tiếp tại quầy và có nhu cầu khám ngay. Hệ thống sẽ tự động check-in và cấp số thứ tự vào hàng đợi.
              </p>
            </div>
          </div>
        </div>

        <!-- Option 2: Scheduled -->
        <div
          class="cursor-pointer rounded-xl border-2 p-5 transition-all"
          :class="bookingType === 'scheduled' ? 'border-violet-600 bg-violet-50/30 ring-2 ring-violet-500/20' : 'border-slate-200 bg-white hover:border-slate-300'"
          @click="bookingType = 'scheduled'"
        >
          <div class="flex items-start gap-4">
            <div
              class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg"
              :class="bookingType === 'scheduled' ? 'bg-violet-600 text-white' : 'bg-slate-100 text-slate-600'"
            >
              <svg class="h-6 w-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                <line x1="16" y1="2" x2="16" y2="6" />
                <line x1="8" y1="2" x2="8" y2="6" />
                <line x1="3" y1="10" x2="21" y2="10" />
              </svg>
            </div>
            <div>
              <div class="flex items-center gap-2">
                <h3 class="text-base font-bold text-slate-900">Đặt lịch hẹn trước (Scheduled)</h3>
                <span class="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-600">Chọn ngày &amp; giờ</span>
              </div>
              <p class="mt-1 text-xs text-slate-500">
                Đặt lịch theo khung giờ hẹn cụ thể với bác sĩ chuyên khoa mong muốn (chưa check-in vào hàng đợi ngay).
              </p>
            </div>
          </div>
        </div>
      </div>

      <!-- FORM 1: WALK-IN (KHÁM NGAY) -->
      <div v-if="bookingType === 'walkin'" class="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h3 class="mb-4 text-base font-bold text-slate-900">
          Thông tin đăng ký khám ngay (Walk-in)
        </h3>

        <div class="space-y-4">
          <!-- Specialty Dropdown (Required) -->
          <div>
            <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Chọn chuyên khoa <span class="text-rose-500">*</span>
            </label>
            <select
              v-model="selectedSpecialtyId"
              class="w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
            >
              <option value="" disabled>-- Chọn khoa khám --</option>
              <option
                v-for="s in specialties"
                :key="s.id"
                :value="s.id"
              >
                {{ s.label }}
              </option>
            </select>
          </div>

          <!-- Doctor Dropdown (Optional) -->
          <div>
            <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Chọn bác sĩ đang trực hôm nay (Tùy chọn)
            </label>
            <div v-if="loadingWorkingDoctors" class="flex items-center gap-2 py-2 text-xs text-slate-500">
              <BaseSpinner class="h-4 w-4 text-violet-600" />
              Đang tải danh sách bác sĩ trực hôm nay...
            </div>
            <select
              v-else
              v-model="selectedDoctorId"
              class="w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
            >
              <option value="">(Hệ thống tự động phân bổ bác sĩ có ít lượt khám nhất trong ca)</option>
              <option
                v-for="d in workingDoctors"
                :key="d.doctorId"
                :value="d.doctorId"
              >
                BS. {{ d.fullName }} (Ca trực: {{ d.shiftStart }} - {{ d.shiftEnd }}, Hiện có {{ d.activeAppointmentsCount }} lượt)
              </option>
            </select>
            <p v-if="!loadingWorkingDoctors && workingDoctors.length === 0 && selectedSpecialtyId" class="mt-1 text-xs text-amber-600">
              Chưa có bác sĩ nào mở ca trực hôm nay trong khoa này.
            </p>
          </div>

          <!-- Reason -->
          <div>
            <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Lý do khám / Triệu chứng ban đầu
            </label>
            <textarea
              v-model="reason"
              rows="3"
              placeholder="Ví dụ: Đau đầu, sốt nhẹ 2 ngày nay..."
              class="w-full rounded-lg border border-slate-300 p-3 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
            ></textarea>
          </div>

          <!-- Urgent Checkbox -->
          <div class="rounded-lg border border-rose-200 bg-rose-50/60 p-4">
            <label class="flex cursor-pointer items-start gap-3">
              <input
                v-model="isUrgent"
                type="checkbox"
                class="mt-0.5 h-4 w-4 rounded border-slate-300 text-rose-600 focus:ring-rose-500"
              />
              <div>
                <span class="text-sm font-bold text-rose-800">Đánh dấu trường hợp khẩn cấp / Ưu tiên cao</span>
                <p class="text-xs text-rose-600">
                  Phiếu khám sẽ được đánh dấu ưu tiên (Priority = true) và được đẩy lên đầu danh sách chờ gọi vào phòng khám của bác sĩ.
                </p>
              </div>
            </label>
          </div>
        </div>

        <div class="mt-8 flex items-center justify-between border-t border-slate-100 pt-5">
          <BaseButton
            variant="outline"
            @click="currentStep = 2"
          >
            Quay lại
          </BaseButton>

          <BaseButton
            variant="primary"
            class="bg-violet-600 hover:bg-violet-700 gap-1.5"
            :disabled="submitting || !selectedSpecialtyId"
            @click="submitBooking"
          >
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="20 6 9 17 4 12" />
            </svg>
            {{ submitting ? 'Đang cấp số...' : 'Xác nhận đăng ký & Cấp số thứ tự' }}
          </BaseButton>
        </div>
      </div>

      <!-- FORM 2: SCHEDULED BOOKING -->
      <div v-else class="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h3 class="mb-4 text-base font-bold text-slate-900">
          Thông tin đặt lịch hẹn trước (Scheduled)
        </h3>

        <div class="space-y-4">
          <!-- Specialty -->
          <div>
            <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Chuyên khoa <span class="text-rose-500">*</span>
            </label>
            <select
              v-model="selectedSpecialtyId"
              class="w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
            >
              <option value="" disabled>-- Chọn khoa --</option>
              <option
                v-for="s in specialties"
                :key="s.id"
                :value="s.id"
              >
                {{ s.label }}
              </option>
            </select>
          </div>

          <!-- Doctor -->
          <div>
            <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Chọn bác sĩ <span class="text-rose-500">*</span>
            </label>
            <select
              v-model="scheduledDoctorId"
              class="w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              @change="onScheduledDoctorChange"
            >
              <option value="" disabled>-- Chọn bác sĩ khám --</option>
              <option
                v-for="d in scheduledFilteredDoctors"
                :key="d.id"
                :value="d.id"
              >
                BS. {{ d.name }}{{ d.room ? ' - Phòng ' + d.room : '' }}
              </option>
            </select>
          </div>

          <!-- Date Picker -->
          <div>
            <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Ngày hẹn khám <span class="text-rose-500">*</span>
            </label>
            <input
              v-model="scheduledDate"
              type="date"
              :min="todayDateStr"
              class="w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              @change="onScheduledDateChange"
            />
          </div>

          <!-- Time slot picker -->
          <div v-if="scheduledDoctorId && scheduledDate" class="space-y-2">
            <label class="block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Chọn khung giờ khám <span class="text-rose-500">*</span>
            </label>
            <PatientSlotPicker
              :slots="patientStore.slots"
              :selected-time="scheduledTimeSlot"
              :loading="patientStore.slotsLoading"
              @select="handleSelectSlot"
            />
          </div>

          <!-- Reason -->
          <div>
            <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-700">
              Lý do khám
            </label>
            <textarea
              v-model="scheduledReason"
              rows="3"
              placeholder="Triệu chứng, lý do đặt lịch hẹn..."
              class="w-full rounded-lg border border-slate-300 p-3 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
            ></textarea>
          </div>
        </div>

        <div class="mt-8 flex items-center justify-between border-t border-slate-100 pt-5">
          <BaseButton
            variant="outline"
            @click="currentStep = 2"
          >
            Quay lại
          </BaseButton>

          <BaseButton
            variant="primary"
            class="bg-violet-600 hover:bg-violet-700 gap-1.5"
            :disabled="submitting || !scheduledWorkScheduleId"
            @click="submitBooking"
          >
            {{ submitting ? 'Đang đặt lịch...' : 'Xác nhận đặt lịch hẹn' }}
          </BaseButton>
        </div>
      </div>
    </div>

    <!-- ============================================================== -->
    <!-- BƯỚC 4: KẾT QUẢ ĐẶT KHÁM & CẤP SỐ THỨ TỰ (WALK-IN HOẶC HẸN) -->
    <!-- ============================================================== -->
    <div v-else-if="currentStep === 4" class="space-y-6">
      <!-- Walk-in Result: STYLED QUEUE TICKET CARD -->
      <div v-if="walkInResult" class="mx-auto max-w-lg">
        <div class="ticket-card overflow-hidden rounded-2xl border-2 border-violet-600 bg-white shadow-xl">
          <!-- Ticket Header -->
          <div class="bg-violet-600 px-6 py-5 text-center text-white">
            <div class="text-xs uppercase tracking-widest font-semibold opacity-90">PHÒNG KHÁM ĐA KHOA QUỐC TẾ</div>
            <h2 class="mt-1 text-xl font-extrabold uppercase">PHIẾU KHÁM BỆNH</h2>
            <div class="mt-1 text-xs opacity-80">Số thứ tự khám trong ngày</div>
          </div>

          <!-- Queue Number Display -->
          <div class="bg-violet-50/50 py-8 text-center border-b border-dashed border-slate-300">
            <div class="text-xs font-semibold uppercase tracking-wider text-slate-500">SỐ THỨ TỰ CỦA BẠN</div>
            <div class="mt-2 font-mono text-6xl font-black text-violet-700">
              {{ walkInTicketPending ? 'Đang chờ' : `#${walkInResult.queueNumber}` }}
            </div>
            <div v-if="walkInResult.isUrgent" class="mt-3">
              <span class="inline-flex items-center gap-1 rounded-full bg-rose-100 px-3 py-1 text-xs font-bold text-rose-700">
                <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                  <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                  <line x1="12" y1="9" x2="12" y2="13" />
                  <line x1="12" y1="17" x2="12.01" y2="17" />
                </svg>
                ƯU TIÊN KHẨN CẤP
              </span>
            </div>
          </div>

          <!-- Ticket Info Details -->
          <div class="p-6 space-y-3.5 text-sm">
            <div class="flex items-center justify-between">
              <span class="text-slate-500">Bệnh nhân:</span>
              <span class="font-bold text-slate-900">{{ walkInResult.patientName }}</span>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-slate-500">Chuyên khoa:</span>
              <span class="font-semibold text-violet-700">{{ walkInResult.specialtyName }}</span>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-slate-500">Bác sĩ khám:</span>
              <span class="font-semibold text-slate-900">BS. {{ walkInResult.doctorName }}</span>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-slate-500">Thời gian cấp số:</span>
              <span class="font-mono text-slate-700">{{ new Date(walkInResult.checkInTime).toLocaleTimeString('vi-VN') }} - {{ new Date(walkInResult.checkInTime).toLocaleDateString('vi-VN') }}</span>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-slate-500">Trạng thái:</span>
              <span v-if="walkInTicketPending" class="inline-flex items-center rounded-full bg-amber-50 px-2.5 py-0.5 text-xs font-semibold text-amber-700">
                Đang đồng bộ Queue; chưa thể in phiếu
              </span>
              <span v-else class="inline-flex items-center rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-semibold text-emerald-700">
                Đã vào hàng đợi (Waiting)
              </span>
            </div>
          </div>

          <!-- Ticket Footer Note -->
          <div class="bg-slate-50 px-6 py-4 text-center text-xs text-slate-500 border-t border-slate-100">
            Vui lòng ngồi chờ tại sảnh phòng khám. Khi số thứ tự được gọi, xin mời vào phòng khám tương ứng.
          </div>
        </div>

        <!-- Action Buttons -->
        <div class="no-print mt-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-center">
          <BaseButton
            v-if="walkInTicketPending"
            variant="outline"
            :loading="retryingWalkInTicket"
            :disabled="retryingWalkInTicket"
            @click="refreshWalkInTicket(true)"
          >
            Thử đồng bộ/lấy phiếu lại
          </BaseButton>
          <BaseButton
            v-if="!walkInTicketPending"
            variant="outline"
            class="gap-1.5"
            @click="handlePrintTicket"
          >
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="6 9 6 2 18 2 18 9" />
              <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
              <rect x="6" y="14" width="12" height="8" />
            </svg>
            In phiếu khám
          </BaseButton>

          <BaseButton
            variant="primary"
            class="bg-violet-600 hover:bg-violet-700 gap-1.5"
            :disabled="walkInTicketPending"
            @click="resetWizard"
          >
            Tiếp đón bệnh nhân mới
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="9 18 15 12 9 6" />
            </svg>
          </BaseButton>
        </div>
      </div>

      <!-- Scheduled Booking Result -->
      <div v-else-if="scheduledResult" class="mx-auto max-w-lg">
        <div class="overflow-hidden rounded-2xl border border-slate-200 bg-white p-6 shadow-sm text-center">
          <div class="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-emerald-100 text-emerald-600">
            <svg class="h-8 w-8" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="20 6 9 17 4 12" />
            </svg>
          </div>

          <h2 class="mt-4 text-xl font-bold text-slate-900">Đặt lịch hẹn khám thành công!</h2>
          <p class="mt-1 text-sm text-slate-500">Lịch hẹn đã được ghi nhận trên hệ thống</p>

          <div class="mt-6 rounded-xl bg-slate-50 p-4 text-left space-y-2.5 text-sm">
            <div class="flex justify-between">
              <span class="text-slate-500">Bệnh nhân:</span>
              <span class="font-bold text-slate-900">{{ scheduledResult.patientName }}</span>
            </div>
            <div class="flex justify-between">
              <span class="text-slate-500">Bác sĩ:</span>
              <span class="font-semibold text-slate-900">BS. {{ scheduledResult.doctorName }}</span>
            </div>
            <div class="flex justify-between">
              <span class="text-slate-500">Chuyên khoa:</span>
              <span class="font-semibold text-violet-700">{{ scheduledResult.specialtyName }}</span>
            </div>
            <div class="flex justify-between">
              <span class="text-slate-500">Ngày khám:</span>
              <span class="font-semibold text-slate-900">{{ formatDate(scheduledResult.date) }}</span>
            </div>
            <div class="flex justify-between">
              <span class="text-slate-500">Khung giờ:</span>
              <span class="font-mono font-bold text-violet-700">{{ scheduledResult.timeSlot }}</span>
            </div>
          </div>

          <div class="mt-6">
            <BaseButton
              variant="primary"
              class="w-full bg-violet-600 hover:bg-violet-700"
              @click="resetWizard"
            >
              Tiếp đón bệnh nhân tiếp theo
            </BaseButton>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@media print {
  .no-print {
    display: none !important;
  }

  .ticket-card {
    border: 2px solid #000 !important;
    box-shadow: none !important;
    max-width: 100% !important;
    margin: 0 auto !important;
  }
}
</style>
