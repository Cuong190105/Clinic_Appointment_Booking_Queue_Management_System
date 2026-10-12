<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { usePatientStore } from '@/stores/patient'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import PatientBackendNotice from '@/features/patients/components/PatientBackendNotice.vue'
import PatientDoctorCard from '@/features/patients/components/PatientDoctorCard.vue'
import PatientSlotPicker from '@/features/patients/components/PatientSlotPicker.vue'
import { specialtiesApi } from '@/features/specialties/specialties.api'
import { formatSpecialtyName, getSpecialtyDisplay, matchesSpecialty } from '@/features/specialties/specialties.utils'
import type { Appointment, AvailableSlot } from '@/features/patients/patient.types'

const router = useRouter()
const patient = usePatientStore()
const auth = useAuthStore()

// Quản lý các bước trong quy trình đặt lịch: 1 - Chọn bác sĩ, 2 - Thông tin bệnh nhân, 3 - Xác nhận
const step = ref(1)

const specialty = ref('')
const doctorId = ref<string | number | null>(null)
const selectedSlotId = ref<number | string | null>(null)
const appointmentDate = ref('')
const appointmentTime = ref('')
const symptoms = ref('')

// Trạng thái đặt lịch thành công và thông tin cuộc hẹn đã tạo
const success = ref(false)
const createdAppointment = ref<Appointment | null>(null)

// Lấy ngày hiện tại theo định dạng yyyy-mm-dd để giới hạn ngày đặt lịch
const todayDate = new Date().toLocaleDateString('sv-SE')

// Hàm tìm và đồng bộ hóa khung giờ đã chọn dựa trên thời gian cuộc hẹn
function syncSelectedSlotFromTime() {
  if (!appointmentTime.value) {
    selectedSlotId.value = null
    return
  }

  const matched = patient.slots.find((slot) => slot.time === appointmentTime.value)
  if (matched) {
    selectedSlotId.value = matched.workScheduleId ?? matched.id
  }
}

// Theo dõi sự thay đổi chuyên khoa để reset các lựa chọn liên quan đến bác sĩ và khung giờ
watch(specialty, () => {
  doctorId.value = null
  selectedSlotId.value = null
  appointmentTime.value = ''
  patient.clearSlots()
})

// Theo dõi sự thay đổi bác sĩ để reset các lựa chọn liên quan đến khung giờ
watch(() => appointmentTime.value, () => {
  syncSelectedSlotFromTime()
})

// Theo dõi sự thay đổi khung giờ để đồng bộ hóa với thời gian cuộc hẹn
watch(
  () => patient.slots,
  () => {
    syncSelectedSlotFromTime()
  },
  { deep: true },
)

interface SpecialtyOption {
  key: string
  label: string
}

const defaultSpecialties: SpecialtyOption[] = [
  { key: 'Cardiology', label: 'Tim mạch' },
  { key: 'Dermatology', label: 'Da liễu' },
  { key: 'Pediatrics', label: 'Nhi khoa' },
  { key: 'Orthopedics', label: 'Xương khớp' },
  { key: 'General Practice', label: 'Nội tổng quát' },
  { key: 'Tai mũi họng', label: 'Tai mũi họng' },
]

const apiSpecialties = ref<SpecialtyOption[]>([])

async function loadSpecialties() {
  try {
    const res = await specialtiesApi.list({ pageSize: 100 })
    const items = res?.items || []
    if (items.length > 0) {
      apiSpecialties.value = items.map((s) => ({
        key: s.name,
        label: getSpecialtyDisplay(s.name) || s.name,
      }))
    }
  } catch {
    // ignore
  }
}

const displayedSpecialties = computed<SpecialtyOption[]>(() => {
  if (apiSpecialties.value.length > 0) {
    return apiSpecialties.value
  }

  // Extract from loaded doctors if available
  const docSpecs = new Set<string>()
  patient.doctors.forEach((d) => {
    if (d.specialty) {
      docSpecs.add(d.specialty)
    }
  })

  if (docSpecs.size > 0) {
    const list: SpecialtyOption[] = []
    docSpecs.forEach((spec) => {
      list.push({
        key: spec,
        label: getSpecialtyDisplay(spec) || spec,
      })
    })
    return list
  }

  return defaultSpecialties
})

// Tính toán danh sách bác sĩ dựa trên chuyên khoa đã chọn
const filteredDoctors = computed(() => {
  if (!specialty.value) return patient.doctors
  return patient.doctors.filter((doctor) => matchesSpecialty(doctor.specialty, specialty.value))
})

// Lấy thông tin bác sĩ đã chọn dựa trên doctorId
const selectedDoctor = computed(() =>
  patient.doctors.find((doctor) => doctor.id === doctorId.value)
)

