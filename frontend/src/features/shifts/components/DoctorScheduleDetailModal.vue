<script setup lang="ts">
import { ref, watch } from 'vue'
import BaseModal from '@/components/ui/BaseModal.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import { shiftsApi } from '../shifts.api'
import type { WorkSchedule } from '../shifts.types'
import type { Doctor } from '@/features/doctors/doctors.types'

const props = defineProps<{
  show: boolean
  doctor: Doctor | null
  startDate: string
  endDate: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'updated'): void
}>()

const loading = ref(false)
const actionLoading = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)
const schedules = ref<WorkSchedule[]>([])

watch(
  () => [props.show, props.doctor?.id, props.startDate, props.endDate],
  async ([show]) => {
    if (show && props.doctor?.id) {
      await loadSchedules()
    }
  },
  { immediate: true },
)

async function loadSchedules() {
  if (!props.doctor?.id) return
  loading.value = true
  errorMessage.value = null
  try {
    const res = await shiftsApi.getDoctorShifts(props.doctor.id, props.startDate, props.endDate)
    schedules.value = res.schedules || []
  } catch (err: any) {
    errorMessage.value = err.message || 'Không thể tải lịch làm việc của bác sĩ.'
  } finally {
    loading.value = false
  }
}

async function handleCancelShift(shiftId: string) {
  const reason = prompt('Nhập lý do hủy ca làm việc này:')
  if (reason === null) return

  actionLoading.value = shiftId
  errorMessage.value = null
  successMessage.value = null
  try {
    await shiftsApi.cancelShift(shiftId, reason || 'Admin hủy ca')
    successMessage.value = 'Đã hủy ca làm việc thành công.'
    await loadSchedules()
    emit('updated')
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || err.message || 'Không thể hủy ca trực.'
  } finally {
    actionLoading.value = null
  }
}

function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  try {
    const d = new Date(dateStr)
    return d.toLocaleDateString('vi-VN', { weekday: 'short', day: '2-digit', month: '2-digit', year: 'numeric' })
  } catch {
    return dateStr
  }
}
</script>

<template>
  <BaseModal
    :open="show"
    :title="`Lịch làm việc chi tiết: BS. ${doctor?.fullName || doctor?.name || ''}`"
    size="2xl"
    @close="emit('close')"
  >
    <div class="space-y-4">
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

      <div class="flex items-center justify-between rounded-xl bg-slate-50 p-4 border border-slate-100">
        <div>
          <p class="text-sm font-bold text-slate-800">
            {{ doctor?.fullName || doctor?.name }}
          </p>
          <p class="text-xs text-slate-500 mt-0.5">
            Chuyên khoa: <span class="font-semibold text-violet-700">{{ doctor?.specialty || 'Chưa phân khoa' }}</span>
          </p>
        </div>
        <div class="text-right">
          <span class="inline-block rounded-full bg-violet-100 px-3 py-1 text-xs font-bold text-violet-700">
            {{ schedules.length }} ca trực trong tuần
          </span>
        </div>
      </div>

      <!-- Loading State -->
      <div v-if="loading" class="py-12 text-center text-sm text-slate-400">
        Đang tải lịch làm việc...
      </div>

      <!-- Empty State -->
      <div v-else-if="schedules.length === 0" class="rounded-xl border border-dashed border-slate-200 py-10 text-center text-sm text-slate-500">
        Chưa có ca làm việc nào được xếp cho bác sĩ trong khoảng thời gian này.
      </div>

      <!-- Schedules List -->
      <div v-else class="divide-y divide-slate-100 max-h-96 overflow-y-auto pr-1">
        <div
          v-for="shift in schedules"
          :key="shift.id"
          class="flex items-center justify-between py-3.5 hover:bg-slate-50/80 px-2 rounded-lg transition-colors"
        >
          <div>
            <div class="flex items-center gap-2">
              <span class="font-bold text-sm text-slate-800">
                {{ formatDate(shift.date) }}
              </span>
              <span
                class="rounded-md px-2 py-0.5 text-xs font-bold"
                :class="
                  shift.status === 'Active' || shift.status === 0
                    ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                    : 'bg-rose-50 text-rose-700 border border-rose-200'
                "
              >
                {{ shift.status === 'Active' || shift.status === 0 ? 'Đang hoạt động' : 'Đã hủy' }}
              </span>
            </div>
            <div class="mt-1 flex items-center gap-4 text-xs text-slate-500">
              <span>🕒 Giờ khám: <b class="text-slate-700">{{ shift.startTime }} - {{ shift.endTime }}</b></span>
              <span>👥 Giới hạn: <b class="text-slate-700">{{ shift.patientLimit }} bệnh nhân</b></span>
            </div>
          </div>

          <div>
            <BaseButton
              v-if="shift.status === 'Active' || shift.status === 0"
              size="sm"
              variant="outline"
              class="border-rose-200 text-rose-600 hover:bg-rose-50 hover:border-rose-300 text-xs"
              :loading="actionLoading === shift.id"
              @click="handleCancelShift(shift.id)"
            >
              Hủy ca
            </BaseButton>
          </div>
        </div>
      </div>

      <div class="flex justify-end pt-4 border-t border-slate-100">
        <BaseButton
          variant="outline"
          @click="emit('close')"
        >
          Đóng
        </BaseButton>
      </div>
    </div>
  </BaseModal>
</template>
