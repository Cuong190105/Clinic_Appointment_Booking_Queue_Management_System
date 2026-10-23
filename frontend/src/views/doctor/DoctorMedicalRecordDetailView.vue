<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseSpinner from '@/components/ui/BaseSpinner.vue'
import BaseModal from '@/components/ui/BaseModal.vue'
import { medicalRecordsApi } from '@/features/medical-records/medical-records.api'
import type { MedicalReportDto } from '@/features/medical-records/medical-records.types'

const route = useRoute()
const router = useRouter()

const reportId = computed(() => String(route.params.id || ''))

const loading = ref(false)
const saving = ref(false)
const errorMessage = ref('')
const successMessage = ref('')
const report = ref<MedicalReportDto | null>(null)

// Form fields
const symptoms = ref('')
const diagnosis = ref('')
const prescription = ref('')
const notes = ref('')

// Finalize confirm modal
const showFinalizeModal = ref(false)

const isFinalized = computed(() => {
  if (!report.value) return false
  return report.value.status === 1 || report.value.status === 'Finalized'
})

async function fetchDetail() {
  if (!reportId.value) return
  loading.value = true
  errorMessage.value = ''
  try {
    const data = await medicalRecordsApi.getById(reportId.value)
    report.value = data
    symptoms.value = data.symptoms || ''
    diagnosis.value = data.diagnosis || ''
    prescription.value = data.prescription || ''
    notes.value = data.notes || ''
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || err?.message || 'Không thể tải chi tiết hồ sơ bệnh án.'
  } finally {
    loading.value = false
  }
}

async function handleSaveDraft() {
  if (!report.value || isFinalized.value) return
  saving.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    const updated = await medicalRecordsApi.update(report.value.id, {
      symptoms: symptoms.value,
      diagnosis: diagnosis.value,
      prescription: prescription.value,
      notes: notes.value,
      isFinalize: false,
    })
    report.value = updated
    successMessage.value = 'Đã lưu bản nháp hồ sơ thành công!'
    setTimeout(() => {
      successMessage.value = ''
    }, 4000)
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || err?.message || 'Lỗi khi lưu bản nháp hồ sơ.'
  } finally {
    saving.value = false
  }
}

async function confirmFinalize() {
  if (!report.value || isFinalized.value) return
  saving.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    const updated = await medicalRecordsApi.update(report.value.id, {
      symptoms: symptoms.value,
      diagnosis: diagnosis.value,
      prescription: prescription.value,
      notes: notes.value,
      isFinalize: true,
    })
    report.value = updated
    showFinalizeModal.value = false
    successMessage.value = 'Hồ sơ đã được chốt và hoàn tất khám bệnh thành công!'
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || err?.message || 'Lỗi khi chốt hồ sơ bệnh án.'
    showFinalizeModal.value = false
  } finally {
    saving.value = false
  }
}

function handlePrint() {
  window.print()
}

