<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { specialtiesApi } from '@/features/specialties/specialties.api'
import type { CreateSpecialtyPayload, Specialty, UpdateSpecialtyPayload } from '@/features/specialties/specialties.types'
import AdminFilterToolbar from '@/features/admin/components/AdminFilterToolbar.vue'
import AdminPagination from '@/features/admin/components/AdminPagination.vue'
import BaseStatusBadge from '@/components/ui/BaseStatusBadge.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseModal from '@/components/ui/BaseModal.vue'
import SpecialtyFormDialog from '@/features/specialties/SpecialtyFormDialog.vue'

const specialties = ref<Specialty[]>([])
const loading = ref(false)
const error = ref('')
const successMessage = ref('')

const search = ref('')
const currentPage = ref(1)
const pageSize = 8
const totalPages = ref(1)
const totalCount = ref(0)

// Dialogs state
const formDialogOpen = ref(false)
const editingSpecialty = ref<Specialty | null>(null)
const submitting = ref(false)

const toggleConfirmOpen = ref(false)
const togglingSpecialty = ref<Specialty | null>(null)
const toggling = ref(false)

async function loadSpecialties() {
  loading.value = true
  error.value = ''

  try {
    const res = await specialtiesApi.list({
      search: search.value.trim() || undefined,
      pageNumber: currentPage.value,
      pageSize,
    })

    specialties.value = res.items
    totalPages.value = res.totalPages || 1
    totalCount.value = res.totalCount || res.items.length
  } catch (err: any) {
    error.value = err?.message || 'Không thể tải danh sách chuyên khoa.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadSpecialties()
})

watch(search, () => {
  if (currentPage.value !== 1) {
    currentPage.value = 1
  } else {
    loadSpecialties()
  }
})

watch(currentPage, () => {
  loadSpecialties()
})

function resetFilters() {
  search.value = ''
  if (currentPage.value !== 1) {
    currentPage.value = 1
  } else {
    loadSpecialties()
  }
}

function openCreate() {
  editingSpecialty.value = null
  formDialogOpen.value = true
}

function openEdit(specialty: Specialty) {
  editingSpecialty.value = specialty
  formDialogOpen.value = true
}

function confirmToggle(specialty: Specialty) {
  togglingSpecialty.value = specialty
  toggleConfirmOpen.value = true
}

async function handleFormSubmit(payload: CreateSpecialtyPayload | UpdateSpecialtyPayload) {
  submitting.value = true
  error.value = ''

  try {
    if (editingSpecialty.value) {
      await specialtiesApi.update(editingSpecialty.value.id, payload as UpdateSpecialtyPayload)
      successMessage.value = `Đã cập nhật chuyên khoa ${payload.name} thành công!`
    } else {
      await specialtiesApi.create(payload as CreateSpecialtyPayload)
      successMessage.value = `Đã thêm chuyên khoa ${payload.name} thành công!`
    }

    formDialogOpen.value = false
    await loadSpecialties()

    setTimeout(() => {
      successMessage.value = ''
    }, 4000)
  } catch (err: any) {
    error.value = err?.response?.data?.message || err?.message || 'Thao tác không thành công.'
  } finally {
    submitting.value = false
  }
}

async function handleToggleStatus() {
  if (!togglingSpecialty.value) return

  toggling.value = true
  try {
    await specialtiesApi.toggleStatus(togglingSpecialty.value.id)
    successMessage.value = `Đã cập nhật trạng thái chuyên khoa ${togglingSpecialty.value.name}!`
    toggleConfirmOpen.value = false
    await loadSpecialties()

    setTimeout(() => {
      successMessage.value = ''
    }, 4000)
  } catch (err: any) {
    error.value = err?.message || 'Không thể thay đổi trạng thái chuyên khoa.'
  } finally {
    toggling.value = false
  }
}
</script>

