<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { usersApi } from '@/features/users/users.api'
import type { CreateUserInput, UpdateUserInput, User } from '@/features/users/users.types'
import AdminFilterToolbar from '@/features/admin/components/AdminFilterToolbar.vue'
import AdminPagination from '@/features/admin/components/AdminPagination.vue'
import AdminRoleBadge from '@/features/admin/components/AdminRoleBadge.vue'
import BaseStatusBadge from '@/components/ui/BaseStatusBadge.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseModal from '@/components/ui/BaseModal.vue'
import UserFormDialog from '@/features/users/UserFormDialog.vue'

// State
const accounts = ref<User[]>([])
const loading = ref(false)
const error = ref('')
const successMessage = ref('')

const search = ref('')
const roleFilter = ref<string>('all')
const currentPage = ref(1)
const pageSize = 8
const totalPages = ref(1)
const totalCount = ref(0)

// Dialogs state
const formDialogOpen = ref(false)
const editingAccount = ref<User | null>(null)
const submitting = ref(false)

const viewModalOpen = ref(false)
const viewingAccount = ref<User | null>(null)

function getSortedRoles(roles?: string[]): string[] {
  if (!roles || roles.length === 0) return ['Patient']
  const priority: Record<string, number> = {
    admin: 1,
    doctor: 2,
    receptionist: 3,
    patient: 4,
  }
  return [...roles].sort((a, b) => (priority[a.toLowerCase()] || 99) - (priority[b.toLowerCase()] || 99))
}

async function loadAccounts() {
  loading.value = true
  error.value = ''

  try {
    const res = await usersApi.list({
      search: search.value.trim() || undefined,
      role: roleFilter.value !== 'all' ? roleFilter.value : undefined,
      pageNumber: currentPage.value,
      pageSize,
    })

    const rawItems = res?.items || (Array.isArray(res as any) ? (res as any) : [])
    let items: User[] = Array.isArray(rawItems) ? rawItems : []

    if (roleFilter.value !== 'all') {
      items = items.filter((u) =>
        u.roles?.some((r) => r.toLowerCase() === roleFilter.value.toLowerCase()),
      )
    }

    accounts.value = items
    totalPages.value = res?.totalPages || Math.max(1, Math.ceil((res?.totalCount ?? items.length) / pageSize))
    totalCount.value = res?.totalCount ?? items.length
  } catch (err: any) {
    accounts.value = []
    error.value = err?.message || 'Không thể tải danh sách tài khoản. Vui lòng kiểm tra kết nối Backend.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadAccounts()
})

watch([search, roleFilter], () => {
  if (currentPage.value !== 1) {
    currentPage.value = 1
  } else {
    loadAccounts()
  }
})

watch(currentPage, () => {
  loadAccounts()
})

function resetFilters() {
  search.value = ''
  roleFilter.value = 'all'
  if (currentPage.value !== 1) {
    currentPage.value = 1
  } else {
    loadAccounts()
  }
}

function openCreate() {
  editingAccount.value = null
  formDialogOpen.value = true
}

function openEdit(account: User) {
  editingAccount.value = account
  formDialogOpen.value = true
}

function openView(account: User) {
  viewingAccount.value = account
  viewModalOpen.value = true
}