function goBack() {
  router.push('/doctor/medical-records')
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

onMounted(() => {
  fetchDetail()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header Controls (Hidden on print) -->
    <div class="no-print flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <div class="flex items-center gap-3">
        <BaseButton
          variant="outline"
          size="sm"
          class="gap-1.5"
          @click="goBack"
        >
          <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M19 12H5M12 19l-7-7 7-7" />
          </svg>
          Quay lại danh sách
        </BaseButton>
        <span class="text-sm font-semibold text-slate-400">/</span>
        <h1 class="text-xl font-bold tracking-tight text-slate-900">
          Chi tiết hồ sơ bệnh án
        </h1>
      </div>

      <!-- Action Buttons -->
      <div class="flex flex-wrap items-center gap-2">
        <!-- Print Button (Visible especially when finalized) -->
        <BaseButton
          v-if="isFinalized"
          variant="outline"
          size="sm"
          class="border-slate-300 gap-1.5 text-slate-700 hover:bg-slate-50"
          @click="handlePrint"
        >
          <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="6 9 6 2 18 2 18 9" />
            <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
            <rect x="6" y="14" width="12" height="8" />
          </svg>
          In hồ sơ bệnh án
        </BaseButton>

        <!-- Draft Actions -->
        <template v-else>
          <BaseButton
            variant="outline"
            size="sm"
            :disabled="saving || loading"
            @click="handleSaveDraft"
          >
            <svg class="mr-1.5 h-4 w-4 text-slate-500" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
              <polyline points="17 21 17 13 7 13 7 21" />
              <polyline points="7 3 7 8 15 8" />
            </svg>
            {{ saving ? 'Đang lưu...' : 'Lưu bản nháp' }}
          </BaseButton>

          <BaseButton
            variant="primary"
            size="sm"
            class="bg-emerald-600 hover:bg-emerald-700"
            :disabled="saving || loading"
            @click="showFinalizeModal = true"
          >
            <svg class="mr-1.5 h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
              <polyline points="22 4 12 14.01 9 11.01" />
            </svg>
            Chốt hồ sơ (Finalize)
          </BaseButton>
        </template>
      </div>
    </div>

    <!-- Alert Banners -->
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

    <!-- Notice if Finalized -->
    <div
      v-if="isFinalized"
      class="no-print flex items-center gap-3 rounded-xl border border-emerald-200 bg-emerald-50/70 p-4 text-sm text-emerald-800"
    >
      <div class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-emerald-600">
        <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <polyline points="20 6 9 17 4 12" />
        </svg>
      </div>
      <div>
        <span class="font-semibold">Hồ sơ đã được chốt (Finalized):</span> Hồ sơ khám bệnh này đã hoàn tất, không được phép chỉnh sửa nội dung nữa. Bác sĩ có thể bấm "In hồ sơ bệnh án" để xuất bản in cho bệnh nhân.
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="flex flex-col items-center justify-center p-16">
      <BaseSpinner class="h-8 w-8 text-violet-600" />
      <p class="mt-2 text-sm text-slate-500">Đang tải chi tiết hồ sơ bệnh án...</p>
    </div>

    <!-- Detail Content / Printable Sheet -->
    <div
      v-else-if="report"
      class="print-sheet rounded-xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8"
    >
      <!-- Print-Only Clinic Header -->
      <div class="print-only mb-6 border-b border-slate-300 pb-4 text-center">
        <h2 class="text-xl font-bold uppercase tracking-wider text-slate-900">PHÒNG KHÁM ĐA KHOA QUỐC TẾ</h2>
        <p class="text-xs text-slate-500">Địa chỉ: 123 Đường Sức Khỏe, Quận 1, TP. Hồ Chí Minh - Hotline: 1900 6868</p>
        <h1 class="mt-4 text-2xl font-extrabold uppercase tracking-tight text-slate-900">PHIẾU KHÁM BỆNH &amp; HỒ SƠ Y TẾ</h1>
      </div>

      <!-- Overview Header Grid -->
      <div class="mb-6 grid grid-cols-1 gap-4 rounded-xl border border-slate-100 bg-slate-50/80 p-4 sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <div class="text-xs font-semibold uppercase tracking-wider text-slate-400">Bệnh nhân</div>
          <div class="mt-1 text-base font-bold text-slate-900">{{ report.patientName || '—' }}</div>
          <div class="text-xs text-slate-500">Mã BN: #{{ report.patientId.slice(0, 8) }}</div>
        </div>

        <div>
          <div class="text-xs font-semibold uppercase tracking-wider text-slate-400">Bác sĩ phụ trách</div>
          <div class="mt-1 text-base font-semibold text-slate-900">{{ report.doctorName || '—' }}</div>
          <div class="text-xs font-medium text-violet-600">{{ report.specialtyName || 'Chuyên khoa' }}</div>
        </div>

        <div>
          <div class="text-xs font-semibold uppercase tracking-wider text-slate-400">Ngày khám &amp; Giờ khám</div>
          <div class="mt-1 text-sm font-semibold text-slate-800">{{ formatDate(report.examDate || report.createdAt) }}</div>
          <div class="text-xs text-slate-500">
            {{ report.examStartTime ? new Date(report.examStartTime).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) : '—' }}
            <span v-if="report.examEndTime"> - {{ new Date(report.examEndTime).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) }}</span>
          </div>
        </div>

        <div>
          <div class="text-xs font-semibold uppercase tracking-wider text-slate-400">Trạng thái hồ sơ</div>
          <div class="mt-1">
            <span
              v-if="isFinalized"
              class="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700 border border-emerald-200"
            >
              <svg class="h-3.5 w-3.5 text-emerald-500" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                <polyline points="20 6 9 17 4 12" />
              </svg>
              Đã chốt (Finalized)
            </span>
            <span
              v-else
              class="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2.5 py-1 text-xs font-semibold text-amber-700 border border-amber-200"
            >
              <svg class="h-3.5 w-3.5 text-amber-500" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <circle cx="12" cy="12" r="10" />
                <line x1="12" y1="8" x2="12" y2="12" />
                <line x1="12" y1="16" x2="12.01" y2="16" />
              </svg>
              Bản nháp (Draft)
            </span>
          </div>
          <div class="mt-1 text-[11px] text-slate-400 font-mono">Mã HS: #{{ report.id.slice(0, 8) }}</div>
        </div>
      </div>

      <!-- Form Inputs / Printable Record Details -->
      <div class="space-y-6">
        <!-- Triệu chứng -->
        <div>
          <label class="mb-1.5 flex items-center gap-1.5 text-sm font-bold text-slate-800">
            <span class="inline-block h-2 w-2 rounded-full bg-violet-600"></span>
            1. Triệu chứng lâm sàng &amp; Lý do khám
          </label>
          <textarea
            v-model="symptoms"
            :disabled="isFinalized"
            rows="3"
            placeholder="Mô tả triệu chứng bệnh nhân khai báo (sốt, ho, đau tức ngực...)..."
            class="w-full rounded-lg border border-slate-300 p-3 text-sm text-slate-900 transition-colors focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500 disabled:bg-slate-50 disabled:text-slate-800 disabled:border-slate-200"
          ></textarea>
        </div>

        <!-- Chẩn đoán -->
        <div>
          <label class="mb-1.5 flex items-center gap-1.5 text-sm font-bold text-slate-800">
            <span class="inline-block h-2 w-2 rounded-full bg-violet-600"></span>
            2. Chẩn đoán của bác sĩ
          </label>
          <textarea
            v-model="diagnosis"
            :disabled="isFinalized"
            rows="3"
            placeholder="Chẩn đoán xác định bệnh lý (kèm mã ICD-10 nếu có)..."
            class="w-full rounded-lg border border-slate-300 p-3 text-sm text-slate-900 transition-colors focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500 disabled:bg-slate-50 disabled:text-slate-800 disabled:border-slate-200"
          ></textarea>
        </div>

        <!-- Đơn thuốc -->
        <div>
          <label class="mb-1.5 flex items-center gap-1.5 text-sm font-bold text-slate-800">
            <span class="inline-block h-2 w-2 rounded-full bg-violet-600"></span>
            3. Đơn thuốc &amp; Hướng dẫn điều trị
          </label>
          <textarea
            v-model="prescription"
            :disabled="isFinalized"
            rows="4"
            placeholder="Tên thuốc, liều dùng, cách dùng (ví dụ: Paracetamol 500mg uống 1v x 2 lần/ngày sau ăn)..."
            class="w-full font-mono rounded-lg border border-slate-300 p-3 text-sm text-slate-900 transition-colors focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500 disabled:bg-slate-50 disabled:text-slate-800 disabled:border-slate-200"
          ></textarea>
        </div>

        <!-- Ghi chú / Lời dặn -->
        <div>
          <label class="mb-1.5 flex items-center gap-1.5 text-sm font-bold text-slate-800">
            <span class="inline-block h-2 w-2 rounded-full bg-violet-600"></span>
            4. Lời dặn của bác sĩ &amp; Hẹn tái khám
          </label>
          <textarea
            v-model="notes"
            :disabled="isFinalized"
            rows="2"
            placeholder="Chế độ ăn uống, sinh hoạt, ngày hẹn tái khám nếu có..."
            class="w-full rounded-lg border border-slate-300 p-3 text-sm text-slate-900 transition-colors focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500 disabled:bg-slate-50 disabled:text-slate-800 disabled:border-slate-200"
          ></textarea>
        </div>
      </div>

      <!-- Print-Only Signature Section -->
      <div class="print-only mt-12 grid grid-cols-2 pt-6 text-center">
        <div>
          <p class="font-bold text-slate-700">BỆNH NHÂN / NGƯỜI NHÀ</p>
          <p class="text-xs italic text-slate-400">(Ký và ghi rõ họ tên)</p>
          <div class="h-20"></div>
          <p class="font-semibold text-slate-800">{{ report.patientName }}</p>
        </div>
        <div>
          <p class="text-xs text-slate-500">Ngày {{ new Date().getDate() }} tháng {{ new Date().getMonth() + 1 }} năm {{ new Date().getFullYear() }}</p>
          <p class="font-bold text-slate-700">BÁC SĨ KHÁM BỆNH</p>
          <p class="text-xs italic text-slate-400">(Ký và ghi rõ họ tên)</p>
          <div class="h-20"></div>
          <p class="font-semibold text-slate-800">{{ report.doctorName }}</p>
        </div>
      </div>
    </div>

    <!-- Finalize Confirmation Modal -->
    <BaseModal
      :open="showFinalizeModal"
      title="Xác nhận chốt hồ sơ bệnh án"
      size="md"
      @close="showFinalizeModal = false"
    >
      <div class="space-y-3">
        <p class="text-sm text-slate-600">
          Bạn có chắc chắn muốn <span class="font-bold text-slate-900">hoàn tất và chốt hồ sơ bệnh án</span> này không?
        </p>
        <div class="rounded-lg bg-amber-50 p-3 text-xs text-amber-800 border border-amber-200">
          <span class="font-bold">Lưu ý quan trọng:</span> Sau khi chốt hồ sơ, trạng thái ca khám sẽ được đánh dấu hoàn tất và hồ sơ sẽ bị khóa, không thể sửa đổi được nữa.
        </div>
      </div>

      <template #footer>
        <div class="flex items-center justify-end gap-2">
          <BaseButton
            variant="outline"
            size="sm"
            :disabled="saving"
            @click="showFinalizeModal = false"
          >
            Hủy bỏ
          </BaseButton>
          <BaseButton
            variant="primary"
            size="sm"
            class="bg-emerald-600 hover:bg-emerald-700"
            :disabled="saving"
            @click="confirmFinalize"
          >
            {{ saving ? 'Đang xử lý...' : 'Xác nhận chốt hồ sơ' }}
          </BaseButton>
        </div>
      </template>
    </BaseModal>
  </div>
</template>

<style scoped>
.print-only {
  display: none;
}

@media print {
  .no-print {
    display: none !important;
  }

  .print-only {
    display: block !important;
  }

  .print-sheet {
    border: none !important;
    box-shadow: none !important;
    padding: 0 !important;
    margin: 0 !important;
  }

  textarea {
    border: 1px solid #cbd5e1 !important;
    resize: none !important;
    background-color: transparent !important;
    color: #0f172a !important;
    overflow: visible !important;
  }
}
</style>
