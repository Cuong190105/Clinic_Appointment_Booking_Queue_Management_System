<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { usePatientStore } from '@/stores/patient'
import { patientApi } from '@/features/patients/patient.api'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'

const auth = useAuthStore()
const patient = usePatientStore()
const router = useRouter()

const saving = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)
const toast = ref<{ visible: boolean; type: 'success' | 'error'; message: string } | null>(null)

function showToast(messageText: string, type: 'success' | 'error' = 'success') {
  toast.value = { visible: true, type, message: messageText }
  window.setTimeout(() => {
    toast.value = null
  }, 3000)
}

function normalizeDateInput(value: unknown): string {
  if (!value) return ''

  if (typeof value === 'string') {
    const text = value.trim()
    if (!text) return ''
    const match = text.match(/^(\d{4}-\d{2}-\d{2})/)
    if (match && match[1]) return match[1]

    const parsed = new Date(text)
    if (!Number.isNaN(parsed.getTime())) {
      return parsed.toISOString().slice(0, 10)
    }

    return text.slice(0, 10)
  }

  if (value instanceof Date) {
    return value.toISOString().slice(0, 10)
  }

  return String(value).slice(0, 10)
}

function normalizeGenderValue(value: unknown): number {
  if (typeof value === 'number' && Number.isFinite(value)) return value

  if (typeof value === 'string') {
    const normalized = value.trim().toLowerCase()
    if (normalized === 'male' || normalized === 'nam') return 0
    if (normalized === 'female' || normalized === 'nu' || normalized === 'nữ') return 1
    if (normalized === 'other' || normalized === 'khac' || normalized === 'khác') return 2

    const numeric = Number(value)
    if (Number.isFinite(numeric)) return numeric
  }

  return 0
}

const form = ref({
  fullName: auth.user?.name ?? '',
  email: auth.user?.email ?? '',
  phoneNumber: (auth.user as any)?.phoneNumber ?? '',
  address: (auth.user as any)?.address ?? '',
  dateOfBirth: normalizeDateInput((auth.user as any)?.dateOfBirth ?? ''),
  gender: normalizeGenderValue((auth.user as any)?.gender ?? 0),
  insuranceNumber: (auth.user as any)?.insuranceNumber ?? '',
  emergencyContact: (auth.user as any)?.emergencyContact ?? '',
})

onMounted(async () => {
  try {
    await patient.loadProfile()
    const profile = patient.profile
    if (!profile) return

    form.value = {
      fullName: profile.fullName ?? auth.user?.name ?? '',
      email: profile.email ?? auth.user?.email ?? '',
      phoneNumber: profile.phoneNumber ?? (auth.user as any)?.phoneNumber ?? '',
      address: profile.address ?? '',
      dateOfBirth: normalizeDateInput(profile.dateOfBirth ?? ''),
      gender: normalizeGenderValue(profile.gender ?? 0),
      insuranceNumber: profile.insuranceNumber ?? '',
      emergencyContact: profile.emergencyContact ?? '',
    }
  } catch {
    // ignore; form stays with auth data if profile endpoint is unavailable
  }
})

