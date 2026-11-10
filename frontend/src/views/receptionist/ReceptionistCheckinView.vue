<script setup lang="ts">
import { ref } from 'vue'
import { receptionistApi } from '@/features/receptionist/receptionist.api'
import type {
  AppointmentItem,
  CheckInResponse,
  PatientSearchItem,
} from '@/features/receptionist/receptionist.types'
import CheckInSearchSection from '@/features/receptionist/components/CheckInSearchSection.vue'
import CheckInPatientInfo from '@/features/receptionist/components/CheckInPatientInfo.vue'
import CheckInAppointmentsList from '@/features/receptionist/components/CheckInAppointmentsList.vue'
import CheckInResult from '@/features/receptionist/components/CheckInResult.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'

const selectedPatient = ref<PatientSearchItem | null>(null)
const appointments = ref<AppointmentItem[]>([])
const loadingAppointments = ref(false)
const actionLoadingId = ref<string | null>(null)

const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

// Check-in Result state
const checkInResult = ref<(CheckInResponse & { pending: boolean; queueTicketId?: string; patientName?: string; doctorName?: string }) | null>(null)

async function retryQueueTicketLookup() {
  const appointmentId = checkInResult.value?.appointmentId
  if (!appointmentId) return
  actionLoadingId.value = appointmentId
  try {
    const ticket = await receptionistApi.pollQueueTicketByAppointmentId(appointmentId)
    if (!ticket) return
    checkInResult.value = {
      ...checkInResult.value!,
      pending: false,
      queueTicketId: ticket.id,
      queueNumber: `#${ticket.queueNumber}`,
      patientName: ticket.patientName ?? selectedPatient.value?.fullName,
      doctorName: ticket.doctorName ?? checkInResult.value?.doctorName,
      checkInTime: ticket.checkInTime ?? new Date().toISOString(),
      priority: ticket.priority,
    }
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || err.message || 'Không thể tải phiếu khám. Vui lòng thử lại.'
  } finally {
    actionLoadingId.value = null
  }
}

async function onSelectPatient(patient: PatientSearchItem) {
  selectedPatient.value = patient
  checkInResult.value = null
  errorMessage.value = null
  successMessage.value = null

  loadingAppointments.value = true
  try {
    const list = await receptionistApi.getUpcomingAppointments(patient.id)
    appointments.value = list
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || err.message || 'Không thể tải lịch hẹn của bệnh nhân.'
    appointments.value = []
  } finally {
    loadingAppointments.value = false
  }
}

function onClearPatient() {
  selectedPatient.value = null
  appointments.value = []
  checkInResult.value = null
  errorMessage.value = null
  successMessage.value = null
}

async function handleConfirmAppointment(appt: AppointmentItem) {
  actionLoadingId.value = appt.id
  errorMessage.value = null
  successMessage.value = null

  try {
    await receptionistApi.confirmAppointment(appt.id)
    successMessage.value = 'Đã xác nhận lịch hẹn khám thành công.'
    // Reload upcoming appointments
    if (selectedPatient.value) {
      appointments.value = await receptionistApi.getUpcomingAppointments(selectedPatient.value.id)
    }
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || err.message || 'Xác nhận lịch hẹn thất bại.'
  } finally {
    actionLoadingId.value = null
  }
}

