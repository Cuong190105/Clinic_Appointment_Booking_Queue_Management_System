<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { doctorsApi } from '@/features/doctors/doctors.api'
import { specialtiesApi } from '@/features/specialties/specialties.api'
import { formatSpecialtyName } from '@/features/specialties/specialties.utils'
import type { CreateDoctorPayload, Doctor, UpdateDoctorPayload } from '@/features/doctors/doctors.types'
import type { Specialty } from '@/features/specialties/specialties.types'
import AdminFilterToolbar from '@/features/admin/components/AdminFilterToolbar.vue'
import AdminPagination from '@/features/admin/components/AdminPagination.vue'
import BaseStatusBadge from '@/components/ui/BaseStatusBadge.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseModal from '@/components/ui/BaseModal.vue'
import DoctorFormDialog from '@/features/doctors/DoctorFormDialog.vue'

// State
const doctors = ref<Doctor[]>([])
const specialties = ref<Specialty[]>([])
const loading = ref(false)
const error = ref('')
const successMessage = ref('')

const search = ref('')
const specialtyFilter = ref('all')
const statusFilter = ref<string>('all')
const currentPage = ref(1)
const pageSize = 8
const totalPages = ref(1)
const totalCount = ref(0)

// Dialogs state
const formDialogOpen = ref(false)
const editingDoctor = ref<Doctor | null>(null)
const submitting = ref(false)

const viewModalOpen = ref(false)
const viewingDoctor = ref<Doctor | null>(null)

const deleteConfirmOpen = ref(false)
const deletingDoctor = ref<Doctor | null>(null)
const deleting = ref(false)

async function loadSpecialties() {
  try {
    const res = await specialtiesApi.list({ pageSize: 100 })
    specialties.value = Array.isArray(res?.items) ? res.items : []
  } catch {
    specialties.value = []
  }
}

async function loadDoctors() {
  loading.value = true
  error.value = ''

  try {
    const res = await doctorsApi.list({
      search: search.value.trim() || undefined,
      specialtyId: specialtyFilter.value !== 'all' ? specialtyFilter.value : undefined,
      status: statusFilter.value !== 'all' ? statusFilter.value : undefined,
      pageNumber: currentPage.value,
      pageSize,
    })

    doctors.value = Array.isArray(res?.items) ? res.items : (Array.isArray(res as any) ? (res as any) : [])
    totalPages.value = res?.totalPages || 1
    totalCount.value = res?.totalCount ?? doctors.value.length
  } catch (err: any) {
    doctors.value = []
    error.value = err?.message || 'Không thể tải danh sách bác sĩ. Vui lòng kiểm tra kết nối Backend.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadSpecialties()
  loadDoctors()
})

watch([search, specialtyFilter, statusFilter], () => {
  if (currentPage.value !== 1) {
    currentPage.value = 1
  } else {
    loadDoctors()
  }
})

watch(currentPage, () => {
  loadDoctors()
})

function resetFilters() {
  search.value = ''
  specialtyFilter.value = 'all'
  statusFilter.value = 'all'
  if (currentPage.value !== 1) {
    currentPage.value = 1
  } else {
    loadDoctors()
  }
}

function openCreate() {
  editingDoctor.value = null
  formDialogOpen.value = true
}

function openEdit(doctor: Doctor) {
  editingDoctor.value = doctor
  formDialogOpen.value = true
}

function openView(doctor: Doctor) {
  viewingDoctor.value = doctor
  viewModalOpen.value = true
}

function confirmDelete(doctor: Doctor) {
  deletingDoctor.value = doctor
  deleteConfirmOpen.value = true
}