async function save() {
  saving.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    const payload = {
      email: form.value.email,
      gender: Number(form.value.gender ?? 0),
      address: form.value.address,
      insuranceNumber: form.value.insuranceNumber || null,
      emergencyContact: form.value.emergencyContact || null,
      fullName: form.value.fullName,
      phoneNumber: form.value.phoneNumber,
      dateOfBirth: form.value.dateOfBirth || null,
    }

    await patientApi.updateMyProfile(payload)
    await patient.loadProfile()

    const msg = 'Đã lưu và cập nhật hồ sơ bệnh nhân thành công!'
    successMessage.value = msg
    showToast(msg, 'success')

    try {
      await auth.ensurePatientProfile()
    } catch {
      // ignore
    }

    setTimeout(() => {
      router.push({ name: 'patient-home' })
    }, 1200)
  } catch (err: any) {
    const status = err?.response?.status ?? 0
    const bodyMsg = err?.response?.data?.message || err?.response?.data?.title || err?.message || 'Lỗi khi lưu hồ sơ'
    const fullErr = `Lỗi (${status}): ${bodyMsg}`
    errorMessage.value = fullErr
    showToast(fullErr, 'error')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-6">
    <!-- FLOATING TOAST -->
    <div
      v-if="toast?.visible"
      class="fixed right-5 top-5 z-50 max-w-sm rounded-2xl border px-4 py-3 shadow-xl backdrop-blur-md transition-all animate-bounce"
      :class="
        toast.type === 'success'
          ? 'border-emerald-200 bg-emerald-50/95 text-emerald-800'
          : 'border-red-200 bg-red-50/95 text-red-800'
      "
    >
      <div class="flex items-center gap-2 text-sm font-semibold">
        <span>{{ toast.type === 'success' ? '✅' : '⚠️' }}</span>
        <span>{{ toast.message }}</span>
      </div>
    </div>

    <!-- HEADER TITLE -->
    <div>
      <h1 class="text-2xl font-bold text-slate-800">
        Hồ sơ bệnh nhân
      </h1>
      <p class="mt-1 text-xs text-slate-500">
        Cập nhật thông tin y tế để liên kết hồ sơ bệnh án điện tử và phục vụ đặt lịch khám chính xác.
      </p>
    </div>

    <!-- NOTICES -->
    <BaseAlert
      v-if="successMessage"
      type="success"
      :message="successMessage"
      dismissible
      @dismiss="successMessage = null"
    />

    <BaseAlert
      v-if="errorMessage"
      type="error"
      :message="errorMessage"
      dismissible
      @dismiss="errorMessage = null"
    />

    <!-- PROFILE FORM -->
    <form @submit.prevent="save" class="rounded-2xl border border-slate-200 bg-white p-6 space-y-5 shadow-xs">
      <!-- FULL NAME (READONLY) -->
      <div>
        <div class="flex items-center justify-between mb-1.5">
          <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider">
            Họ và tên
          </label>
          <span class="text-[10px] font-semibold text-slate-400 bg-slate-100 px-2 py-0.5 rounded-full">
            Định danh tài khoản
          </span>
        </div>
        <input
          v-model="form.fullName"
          readonly
          class="w-full rounded-xl border border-slate-200 bg-slate-50 px-4 py-2.5 text-sm font-semibold text-slate-700 cursor-not-allowed"
          placeholder="Họ và tên đầy đủ"
        />
      </div>

      <!-- EMAIL & PHONE -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
            Email liên hệ
          </label>
          <input
            v-model="form.email"
            type="email"
            class="w-full rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-800 focus:border-[#0E4D92] focus:outline-none focus:ring-1 focus:ring-[#0E4D92]"
            placeholder="example@gmail.com"
          />
        </div>

        <div>
          <div class="flex items-center justify-between mb-1.5">
            <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider">
              Số điện thoại
            </label>
            <span class="text-[10px] font-semibold text-slate-400 bg-slate-100 px-2 py-0.5 rounded-full">
              Chỉ đọc
            </span>
          </div>
          <input
            v-model="form.phoneNumber"
            readonly
            class="w-full rounded-xl border border-slate-200 bg-slate-50 px-4 py-2.5 text-sm font-semibold text-slate-700 cursor-not-allowed"
            placeholder="0912345678"
          />
        </div>
      </div>

      <!-- ADDRESS -->
      <div>
        <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
          Địa chỉ thường trú
        </label>
        <input
          v-model="form.address"
          class="w-full rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-800 focus:border-[#0E4D92] focus:outline-none focus:ring-1 focus:ring-[#0E4D92]"
          placeholder="Số nhà, đường, phường/xã, quận/huyện..."
        />
      </div>

      <!-- DOB & GENDER -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
            Ngày sinh
          </label>
          <input
            type="date"
            v-model="form.dateOfBirth"
            class="w-full rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-800 focus:border-[#0E4D92] focus:outline-none focus:ring-1 focus:ring-[#0E4D92]"
          />
        </div>

        <div>
          <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
            Giới tính
          </label>
          <select
            v-model.number="form.gender"
            class="w-full rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-800 focus:border-[#0E4D92] focus:outline-none focus:ring-1 focus:ring-[#0E4D92]"
          >
            <option :value="0">Nam</option>
            <option :value="1">Nữ</option>
            <option :value="2">Khác</option>
          </select>
        </div>
      </div>

      <!-- INSURANCE NUMBER & EMERGENCY CONTACT -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
            Mã thẻ BHYT (nếu có)
          </label>
          <input
            v-model="form.insuranceNumber"
            type="text"
            class="w-full rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-800 focus:border-[#0E4D92] focus:outline-none focus:ring-1 focus:ring-[#0E4D92]"
            placeholder="VD: DN4010123456789"
          />
        </div>

        <div>
          <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
            Người liên hệ khẩn cấp
          </label>
          <input
            v-model="form.emergencyContact"
            type="text"
            class="w-full rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-800 focus:border-[#0E4D92] focus:outline-none focus:ring-1 focus:ring-[#0E4D92]"
            placeholder="VD: Mẹ - 0987654321"
          />
        </div>
      </div>

      <!-- ACTIONS -->
      <div class="flex items-center gap-3 pt-4 border-t border-slate-100">
        <BaseButton
          type="submit"
          variant="primary"
          :loading="saving"
          loading-text="Đang lưu hồ sơ..."
        >
          <span>💾 Lưu hồ sơ</span>
        </BaseButton>

        <RouterLink
          to="/patient"
          class="px-4 py-2.5 rounded-xl border border-slate-200 text-xs font-semibold text-slate-600 hover:bg-slate-50 transition-colors"
        >
          Hủy bỏ
        </RouterLink>
      </div>
    </form>
  </div>
</template>
