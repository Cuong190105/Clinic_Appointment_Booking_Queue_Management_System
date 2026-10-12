<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { CreateSpecialtyPayload, Specialty, UpdateSpecialtyPayload } from './specialties.types'
import { validators } from '@/utils/validators'
import BaseModal from '@/components/ui/BaseModal.vue'
import BaseInput from '@/components/ui/BaseInput.vue'
import BaseTextarea from '@/components/ui/BaseTextarea.vue'
import BaseButton from '@/components/ui/BaseButton.vue'

const props = defineProps<{
  open: boolean
  specialty?: Specialty | null
  submitting?: boolean
}>()

const emit = defineEmits<{
  close: []
  submit: [payload: CreateSpecialtyPayload | UpdateSpecialtyPayload]
}>()

const form = reactive({
  name: '',
  description: '',
  establishedDate: '',
})

const fieldErrors = reactive({
  name: '',
  establishedDate: '',
})

watch(
  () => [props.open, props.specialty],
  () => {
    fieldErrors.name = ''
    fieldErrors.establishedDate = ''

    if (props.specialty) {
      form.name = props.specialty.name
      form.description = props.specialty.description || ''
      form.establishedDate = props.specialty.establishedDate || '2020-01-01'
    } else {
      form.name = ''
      form.description = ''
      form.establishedDate = new Date().toISOString().split('T')[0] || '2026-01-01'
    }
  },
  { immediate: true },
)

function validate(): boolean {
  fieldErrors.name = ''
  fieldErrors.establishedDate = ''

  const nameReq = validators.required(form.name, 'Tên chuyên khoa')
  if (!nameReq.isValid) fieldErrors.name = nameReq.message

  const dateReq = validators.required(form.establishedDate, 'Ngày thành lập')
  if (!dateReq.isValid) fieldErrors.establishedDate = dateReq.message

  return !fieldErrors.name && !fieldErrors.establishedDate
}

function handleSubmit() {
  if (!validate()) return

  emit('submit', {
    name: form.name.trim(),
    description: form.description.trim() || null,
    establishedDate: form.establishedDate,
  })
}
</script>

<template>
  <BaseModal
    :open="open"
    :title="specialty ? 'Chỉnh sửa Chuyên khoa' : 'Thêm Chuyên khoa mới'"
    :subtitle="specialty ? 'Cập nhật thông tin chuyên khoa phòng khám' : 'Đăng ký chuyên khoa mới vào danh mục khám bệnh'"
    size="md"
    icon="🏥"
    @close="emit('close')"
  >
    <form @submit.prevent="handleSubmit" class="space-y-4 font-sans" novalidate>
      <!-- Tên chuyên khoa -->
      <BaseInput
        v-model="form.name"
        label="Tên chuyên khoa"
        required
        placeholder="Ví dụ: Tim mạch, Nhi khoa, Tai Mũi Họng..."
        :error="fieldErrors.name"
      />

      <!-- Ngày thành lập -->
      <BaseInput
        v-model="form.establishedDate"
        label="Ngày thành lập"
        type="date"
        required
        :error="fieldErrors.establishedDate"
      />

      <!-- Mô tả -->
      <BaseTextarea
        v-model="form.description"
        label="Mô tả chức năng & phạm vi khám"
        placeholder="Mô tả các dịch vụ, thế mạnh điều trị của chuyên khoa..."
        :rows="3"
      />

      <div class="flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
        <BaseButton
          type="button"
          variant="outline"
          size="md"
          @click="emit('close')"
          :disabled="submitting"
        >
          Hủy bỏ
        </BaseButton>

        <BaseButton
          type="submit"
          variant="primary"
          size="md"
          :loading="submitting"
          :loading-text="specialty ? 'Đang lưu…' : 'Đang tạo…'"
        >
          {{ specialty ? 'Lưu thay đổi' : 'Thêm Chuyên khoa' }}
        </BaseButton>
      </div>
    </form>
  </BaseModal>
</template>