<template>
  <div class="space-y-5 font-sans">
    <!-- Header Title -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-slate-800 tracking-tight">Quản lý Chuyên khoa</h1>
        <p class="mt-1 text-sm text-slate-500">
          Danh mục chuyên khoa khám chữa bệnh, thông tin dịch vụ và ngày thành lập
        </p>
      </div>

      <BaseButton
        type="button"
        variant="primary"
        size="md"
        @click="openCreate"
      >
        + Thêm Chuyên khoa
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
      search-placeholder="Tìm kiếm theo tên chuyên khoa..."
      add-button-label="+ Thêm Chuyên khoa"
      :show-add-button="false"
      @reset="resetFilters"
      @add="openCreate"
    />

    <!-- Table Container -->
    <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-2xs">
      <div class="mb-4 flex items-center justify-between">
        <div>
          <h2 class="text-base font-bold text-slate-800">Danh mục Chuyên khoa</h2>
          <p class="text-xs text-slate-400 mt-0.5">Danh sách các khoa phòng đang hoạt động</p>
        </div>
      </div>

      <div class="overflow-x-auto rounded-xl border border-slate-100">
        <!-- Loading State -->
        <div v-if="loading" class="py-16 text-center text-slate-400 text-sm">
          <div class="inline-block animate-spin rounded-full h-8 w-8 border-3 border-[#0E4D92] border-t-transparent mb-2"></div>
          <p>Đang tải danh sách chuyên khoa…</p>
        </div>

        <!-- Table -->
        <table v-else class="w-full text-left text-sm border-collapse">
          <thead>
            <tr class="border-b border-slate-100 bg-slate-50/75 text-xs font-semibold uppercase tracking-wider text-slate-500">
              <th class="px-4 py-3.5">#</th>
              <th class="px-4 py-3.5">Tên chuyên khoa</th>
              <th class="px-4 py-3.5">Mô tả</th>
              <th class="px-4 py-3.5">Ngày thành lập</th>
              <th class="px-4 py-3.5">Trạng thái</th>
              <th class="px-4 py-3.5 text-right">Thao tác</th>
            </tr>
          </thead>

          <tbody class="divide-y divide-slate-100">
            <tr
              v-for="(specialty, index) in specialties"
              :key="specialty.id"
              class="hover:bg-slate-50/80 transition-colors"
            >
              <td class="px-4 py-3.5 text-xs text-slate-400 font-mono">
                {{ (currentPage - 1) * pageSize + index + 1 }}
              </td>

              <!-- Tên chuyên khoa -->
              <td class="px-4 py-3.5">
                <div class="font-bold text-slate-800">
                  {{ specialty.name }}
                </div>
              </td>

              <!-- Mô tả -->
              <td class="px-4 py-3.5 text-xs text-slate-600 max-w-xs truncate">
                {{ specialty.description || 'Chưa có mô tả' }}
              </td>

              <!-- Ngày thành lập -->
              <td class="px-4 py-3.5 text-xs font-mono text-slate-600">
                {{ specialty.establishedDate ? new Date(specialty.establishedDate).toLocaleDateString('vi-VN') : '—' }}
              </td>

              <!-- Trạng thái -->
              <td class="px-4 py-3.5">
                <BaseStatusBadge
                  :status="specialty.status === false ? 'inactive' : 'active'"
                />
              </td>

              <!-- Actions -->
              <td class="px-4 py-3.5 text-right">
                <div class="inline-flex items-center gap-1.5">
                  <button
                    type="button"
                    class="rounded-lg px-2.5 py-1 text-xs font-semibold text-[#0E4D92] hover:bg-blue-50 transition-colors cursor-pointer"
                    @click="openEdit(specialty)"
                  >
                    Sửa
                  </button>

                  <button
                    type="button"
                    class="rounded-lg px-2.5 py-1 text-xs font-semibold text-slate-600 hover:bg-slate-100 transition-colors cursor-pointer"
                    @click="confirmToggle(specialty)"
                  >
                    Đổi trạng thái
                  </button>
                </div>
              </td>
            </tr>

            <!-- Empty State -->
            <tr v-if="specialties.length === 0">
              <td colspan="6" class="px-4 py-12 text-center text-slate-400 text-sm">
                Không tìm thấy chuyên khoa phù hợp.
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

    <!-- Specialty Form Dialog -->
    <SpecialtyFormDialog
      :open="formDialogOpen"
      :specialty="editingSpecialty"
      :submitting="submitting"
      @close="formDialogOpen = false"
      @submit="handleFormSubmit"
    />

    <!-- Toggle Status Confirmation Modal -->
    <BaseModal
      :open="toggleConfirmOpen"
      title="Xác nhận đổi trạng thái"
      size="sm"
      icon="🔄"
      @close="toggleConfirmOpen = false"
    >
      <p class="text-sm text-slate-600">
        Bạn có chắc chắn muốn thay đổi trạng thái hoạt động của chuyên khoa <strong class="text-slate-800">{{ togglingSpecialty?.name }}</strong> không?
      </p>
      <template #footer>
        <BaseButton variant="outline" size="sm" @click="toggleConfirmOpen = false">Hủy</BaseButton>
        <BaseButton variant="primary" size="sm" :loading="toggling" @click="handleToggleStatus">Xác nhận</BaseButton>
      </template>
    </BaseModal>
  </div>
</template>
