<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseInput from '@/components/ui/BaseInput.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseSpinner from '@/components/ui/BaseSpinner.vue'
import { medicalRecordsApi } from '@/features/medical-records/medical-records.api'
import type { MedicalReportDto } from '@/features/medical-records/medical-records.types'

const router = useRouter()

const loading = ref(false)
const errorMessage = ref('')
const reports = ref<MedicalReportDto[]>([])

// Filter states
const search = ref('')
const statusFilter = ref<string>('all')
const fromDate = ref('')
const toDate = ref('')
const pageNumber = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const totalPages = ref(1)

async function fetchReports() {
  loading.value = true
  errorMessage.value = ''
  try {
    const res = await medicalRecordsApi.list({
      search: search.value.trim() || undefined,
      status: statusFilter.value !== 'all' ? Number(statusFilter.value) : undefined,
      fromDate: fromDate.value || undefined,
      toDate: toDate.value || undefined,
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
    })

    reports.value = res.items
    totalCount.value = res.totalCount
    totalPages.value = res.totalPages
    pageNumber.value = res.pageNumber
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || err?.message || 'Không thể tải danh sách hồ sơ bệnh án.'
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pageNumber.value = 1
  fetchReports()
}

function handleReset() {
  search.value = ''
  statusFilter.value = 'all'
  fromDate.value = ''
  toDate.value = ''
  pageNumber.value = 1
  fetchReports()
}

function goToPage(page: number) {
  if (page >= 1 && page <= totalPages.value) {
    pageNumber.value = page
    fetchReports()
  }
}

function viewDetail(id: string) {
  router.push(`/doctor/medical-records/${id}`)
}

function isFinalized(status: any): boolean {
  return status === 1 || status === 'Finalized'
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
  fetchReports()
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">
          Hồ sơ bệnh án
        </h1>
        <p class="text-sm text-slate-500">
          Tra cứu, quản lý và theo dõi hồ sơ khám bệnh của bệnh nhân
        </p>
      </div>

      <div class="flex items-center gap-2">
        <BaseButton
          variant="outline"
          size="sm"
          :disabled="loading"
          @click="fetchReports"
        >
          <svg class="mr-1.5 h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67" />
          </svg>
          Làm mới
        </BaseButton>
      </div>
    </div>

    <!-- Filter Card -->
    <div class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <!-- Search Keyword -->
        <div>
          <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-500">
            Từ khóa tìm kiếm
          </label>
          <div class="relative">
            <input
              v-model="search"
              type="text"
              placeholder="Tên BN, SĐT, chẩn đoán..."
              class="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 placeholder-slate-400 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
              @keyup.enter="handleSearch"
            />
          </div>
        </div>

        <!-- Status Filter -->
        <div>
          <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-500">
            Trạng thái
          </label>
          <select
            v-model="statusFilter"
            class="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
            @change="handleSearch"
          >
            <option value="all">Tất cả trạng thái</option>
            <option value="0">Bản nháp (Draft)</option>
            <option value="1">Đã chốt (Finalized)</option>
          </select>
        </div>

        <!-- Date Range: From Date -->
        <div>
          <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-500">
            Từ ngày
          </label>
          <input
            v-model="fromDate"
            type="date"
            class="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
            @change="handleSearch"
          />
        </div>

        <!-- Date Range: To Date -->
        <div>
          <label class="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-500">
            Đến ngày
          </label>
          <input
            v-model="toDate"
            type="date"
            class="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-violet-500 focus:outline-none focus:ring-1 focus:ring-violet-500"
            @change="handleSearch"
          />
        </div>
      </div>

      <div class="mt-4 flex items-center justify-end gap-2 border-t border-slate-100 pt-3">
        <BaseButton
          variant="ghost"
          size="sm"
          @click="handleReset"
        >
          Đặt lại bộ lọc
        </BaseButton>
        <BaseButton
          variant="primary"
          size="sm"
          :disabled="loading"
          @click="handleSearch"
        >
          <svg class="mr-1.5 h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          Tìm kiếm
        </BaseButton>
      </div>
    </div>

    <!-- Error Alert -->
    <BaseAlert
      v-if="errorMessage"
      variant="danger"
      :message="errorMessage"
      class="mb-4"
    />

    <!-- Main Table -->
    <div class="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
      <div v-if="loading" class="flex flex-col items-center justify-center p-12">
        <BaseSpinner class="h-8 w-8 text-violet-600" />
        <p class="mt-2 text-sm text-slate-500">Đang tải dữ liệu hồ sơ bệnh án...</p>
      </div>

      <div v-else-if="reports.length === 0" class="flex flex-col items-center justify-center p-12 text-center">
        <div class="flex h-14 w-14 items-center justify-center rounded-full bg-slate-100 text-slate-400">
          <svg class="h-8 w-8" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
            <polyline points="14 2 14 8 20 8" />
            <line x1="16" y1="13" x2="8" y2="13" />
            <line x1="16" y1="17" x2="8" y2="17" />
            <polyline points="10 9 9 9 8 9" />
          </svg>
        </div>
        <h3 class="mt-3 text-base font-semibold text-slate-800">Không tìm thấy hồ sơ nào</h3>
        <p class="mt-1 text-sm text-slate-500">
          Hãy thử thay đổi điều kiện tìm kiếm hoặc khoảng thời gian tra cứu.
        </p>
      </div>

      <div v-else class="overflow-x-auto">
        <table class="w-full text-left text-sm">
          <thead class="border-b border-slate-200 bg-slate-50 text-xs font-semibold uppercase tracking-wider text-slate-500">
            <tr>
              <th scope="col" class="px-5 py-3.5">Mã HS / Lịch hẹn</th>
              <th scope="col" class="px-5 py-3.5">Bệnh nhân</th>
              <th scope="col" class="px-5 py-3.5">Khoa / Bác sĩ</th>
              <th scope="col" class="px-5 py-3.5">Ngày khám</th>
              <th scope="col" class="px-5 py-3.5">Chẩn đoán</th>
              <th scope="col" class="px-5 py-3.5 text-center">Trạng thái</th>
              <th scope="col" class="px-5 py-3.5 text-right">Thao tác</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            <tr
              v-for="item in reports"
              :key="item.id"
              class="transition-colors hover:bg-slate-50/70"
            >
              <!-- ID -->
              <td class="px-5 py-4 font-mono text-xs text-slate-600">
                <div class="font-semibold text-slate-800">#{{ item.id.slice(0, 8) }}</div>
                <div class="text-[11px] text-slate-400">LH: {{ item.appointmentId.slice(0, 8) }}</div>
              </td>

              <!-- Patient -->
              <td class="px-5 py-4">
                <div class="font-medium text-slate-900">{{ item.patientName || 'Chưa cập nhật' }}</div>
                <div class="text-xs text-slate-400">Mã BN: #{{ item.patientId.slice(0, 8) }}</div>
              </td>

              <!-- Department & Doctor -->
              <td class="px-5 py-4">
                <div class="text-sm font-medium text-slate-800">{{ item.doctorName || 'Bác sĩ' }}</div>
                <div class="text-xs text-violet-600 font-medium">{{ item.specialtyName || 'Chuyên khoa' }}</div>
              </td>

              <!-- Date -->
              <td class="px-5 py-4 text-slate-700 whitespace-nowrap">
                {{ formatDate(item.examDate || item.createdAt) }}
              </td>

              <!-- Diagnosis -->
              <td class="px-5 py-4 max-w-xs truncate text-slate-700" :title="item.diagnosis || 'Chưa có chẩn đoán'">
                {{ item.diagnosis || '—' }}
              </td>

              <!-- Status Badge -->
              <td class="px-5 py-4 text-center whitespace-nowrap">
                <span
                  v-if="isFinalized(item.status)"
                  class="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700 border border-emerald-200"
                >
                  <svg class="h-3.5 w-3.5 text-emerald-500" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                  Đã chốt
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
                  Bản nháp
                </span>
              </td>

              <!-- Action -->
              <td class="px-5 py-4 text-right whitespace-nowrap">
                <BaseButton
                  variant="outline"
                  size="sm"
                  class="hover:bg-violet-50 hover:text-violet-700 hover:border-violet-300"
                  @click="viewDetail(item.id)"
                >
                  Xem chi tiết
                </BaseButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination Footer -->
      <div
        v-if="reports.length > 0"
        class="flex flex-col items-center justify-between gap-3 border-t border-slate-200 bg-slate-50/60 px-5 py-3 sm:flex-row"
      >
        <div class="text-xs text-slate-500">
          Hiển thị trang <span class="font-medium text-slate-800">{{ pageNumber }}</span> / <span class="font-medium text-slate-800">{{ totalPages }}</span> (Tổng cộng <span class="font-medium text-slate-800">{{ totalCount }}</span> hồ sơ)
        </div>

        <div class="flex items-center gap-1.5">
          <BaseButton
            variant="outline"
            size="sm"
            :disabled="pageNumber <= 1 || loading"
            @click="goToPage(pageNumber - 1)"
          >
            Trang trước
          </BaseButton>

          <BaseButton
            variant="outline"
            size="sm"
            :disabled="pageNumber >= totalPages || loading"
            @click="goToPage(pageNumber + 1)"
          >
            Trang sau
          </BaseButton>
        </div>
      </div>
    </div>
  </div>
</template>