async function handleFormSubmit(payload: CreateUserInput | UpdateUserInput) {
  submitting.value = true
  error.value = ''

  try {
    if (editingAccount.value) {
      await usersApi.update(editingAccount.value.id, payload as UpdateUserInput)
      successMessage.value = `Đã cập nhật thông tin tài khoản ${payload.fullName || editingAccount.value.username} thành công!`
    } else {
      await usersApi.create(payload as CreateUserInput)
      successMessage.value = `Đã tạo mới tài khoản ${(payload as CreateUserInput).username} thành công!`
    }

    formDialogOpen.value = false
    await loadAccounts()

    setTimeout(() => {
      successMessage.value = ''
    }, 4000)
  } catch (err: any) {
    error.value = err?.response?.data?.message || err?.message || 'Thao tác không thành công.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="space-y-5 font-sans">
    <!-- Header Title -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-slate-800 tracking-tight">Quản lý Tài khoản</h1>
        <p class="mt-1 text-sm text-slate-500">
          Danh sách người dùng hệ thống, phân quyền vai trò và quản lý trạng thái tài khoản
        </p>
      </div>

      <BaseButton
        type="button"
        variant="primary"
        size="md"
        @click="openCreate"
      >
        + Thêm tài khoản
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
      search-placeholder="Tìm theo họ tên, username, email, số điện thoại..."
      add-button-label="+ Thêm tài khoản"
      :show-add-button="false"
      @reset="resetFilters"
      @add="openCreate"
    >
      <template #filters>
        <!-- Role Filter -->
        <select
          v-model="roleFilter"
          class="rounded-xl border border-slate-200 bg-slate-50/50 px-3 py-2 text-xs font-medium text-slate-700 focus:border-[#0E4D92] focus:bg-white focus:outline-none transition-colors"
        >
          <option value="all">Tất cả vai trò</option>
          <option value="Admin">Quản trị viên (Admin)</option>
          <option value="Doctor">Bác sĩ (Doctor)</option>
          <option value="Receptionist">Lễ tân (Receptionist)</option>
          <option value="Patient">Bệnh nhân (Patient)</option>
        </select>
      </template>
    </AdminFilterToolbar>

    <!-- Table Container -->
    <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-2xs">
      <div class="mb-4 flex items-center justify-between">
        <div>
          <h2 class="text-base font-bold text-slate-800">Danh sách Người dùng</h2>
          <p class="text-xs text-slate-400 mt-0.5">Hiển thị thông tin tài khoản và vai trò phân quyền</p>
        </div>
      </div>

      <div class="overflow-x-auto rounded-xl border border-slate-100">
        <!-- Loading State -->
        <div v-if="loading" class="py-16 text-center text-slate-400 text-sm">
          <div class="inline-block animate-spin rounded-full h-8 w-8 border-3 border-[#0E4D92] border-t-transparent mb-2"></div>
          <p>Đang tải danh sách tài khoản…</p>
        </div>

        <!-- Table -->
        <table v-else class="w-full text-left text-sm border-collapse">
          <thead>
            <tr class="border-b border-slate-100 bg-slate-50/75 text-xs font-semibold uppercase tracking-wider text-slate-500">
              <th class="px-4 py-3.5">#</th>
              <th class="px-4 py-3.5">Người dùng</th>
              <th class="px-4 py-3.5">Liên hệ</th>
              <th class="px-4 py-3.5">Vai trò</th>
              <th class="px-4 py-3.5">Trạng thái</th>
              <th class="px-4 py-3.5">Ngày tạo</th>
              <th class="px-4 py-3.5 text-right">Thao tác</th>
            </tr>
          </thead>

          <tbody class="divide-y divide-slate-100">
            <tr
              v-for="(account, index) in accounts"
              :key="account.id"
              class="hover:bg-slate-50/80 transition-colors"
            >
              <td class="px-4 py-3.5 text-xs text-slate-400 font-mono">
                {{ (currentPage - 1) * pageSize + index + 1 }}
              </td>

              <!-- Người dùng -->
              <td class="px-4 py-3.5">
                <div class="font-bold text-slate-800">
                  {{ account.fullName || account.name || account.username }}
                </div>
                <div class="text-xs text-slate-400 mt-0.5 font-mono">
                  @{{ account.username }}
                </div>
              </td>

              <!-- Email / SĐT -->
              <td class="px-4 py-3.5 text-xs text-slate-600">
                <div>{{ account.email || '—' }}</div>
                <div class="text-slate-400 mt-0.5">{{ account.phoneNumber || '—' }}</div>
              </td>

              <!-- Role (Hiển thị đầy đủ các vai trò với Lễ tân / Admin ưu tiên trước) -->
              <td class="px-4 py-3.5">
                <div class="flex flex-wrap gap-1">
                  <AdminRoleBadge
                    v-for="role in getSortedRoles(account.roles)"
                    :key="role"
                    :role="role"
                  />
                </div>
              </td>

              <!-- Status -->
              <td class="px-4 py-3.5">
                <BaseStatusBadge
                  :status="account.isActive === false ? 'inactive' : 'active'"
                />
              </td>

              <!-- CreatedAt -->
              <td class="px-4 py-3.5 text-xs text-slate-500 font-mono">
                {{ account.createdAt ? new Date(account.createdAt).toLocaleDateString('vi-VN') : '—' }}
              </td>

              <!-- Actions -->
              <td class="px-4 py-3.5 text-right">
                <div class="inline-flex items-center gap-1.5">
                  <button
                    type="button"
                    class="rounded-lg px-2.5 py-1 text-xs font-semibold text-slate-600 hover:bg-slate-100 transition-colors cursor-pointer"
                    @click="openView(account)"
                  >
                    Xem
                  </button>

                  <button
                    type="button"
                    class="rounded-lg px-2.5 py-1 text-xs font-semibold text-[#0E4D92] hover:bg-blue-50 transition-colors cursor-pointer"
                    @click="openEdit(account)"
                  >
                    Sửa
                  </button>
                </div>
              </td>
            </tr>

            <!-- Empty State -->
            <tr v-if="!accounts || accounts.length === 0">
              <td colspan="7" class="px-4 py-12 text-center text-slate-400 text-sm">
                Không tìm thấy tài khoản phù hợp với điều kiện lọc.
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

    <!-- Account Form Dialog (Create / Edit) -->
    <UserFormDialog
      :open="formDialogOpen"
      :initial="editingAccount"
      :submitting="submitting"
      @close="formDialogOpen = false"
      @submit="handleFormSubmit"
    />

    <!-- Account Details Modal -->
    <BaseModal
      :open="viewModalOpen"
      title="Chi tiết Tài khoản"
      size="md"
      icon="👤"
      @close="viewModalOpen = false"
    >
      <div v-if="viewingAccount" class="space-y-4 text-sm">
        <div class="flex items-center gap-3.5 pb-4 border-b border-slate-100">
          <div class="w-12 h-12 rounded-full bg-[#0E4D92]/10 text-[#0E4D92] flex items-center justify-center font-bold text-lg">
            {{ (viewingAccount.fullName || viewingAccount.username || 'U').charAt(0).toUpperCase() }}
          </div>
          <div>
            <h3 class="text-base font-bold text-slate-800">{{ viewingAccount.fullName || viewingAccount.name || viewingAccount.username }}</h3>
            <p class="text-xs text-slate-500 font-mono">@{{ viewingAccount.username }}</p>
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3 text-xs">
          <div>
            <span class="text-slate-400 block">Số điện thoại:</span>
            <span class="font-medium text-slate-700">{{ viewingAccount.phoneNumber || '—' }}</span>
          </div>
          <div>
            <span class="text-slate-400 block">Email:</span>
            <span class="font-medium text-slate-700">{{ viewingAccount.email || '—' }}</span>
          </div>
          <div>
            <span class="text-slate-400 block mb-1">Vai trò:</span>
            <div class="flex flex-wrap gap-1">
              <AdminRoleBadge
                v-for="role in getSortedRoles(viewingAccount.roles)"
                :key="role"
                :role="role"
              />
            </div>
          </div>
          <div>
            <span class="text-slate-400 block">Trạng thái:</span>
            <BaseStatusBadge :status="viewingAccount.isActive === false ? 'inactive' : 'active'" />
          </div>
        </div>
      </div>
    </BaseModal>
  </div>
</template>
