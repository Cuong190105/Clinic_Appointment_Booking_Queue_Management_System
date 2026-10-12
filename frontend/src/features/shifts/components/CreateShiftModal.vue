<script setup lang="ts">
import { ref, reactive, watch, computed } from 'vue'
import BaseModal from '@/components/ui/BaseModal.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseInput from '@/components/ui/BaseInput.vue'
import BaseSelect from '@/components/ui/BaseSelect.vue'
import { shiftsApi } from '../shifts.api'
import type { Doctor } from '@/features/doctors/doctors.types'

const props = defineProps<{
  show: boolean
  doctors: Doctor[]
  defaultDoctorId?: string
  defaultDate?: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'saved'): void
}>()

const loading = ref(false)
const errorMessage = ref<string | null>(null)

const todayDateStr = new Date().toLocaleDateString('sv-SE')

const form = reactive({
  doctorId: '',
  date: '',
  startTime: '07:30',
  endTime: '11:30',
  patientLimit: 20,
})

watch(
  () => props.show,
  (val) => {
    if (val) {
      errorMessage.value = null
      const firstDocId = props.doctors.length > 0 && props.doctors[0] ? props.doctors[0].id : ''
      form.doctorId = props.defaultDoctorId || firstDocId

      // Nếu defaultDate nhỏ hơn ngày hôm nay thì chọn ngày hôm nay
      const initialDate = props.defaultDate && props.defaultDate >= todayDateStr ? props.defaultDate : todayDateStr
      form.date = initialDate

      // Nếu ngày là hôm nay, tự động gợi ý ca làm việc ở tương lai
      const now = new Date()
      const currentHours = now.getHours()
      if (initialDate === todayDateStr && currentHours >= 11) {
        // Chiều
        form.startTime = '13:30'
        form.endTime = '17:00'
      } else {
        form.startTime = '07:30'
        form.endTime = '11:30'
      }

      form.patientLimit = 20
    }
  },
  { immediate: true },
)

function setPresetShift(type: 'morning' | 'afternoon' | 'fullday') {
  if (type === 'morning') {
    form.startTime = '07:30'
    form.endTime = '11:30'
  } else if (type === 'afternoon') {
    form.startTime = '13:30'
    form.endTime = '17:00'
  } else {
    form.startTime = '07:30'
    form.endTime = '17:00'
  }
}

async function handleSubmit() {
  if (!form.doctorId) {
    errorMessage.value = 'Vui lòng chọn bác sĩ.'
    return
  }
  if (!form.date) {
    errorMessage.value = 'Vui lòng chọn ngày làm việc.'
    return
  }
  if (!form.startTime || !form.endTime) {
    errorMessage.value = 'Vui lòng nhập đầy đủ giờ bắt đầu và giờ kết thúc.'
    return
  }
  if (form.startTime >= form.endTime) {
    errorMessage.value = 'Giờ kết thúc phải sau giờ bắt đầu.'
    return
  }
  if (form.patientLimit < 1 || form.patientLimit > 50) {
    errorMessage.value = 'Giới hạn bệnh nhân phải từ 1 đến 50.'
    return
  }

  // Kiểm tra ca trực ở quá khứ
  if (form.date < todayDateStr) {
    errorMessage.value = 'Không thể tạo ca làm việc trong quá khứ. Vui lòng chọn từ hôm nay trở đi.'
    return
  }

  if (form.date === todayDateStr) {
    const now = new Date()
    const currentHhMm = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`
    if (form.startTime <= currentHhMm) {
      errorMessage.value = `Giờ bắt đầu (${form.startTime}) đã qua so với giờ hiện tại (${currentHhMm}). Vui lòng chọn ca làm việc tiếp theo hoặc ngày mai.`
      return
    }
  }

  loading.value = true
  errorMessage.value = null

  try {
    await shiftsApi.createDoctorShift({
      doctorId: form.doctorId,
      date: form.date,
      startTime: form.startTime,
      endTime: form.endTime,
      patientLimit: Number(form.patientLimit),
    })
    emit('saved')
    emit('close')
  } catch (err: any) {
    const backendMsg =
      err.response?.data?.errorMessages?.[0] ||
      err.response?.data?.message ||
      err.message ||
      'Không thể tạo ca làm việc. Vui lòng kiểm tra trùng lịch.'
    errorMessage.value = backendMsg
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <BaseModal
    :open="show"
    title="Thêm lịch làm việc cho bác sĩ"
    size="lg"
    @close="emit('close')"
  >
    <form @submit.prevent="handleSubmit" class="space-y-4">
      <BaseAlert
        v-if="errorMessage"
        type="error"
        :message="errorMessage"
        dismissible
        @dismiss="errorMessage = null"
      />

      <!-- Chọn Bác sĩ -->
      <BaseSelect
        v-model="form.doctorId"
        label="Bác sĩ phụ trách"
        required
      >
        <option value="" disabled>-- Chọn bác sĩ --</option>
        <option
          v-for="doc in doctors"
          :key="doc.id"
          :value="doc.id"
        >
          {{ doc.fullName || doc.name }} ({{ doc.specialty || 'Chưa phân khoa' }})
        </option>
      </BaseSelect>

      <!-- Chọn Ngày -->
      <BaseInput
        v-model="form.date"
        type="date"
        label="Ngày làm việc"
        :min="todayDateStr"
        required
      />

      <!-- Khung giờ định sẵn -->
      <div>
        <label class="block text-xs font-semibold text-slate-700 mb-1.5">
          Chọn nhanh ca làm việc
        </label>
        <div class="grid grid-cols-3 gap-2">
          <button
            type="button"
            class="rounded-lg border border-slate-200 px-3 py-2 text-xs font-medium hover:border-violet-500 hover:bg-violet-50 text-slate-700 transition-colors"
            @click="setPresetShift('morning')"
          >
            ☀️ Sáng (07:30 - 11:30)
          </button>
          <button
            type="button"
            class="rounded-lg border border-slate-200 px-3 py-2 text-xs font-medium hover:border-violet-500 hover:bg-violet-50 text-slate-700 transition-colors"
            @click="setPresetShift('afternoon')"
          >
            🌤️ Chiều (13:30 - 17:00)
          </button>
          <button
            type="button"
            class="rounded-lg border border-slate-200 px-3 py-2 text-xs font-medium hover:border-violet-500 hover:bg-violet-50 text-slate-700 transition-colors"
            @click="setPresetShift('fullday')"
          >
            📅 Cả ngày (07:30 - 17:00)
          </button>
        </div>
      </div>

      <!-- Giờ bắt đầu & Kết thúc -->
      <div class="grid grid-cols-2 gap-3">
        <BaseInput
          v-model="form.startTime"
          type="time"
          label="Giờ bắt đầu"
          required
        />
        <BaseInput
          v-model="form.endTime"
          type="time"
          label="Giờ kết thúc"
          required
        />
      </div>

      <!-- Giới hạn bệnh nhân -->
      <BaseInput
        v-model="form.patientLimit"
        type="number"
        label="Số lượng bệnh nhân tối đa tiếp nhận"
        min="1"
        max="50"
        required
      />

      <div class="flex justify-end gap-3 pt-4 border-t border-slate-100">
        <BaseButton
          type="button"
          variant="outline"
          :disabled="loading"
          @click="emit('close')"
        >
          Hủy bỏ
        </BaseButton>
        <BaseButton
          type="submit"
          variant="primary"
          :loading="loading"
        >
          Tạo ca trực
        </BaseButton>
      </div>
    </form>
  </BaseModal>
</template>