async function handleFormSubmit(payload: CreateDoctorPayload | UpdateDoctorPayload) {
  submitting.value = true
  error.value = ''

  try {
    if (editingDoctor.value) {
      await doctorsApi.update(editingDoctor.value.id, payload as UpdateDoctorPayload)
      successMessage.value = `Đã cập nhật thông tin bác sĩ ${payload.fullName} thành công!`
    } else {
      await doctorsApi.create(payload as CreateDoctorPayload)
      successMessage.value = `Đã tạo mới bác sĩ ${payload.fullName} thành công!`
    }

    formDialogOpen.value = false
    await loadDoctors()

    setTimeout(() => {
      successMessage.value = ''
    }, 4000)
  } catch (err: any) {
    error.value = err?.response?.data?.message || err?.message || 'Thao tác không thành công.'
  } finally {
    submitting.value = false
  }
}

async function handleDelete() {
  if (!deletingDoctor.value) return

  deleting.value = true
  try {
    await doctorsApi.delete(deletingDoctor.value.id)
    successMessage.value = `Đã ngừng hoạt động bác sĩ ${deletingDoctor.value.fullName || deletingDoctor.value.name}!`
    deleteConfirmOpen.value = false
    await loadDoctors()

    setTimeout(() => {
      successMessage.value = ''
    }, 4000)
  } catch (err: any) {
    error.value = err?.message || 'Không thể xóa bác sĩ.'
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <div class="space-y-5 font-sans">
    <!-- Header Title -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-slate-800 tracking-tight">Quản lý Bác sĩ</h1>
        <p class="mt-1 text-sm text-slate-500">
          Danh sách đội ngũ bác sĩ, chuyên khoa công tác và quản lý hồ sơ chuyên môn
        </p>
      </div>

      <BaseButton
        type="button"
        variant="primary"
        size="md"
        @click="openCreate"
      >
        + Thêm Bác sĩ
      </BaseButton>
    </div>

    <!-- Alert thông báo -->
    <div v-if="successMessage">
      <BaseAlert type="success" :message="successMessage" dismissible @dismiss="successMessage = ''" />
    </div>
    <div v-if="error">
      <BaseAlert type="error" :message="error" dismissible @dismiss="error = ''" />
    </div>

    <!-- Filter Toolbar -->
    <AdminFilterToolbar
      v-model:search="search"
      search-placeholder="Tìm kiếm theo tên bác sĩ, số CCHN..."
      add-button-label="+ Thêm Bác sĩ"
      :show-add-button="false"
      @reset="resetFilters"
      @add="openCreate"
    >
      <template #filters>
        <!-- Specialty Filter -->
        <select
          v-model="specialtyFilter"
          class="rounded-xl border border-slate-200 bg-slate-50/50 px-3 py-2 text-xs font-medium text-slate-700 focus:border-[#0E4D92] focus:bg-white focus:outline-none transition-colors"
        >
          <option value="all">Tất cả chuyên khoa</option>
          <option
            v-for="s in specialties"
            :key="s.id"
            :value="s.id"
          >
            {{ formatSpecialtyName(s.name) }}
          </option>
        </select>

        <!-- Status Filter -->
        <select
          v-model="statusFilter"
          class="rounded-xl border border-slate-200 bg-slate-50/50 px-3 py-2 text-xs font-medium text-slate-700 focus:border-[#0E4D92] focus:bg-white focus:outline-none transition-colors"
        >
          <option value="all">Tất cả trạng thái</option>
          <option value="0">Đang hoạt động</option>
          <option value="1">Ngừng hoạt động</option>
        </select>
      </template>
    </AdminFilterToolbar>

    <!-- Table Container -->
    <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-2xs">
      <div class="mb-4 flex items-center justify-between">
        <div>
          <h2 class="text-base font-bold text-slate-800">Danh sách Bác sĩ</h2>
          <p class="text-xs text-slate-400 mt-0.5">Hiển thị thông tin chứng chỉ và chuyên khoa</p>
        </div>
      </div>

      <div class="overflow-x-auto rounded-xl border border-slate-100">
        <!-- Loading State -->
        <div v-if="loading" class="py-16 text-center text-slate-400 text-sm">
          <div class="inline-block animate-spin rounded-full h-8 w-8 border-3 border-[#0E4D92] border-t-transparent mb-2"></div>
          <p>Đang tải danh sách bác sĩ…</p>
        </div>

        <!-- Table -->
        <table v-else class="w-full text-left text-sm border-collapse">
          <thead>
            <tr class="border-b border-slate-100 bg-slate-50/75 text-xs font-semibold uppercase tracking-wider text-slate-500">
              <th class="px-4 py-3.5">#</th>
              <th class="px-4 py-3.5">Bác sĩ</th>
              <th class="px-4 py-3.5">Chuyên khoa</th>
              <th class="px-4 py-3.5">Số CCHN</th>
              <th class="px-4 py-3.5">Trình độ</th>
              <th class="px-4 py-3.5">Kinh nghiệm</th>
              <th class="px-4 py-3.5">Trạng thái</th>
              <th class="px-4 py-3.5 text-right">Thao tác</th>
            </tr>
          </thead>

          <tbody class="divide-y divide-slate-100">
            <tr
              v-for="(doctor, index) in doctors"
              :key="doctor.id"
              class="hover:bg-slate-50/80 transition-colors"
            >
              <td class="px-4 py-3.5 text-xs text-slate-400 font-mono">
                {{ (currentPage - 1) * pageSize + index + 1 }}
              </td>

              <!-- Bác sĩ -->
              <td class="px-4 py-3.5">
                <div class="font-bold text-slate-800">
                  {{ doctor.fullName || doctor.name }}
                </div>
                <div class="text-xs text-slate-400 mt-0.5 flex items-center gap-1.5">
                  <span>{{ doctor.phoneNumber || '—' }}</span>
                  <span v-if="doctor.email">· {{ doctor.email }}</span>
                </div>
              </td>

              <!-- Chuyên khoa -->
              <td class="px-4 py-3.5">
                <span class="inline-flex items-center rounded-lg bg-blue-50 px-2.5 py-1 text-xs font-semibold text-[#0E4D92]">
                  {{ formatSpecialtyName(doctor.currentSpecialty || doctor.specialty) }}
                </span>
              </td>

              <!-- Số CCHN -->
              <td class="px-4 py-3.5 text-xs font-mono text-slate-600">
                {{ doctor.licenseNumber || 'Chưa cập nhật' }}
              </td>

              <!-- Trình độ -->
              <td class="px-4 py-3.5 text-xs text-slate-600">
                {{ doctor.qualification || 'Bác sĩ' }}
              </td>

              <!-- Kinh nghiệm -->
              <td class="px-4 py-3.5 text-xs font-medium text-slate-700">
                {{ doctor.experienceYears ? `${doctor.experienceYears} năm` : '—' }}
              </td>

              <!-- Trạng thái -->
              <td class="px-4 py-3.5">
                <BaseStatusBadge
                  :status="doctor.status === 0 || doctor.status === 'Active' || doctor.status === 'active' ? 'active' : 'inactive'"
                />
              </td>

              <!-- Actions -->
              <td class="px-4 py-3.5 text-right">
                <div class="inline-flex items-center gap-1.5">
                  <button
                    type="button"
                    class="rounded-lg px-2.5 py-1 text-xs font-semibold text-slate-600 hover:bg-slate-100 transition-colors cursor-pointer"
                    @click="openView(doctor)"
                  >
                    Xem
                  </button>

                  <button
                    type="button"
                    class="rounded-lg px-2.5 py-1 text-xs font-semibold text-[#0E4D92] hover:bg-blue-50 transition-colors cursor-pointer"
                    @click="openEdit(doctor)"
                  >
                    Sửa
                  </button>

                  <button
                    type="button"
                    class="rounded-lg px-2.5 py-1 text-xs font-semibold text-rose-600 hover:bg-rose-50 transition-colors cursor-pointer"
                    @click="confirmDelete(doctor)"
                  >
                    Xóa
                  </button>
                </div>
              </td>
            </tr>

            <!-- Empty State -->
            <tr v-if="!doctors || doctors.length === 0">
              <td colspan="8" class="px-4 py-12 text-center text-slate-400 text-sm">
                Không tìm thấy bác sĩ phù hợp với điều kiện tìm kiếm.
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <AdminPagination
        v-model:current-page="currentPage"
        :total-pages="totalPages"
        :total-count="totalCount"
      />
    </div>

    <!-- Doctor Form Dialog (Create / Edit) -->
    <DoctorFormDialog
      :open="formDialogOpen"
      :doctor="editingDoctor"
      :submitting="submitting"
      @close="formDialogOpen = false"
      @submit="handleFormSubmit"
    />

    <!-- Doctor Details Modal -->
    <BaseModal
      :open="viewModalOpen"
      title="Thông tin Bác sĩ"
      size="md"
      icon="👨‍⚕️"
      @close="viewModalOpen = false"
    >
      <div v-if="viewingDoctor" class="space-y-4 text-sm">
        <div class="flex items-center gap-3.5 pb-4 border-b border-slate-100">
          <div class="w-12 h-12 rounded-full bg-blue-100 text-[#0E4D92] flex items-center justify-center font-bold text-lg">
            {{ (viewingDoctor.fullName || viewingDoctor.name || 'D').charAt(0).toUpperCase() }}
          </div>
          <div>
            <h3 class="text-base font-bold text-slate-800">{{ viewingDoctor.fullName || viewingDoctor.name }}</h3>
            <p class="text-xs text-slate-500">{{ viewingDoctor.qualification || 'Bác sĩ chuyên khoa' }} · {{ viewingDoctor.currentSpecialty || viewingDoctor.specialty || 'Đa khoa' }}</p>
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3 text-xs">
          <div>
            <span class="text-slate-400 block">Số điện thoại:</span>
            <span class="font-medium text-slate-700">{{ viewingDoctor.phoneNumber || '—' }}</span>
          </div>
          <div>
            <span class="text-slate-400 block">Email:</span>
            <span class="font-medium text-slate-700">{{ viewingDoctor.email || '—' }}</span>
          </div>
          <div>
            <span class="text-slate-400 block">Số CCHN:</span>
            <span class="font-medium text-slate-700 font-mono">{{ viewingDoctor.licenseNumber || '—' }}</span>
          </div>
          <div>
            <span class="text-slate-400 block">Kinh nghiệm:</span>
            <span class="font-medium text-slate-700">{{ viewingDoctor.experienceYears }} năm</span>
          </div>
        </div>

        <div v-if="viewingDoctor.biography" class="pt-3 border-t border-slate-100">
          <span class="text-xs text-slate-400 block mb-1">Tiểu sử chuyên môn:</span>
          <p class="text-xs text-slate-600 leading-relaxed bg-slate-50 p-3 rounded-xl">{{ viewingDoctor.biography }}</p>
        </div>
      </div>
    </BaseModal>

    <!-- Delete Confirmation Modal -->
    <BaseModal
      :open="deleteConfirmOpen"
      title="Xác nhận thao tác"
      size="sm"
      icon="⚠️"
      @close="deleteConfirmOpen = false"
    >
      <p class="text-sm text-slate-600">
        Bạn có chắc chắn muốn ngừng hoạt động bác sĩ <strong class="text-slate-800">{{ deletingDoctor?.fullName || deletingDoctor?.name }}</strong> không?
      </p>
      <template #footer>
        <BaseButton variant="outline" size="sm" @click="deleteConfirmOpen = false">Hủy</BaseButton>
        <BaseButton variant="danger" size="sm" :loading="deleting" @click="handleDelete">Xác nhận</BaseButton>
      </template>
    </BaseModal>
  </div>
</template>