async function handleCheckInAppointment(appt: AppointmentItem) {
  actionLoadingId.value = appt.id
  errorMessage.value = null
  successMessage.value = null
  checkInResult.value = null

  try {
    await receptionistApi.checkInAppointment(appt.id)
    successMessage.value = 'Check-in thành công! Đang đồng bộ số thứ tự...'

    // Poll Queue Service for the actual assigned ticket
    const ticket = await receptionistApi.pollQueueTicketByAppointmentId(appt.id)
    // Reload upcoming appointments
    if (selectedPatient.value) {
      appointments.value = await receptionistApi.getUpcomingAppointments(selectedPatient.value.id)
    }

    if (ticket?.queueNumber) {
      successMessage.value = `Check-in thành công! Đã cấp số thứ tự khám #${ticket.queueNumber}.`
      checkInResult.value = {
        queueNumber: `#${ticket.queueNumber}`,
        appointmentId: appt.id,
        checkInTime: ticket.checkInTime ?? new Date().toISOString(),
        patientName: ticket.patientName ?? selectedPatient.value?.fullName,
        doctorName: ticket.doctorName ?? appt.doctorName ?? '',
        priority: ticket.priority,
        pending: false,
        queueTicketId: ticket.id,
      }
    } else {
      successMessage.value = 'Check-in thành công! Phiếu khám đang được xử lý trong hàng đợi.'
      checkInResult.value = {
        queueNumber: 'Đang xếp lượt',
        appointmentId: appt.id,
        checkInTime: new Date().toISOString(),
        patientName: selectedPatient.value?.fullName,
        doctorName: appt.doctorName ?? '',
        pending: true,
      }
    }
  } catch (err: any) {
    const errorMsg =
      err.response?.data?.message ||
      (Array.isArray(err.response?.data?.errorMessages) && err.response.data.errorMessages.join(', ')) ||
      err.response?.data?.title ||
      err.message ||
      'Check-in thất bại. Vui lòng kiểm tra lại.'
    errorMessage.value = errorMsg
  } finally {
    actionLoadingId.value = null
  }
}

function resetCheckIn() {
  checkInResult.value = null
}
</script>

<template>
  <div class="space-y-6 max-w-5xl">
    <!-- TITLE -->
    <div>
      <h1 class="text-xl font-bold text-slate-800">
        Tiếp đón & Check-in bệnh nhân
      </h1>
      <p class="mt-1 text-xs sm:text-sm text-slate-500">
        Tra cứu hồ sơ bệnh nhân, xác nhận lịch hẹn và cấp số thứ tự vào phòng khám trực tiếp
      </p>
    </div>

    <!-- ALERTS -->
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

    <!-- CHECK-IN RESULT CARD -->
    <div v-if="checkInResult && selectedPatient" class="space-y-4">
      <CheckInResult
        :queue-number="String(checkInResult.queueNumber)"
        :patient-name="checkInResult.patientName || selectedPatient.fullName"
        :doctor-name="checkInResult.doctorName || ''"
        :check-in-time="checkInResult.checkInTime || ''"
        :pending="checkInResult.pending"
        :loading="actionLoadingId === checkInResult.appointmentId"
        @retry="retryQueueTicketLookup"
      />
      <div v-if="!checkInResult.pending" class="text-center">
        <button
          type="button"
          class="text-xs font-semibold text-violet-700 hover:underline cursor-pointer"
          @click="resetCheckIn"
        >
          ← Tiếp tục xử lý cho bệnh nhân này hoặc tra cứu bệnh nhân khác
        </button>
      </div>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
      <!-- LEFT COLUMN: SEARCH & PATIENT INFO -->
      <div class="lg:col-span-5 space-y-5">
        <CheckInSearchSection
          :selected-patient-id="selectedPatient?.id"
          @select-patient="onSelectPatient"
          @clear="onClearPatient"
        />

        <CheckInPatientInfo
          v-if="selectedPatient"
          :patient="selectedPatient"
          @change-patient="onClearPatient"
        />
      </div>

      <!-- RIGHT COLUMN: APPOINTMENTS LIST & ACTIONS -->
      <div class="lg:col-span-7">
        <div v-if="selectedPatient">
          <CheckInAppointmentsList
            :appointments="appointments"
            :loading="loadingAppointments"
            :action-loading-id="actionLoadingId"
            @confirm="handleConfirmAppointment"
            @check-in="handleCheckInAppointment"
          />
        </div>

        <div
          v-else
          class="rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 p-12 text-center text-slate-400 h-full flex flex-col items-center justify-center min-h-[300px]"
        >
          <div class="text-4xl mb-3">🔍</div>
          <div class="font-bold text-slate-600">Chưa chọn bệnh nhân</div>
          <p class="text-xs text-slate-400 mt-1 max-w-xs">
            Vui lòng tìm kiếm và chọn bệnh nhân từ bảng bên trái để hiển thị lịch hẹn và thực hiện check-in
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