// Hàm tải danh sách bác sĩ khi component được mounted
onMounted(async () => {
  await reloadBookingData()
  loadSpecialties()
})

async function reloadBookingData() {
  await patient.loadDoctors()
  if (doctorId.value && appointmentDate.value) {
    await patient.loadSlots(doctorId.value, appointmentDate.value)
  }
}

// Hàm xử lý sự kiện khi người dùng chọn bác sĩ
async function selectDoctor(id: string | number) {
  doctorId.value = id
  selectedSlotId.value = null
  appointmentTime.value = ''
  patient.clearSlots()

  if (appointmentDate.value) {
    await patient.loadSlots(id, appointmentDate.value)
  }
}

// Hàm xử lý sự kiện khi người dùng thay đổi ngày đặt lịch
async function changeDate() {
  selectedSlotId.value = null
  appointmentTime.value = ''
  patient.clearSlots()

  if (doctorId.value && appointmentDate.value) {
    await patient.loadSlots(doctorId.value, appointmentDate.value)
  }
}

// Hàm xử lý sự kiện khi người dùng chọn khung giờ
function handleSelectSlot(slot: AvailableSlot | { id?: number | string; time: string; workScheduleId?: number | string }) {
  selectedSlotId.value = slot.workScheduleId ?? slot.id ?? null
  appointmentTime.value = slot.time
}

const chooseSlot = handleSelectSlot

// Chuyển sang bước tiếp theo
function nextStep() {
  if (step.value === 1) {
    if (!doctorId.value || !appointmentDate.value || !appointmentTime.value || !selectedSlotId.value) {
      return
    }
  }
  step.value++
}

// Quay lại bước trước
function previousStep() {
  if (step.value > 1) {
    step.value--
  }
}

// Hàm xác nhận đặt lịch
async function confirmBooking() {
  const resolvedSlotId = selectedSlotId.value ??
    patient.slots.find((slot) => slot.time === appointmentTime.value)?.workScheduleId ??
    patient.slots.find((slot) => slot.time === appointmentTime.value)?.id

  if (!doctorId.value || !resolvedSlotId || !appointmentDate.value || !appointmentTime.value) {
    return
  }

  try {
    const result = await patient.createAppointment({
      doctorId: doctorId.value,
      workScheduleId: resolvedSlotId,
      appointmentDate: appointmentDate.value,
      appointmentTime: appointmentTime.value,
      symptoms: symptoms.value.trim() || undefined,
    })

    createdAppointment.value = result
    success.value = true
  } catch {
    // lỗi đã được lưu trong store
  }
}

// Hàm reset lại trạng thái đặt lịch
function newBooking() {
  step.value = 1
  specialty.value = ''
  doctorId.value = null
  selectedSlotId.value = null
  appointmentDate.value = ''
  appointmentTime.value = ''
  symptoms.value = ''
  success.value = false
  createdAppointment.value = null
  patient.clearSlots()
}

defineExpose({
  step,
  specialty,
  doctorId,
  selectedSlotId,
  appointmentDate,
  appointmentTime,
  symptoms,
  success,
  createdAppointment,
  selectDoctor,
  changeDate,
  chooseSlot,
  handleSelectSlot,
  nextStep,
  previousStep,
  confirmBooking,
  newBooking,
})
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-5">
    <div>
      <h1 class="text-2xl font-bold text-slate-800">
        Đặt lịch khám bệnh
      </h1>
      <p class="mt-1 text-xs text-slate-500">
        Đặt hẹn trực tuyến nhanh chóng, nhận số thứ tự khám ngay lập tức
      </p>
    </div>

    <!-- Thông báo kết nối máy chủ nếu có sự cố -->
    <PatientBackendNotice @reconnected="reloadBookingData" />

    <!-- SUCCESS STATE -->
    <section
      v-if="success"
      class="rounded-2xl border border-slate-200 bg-white p-6 sm:p-8 text-center shadow-xs"
    >
      <div
        class="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-emerald-100 text-3xl text-emerald-600 font-bold"
      >
        ✓
      </div>

      <h2 class="text-2xl font-bold text-slate-800">
        Đặt lịch thành công!
      </h2>

      <p class="mt-2 text-xs sm:text-sm text-slate-500 max-w-md mx-auto">
        Lịch hẹn của bạn đã được ghi nhận vào hệ thống. Vui lòng đến trước giờ khám để hoàn tất thủ tục check-in.
      </p>

      <div class="mt-6 rounded-2xl bg-gradient-to-br from-[#0E4D92] to-[#1a6bbf] p-5 text-left text-white shadow-xs">
        <div class="flex items-center justify-between">
          <span class="text-xs font-semibold uppercase tracking-wider text-blue-100">
            Số thứ tự hàng đợi
          </span>
          <span class="text-2xl font-extrabold tracking-tight">
            {{ createdAppointment?.queueNumber || '---' }}
          </span>
        </div>

        <div class="my-3.5 h-px bg-white/20" />

        <div class="space-y-2.5 text-sm">
          <div class="flex justify-between">
            <span class="text-blue-100 font-medium">Bác sĩ khám:</span>
            <span class="font-bold">{{ selectedDoctor?.name }}</span>
          </div>

          <div class="flex justify-between">
            <span class="text-blue-100 font-medium">Chuyên khoa:</span>
            <span class="font-bold">{{ selectedDoctor?.specialty }}</span>
          </div>

          <div class="flex justify-between">
            <span class="text-blue-100 font-medium">Ngày khám:</span>
            <span class="font-bold">{{ appointmentDate }}</span>
          </div>

          <div class="flex justify-between">
            <span class="text-blue-100 font-medium">Giờ hẹn:</span>
            <span class="font-bold text-amber-300">{{ appointmentTime }}</span>
          </div>
        </div>
      </div>

      <div class="mt-6 flex flex-col sm:flex-row gap-3">
        <BaseButton
          variant="outline"
          block
          @click="newBooking"
        >
          + Đặt thêm lịch mới
        </BaseButton>

        <BaseButton
          variant="primary"
          block
          @click="router.push('/patient/queue')"
        >
          Theo dõi hàng đợi →
        </BaseButton>
      </div>
    </section>

    <!-- STEPPER PROCESS -->
    <template v-else>
      <!-- Stepper indicator -->
      <div class="flex items-center justify-between rounded-xl bg-white p-3 border border-slate-200 shadow-2xs">
        <div
          v-for="(sName, idx) in ['1. Chọn bác sĩ & giờ', '2. Triệu chứng', '3. Xác nhận']"
          :key="sName"
          class="flex items-center gap-2 text-xs font-semibold"
          :class="step === idx + 1 ? 'text-[#0E4D92]' : step > idx + 1 ? 'text-emerald-600' : 'text-slate-400'"
        >
          <span
            class="h-5 w-5 rounded-full flex items-center justify-center text-[10px] font-bold"
            :class="step === idx + 1 ? 'bg-[#0E4D92] text-white' : step > idx + 1 ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-400'"
          >
            {{ step > idx + 1 ? '✓' : idx + 1 }}
          </span>
          <span class="hidden sm:inline">{{ sName }}</span>
        </div>
      </div>

      <!-- STEP 1 -->
      <section v-if="step === 1" class="space-y-4">
        <!-- Specialty filter -->
        <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-2xs">
          <label class="mb-2 block text-xs font-bold text-slate-700 uppercase tracking-wider">
            Lọc theo chuyên khoa
          </label>

          <div class="flex flex-wrap gap-2">
            <button
              type="button"
              class="rounded-xl border px-3.5 py-1.5 text-xs font-semibold transition-all select-none"
              :class="
                specialty === ''
                  ? 'border-[#0E4D92] bg-[#0E4D92] text-white shadow-2xs'
                  : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
              "
              @click="specialty = ''"
            >
              Tất cả
            </button>

            <button
              v-for="sp in displayedSpecialties"
              :key="sp.key"
              type="button"
              class="rounded-xl border px-3.5 py-1.5 text-xs font-semibold transition-all select-none cursor-pointer"
              :class="
                specialty === sp.key || specialty === sp.label
                  ? 'border-[#0E4D92] bg-[#0E4D92] text-white shadow-2xs'
                  : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
              "
              @click="specialty = sp.key"
            >
              {{ sp.label }}
            </button>
          </div>
        </div>

        <!-- Date Picker -->
        <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-2xs">
          <label class="mb-2 block text-xs font-bold text-slate-700 uppercase tracking-wider">
            Chọn ngày khám
          </label>

          <input
            v-model="appointmentDate"
            type="date"
            :min="todayDate"
            class="w-full rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-800 focus:border-[#0E4D92] focus:outline-none focus:ring-1 focus:ring-[#0E4D92]"
            @change="changeDate"
          />
        </div>

        <!-- Doctor list -->
        <div class="space-y-3">
          <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider px-1">
            Chọn bác sĩ ({{ filteredDoctors.length }})
          </label>

          <div v-if="!filteredDoctors.length" class="rounded-2xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">
            Không tìm thấy bác sĩ phù hợp với chuyên khoa đã chọn.
          </div>

          <PatientDoctorCard
            v-for="doctor in filteredDoctors"
            :key="doctor.id"
            :doctor="doctor"
            :selected="doctorId === doctor.id"
            @select="selectDoctor"
          >
            <!-- Slot Picker inside selected doctor card -->
            <PatientSlotPicker
              v-if="doctorId === doctor.id"
              :slots="patient.slots"
              :selected-time="appointmentTime"
              @select-slot="handleSelectSlot"
            />
          </PatientDoctorCard>
        </div>

        <BaseButton
          block
          variant="primary"
          :disabled="!doctorId || !appointmentDate || !appointmentTime || !selectedSlotId"
          @click="nextStep"
        >
          Tiếp theo: Điền triệu chứng →
        </BaseButton>
      </section>

      <!-- STEP 2 -->
      <section v-if="step === 2" class="space-y-4">
        <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
          <h2 class="mb-4 font-bold text-slate-800 text-base">
            Thông tin bệnh nhân
          </h2>

          <div class="space-y-4">
            <div>
              <label class="mb-1.5 block text-xs font-bold text-slate-700 uppercase tracking-wider">
                Họ và tên bệnh nhân
              </label>
              <input
                :value="patient.profile?.fullName || auth.user?.name"
                disabled
                class="w-full rounded-xl border border-slate-200 bg-slate-100 px-4 py-2.5 text-sm font-semibold text-slate-700 cursor-not-allowed"
              />
            </div>

            <div>
              <label class="mb-1.5 block text-xs font-bold text-slate-700 uppercase tracking-wider">
                Lý do khám / Triệu chứng bệnh
              </label>
              <textarea
                v-model="symptoms"
                rows="4"
                placeholder="Mô tả ngắn gọn các biểu hiện sức khỏe hiện tại (vd: sốt, đau ngực, ho kéo dài...)"
                class="w-full resize-none rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-800 placeholder-slate-400 focus:border-[#0E4D92] focus:outline-none focus:ring-1 focus:ring-[#0E4D92]"
              />
            </div>
          </div>
        </div>

        <div class="flex gap-3">
          <BaseButton
            variant="outline"
            class="w-1/3"
            @click="previousStep"
          >
            ← Quay lại
          </BaseButton>

          <BaseButton
            variant="primary"
            class="w-2/3"
            @click="nextStep"
          >
            Tiếp theo: Xem lại & Xác nhận →
          </BaseButton>
        </div>
      </section>

      <!-- STEP 3 -->
      <section v-if="step === 3" class="space-y-4">
        <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
          <h2 class="mb-4 font-bold text-slate-800 text-base">
            Kiểm tra và xác nhận thông tin lịch khám
          </h2>

          <div class="grid grid-cols-2 gap-3">
            <div class="rounded-xl bg-blue-50/70 border border-blue-100 p-3.5">
              <p class="text-xs font-semibold text-blue-700">Bác sĩ khám</p>
              <p class="text-sm font-bold text-slate-800 mt-0.5">
                {{ selectedDoctor?.name }}
              </p>
            </div>

            <div class="rounded-xl bg-blue-50/70 border border-blue-100 p-3.5">
              <p class="text-xs font-semibold text-blue-700">Chuyên khoa</p>
              <p class="text-sm font-bold text-slate-800 mt-0.5">
                {{ formatSpecialtyName(selectedDoctor?.specialty) }}
              </p>
            </div>

            <div class="rounded-xl bg-blue-50/70 border border-blue-100 p-3.5">
              <p class="text-xs font-semibold text-blue-700">Ngày khám</p>
              <p class="text-sm font-bold text-slate-800 mt-0.5">
                {{ appointmentDate }}
              </p>
            </div>

            <div class="rounded-xl bg-blue-50/70 border border-blue-100 p-3.5">
              <p class="text-xs font-semibold text-blue-700">Giờ hẹn khám</p>
              <p class="text-base font-extrabold text-[#0E4D92] mt-0.5">
                {{ appointmentTime }}
              </p>
            </div>
          </div>

          <div v-if="symptoms" class="mt-4 border-t border-slate-100 pt-3.5">
            <p class="text-xs font-semibold text-slate-500 uppercase tracking-wider">
              Lý do khám đã ghi nhận:
            </p>
            <p class="mt-1 text-sm font-medium text-slate-800 italic bg-slate-50 p-3 rounded-xl border border-slate-100">
              "{{ symptoms }}"
            </p>
          </div>
        </div>

        <div class="flex gap-3">
          <BaseButton
            variant="outline"
            class="w-1/3"
            @click="previousStep"
          >
            ← Quay lại
          </BaseButton>

          <BaseButton
            variant="primary"
            class="w-2/3"
            :loading="patient.appointmentsLoading"
            loading-text="Đang đặt lịch..."
            @click="confirmBooking"
          >
            ✓ Xác nhận đặt lịch
          </BaseButton>
        </div>

        <BaseAlert
          v-if="patient.appointmentsError"
          type="error"
          :message="patient.appointmentsError"
        />
      </section>
    </template>
  </div>
</template>