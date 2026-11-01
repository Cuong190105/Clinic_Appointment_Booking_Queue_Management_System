<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { usePatientStore } from '@/stores/patient'
import { supportedLocales, setLocale } from '@/lib/i18n'
import ChangePasswordModal from '@/components/ui/ChangePasswordModal.vue'
import { notificationsApi, type NotificationItemDto } from '@/features/notifications/notifications.api'

const { locale } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const patient = usePatientStore()

const isChangePasswordOpen = ref(false)
const isNotificationsOpen = ref(false)
const unreadCount = ref(0)
const notifications = ref<NotificationItemDto[]>([])
let realtimeConnection: { stop: () => void } | null = null

const currentRole = computed<string>(() => {
  return auth.user?.activeRole ?? auth.currentUserRole ?? auth.user?.role ?? auth.user?.roles?.[0] ?? ''
})

const displayName = computed(() => {
  if (currentRole.value === 'Patient' && patient.profile?.fullName) {
    return patient.profile.fullName
  }
  return auth.user?.name || 'User'
})

async function syncNotifications() {
  if (!auth.isAuthenticated) return
  try {
    const [count, page] = await Promise.all([
      notificationsApi.getUnreadCount(),
      notificationsApi.list({ pageNumber: 1, pageSize: 10 }),
    ])
    unreadCount.value = count
    notifications.value = page.items
  } catch {
    // Ignore transient fetch errors in layout header
  }
}

async function handleMarkRead(item: NotificationItemDto) {
  if (item.isRead) return
  try {
    await notificationsApi.markAsRead(item.id)
    await syncNotifications()
  } catch {
    // Ignore
  }
}

async function handleMarkAllRead() {
  try {
    await notificationsApi.markAllAsRead()
    await syncNotifications()
  } catch {
    // Ignore
  }
}

onMounted(() => {
  if (auth.isAuthenticated) {
    auth.fetchMe().catch(() => undefined)
    void syncNotifications()
    realtimeConnection = notificationsApi.connectRealtimeHints({
      onSyncRequired: () => syncNotifications(),
    })
  }
})

onUnmounted(() => {
  realtimeConnection?.stop()
  realtimeConnection = null
})

function onLocaleChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  setLocale(value)
}

function logout() {
  realtimeConnection?.stop()
  realtimeConnection = null
  auth.logout()
  router.push('/login')
}
</script>

<template>
  <div class="flex min-h-screen bg-slate-50">

    <!-- ================================================= -->
    <!-- SIDEBAR -->
    <!-- ================================================= -->
    <aside
      class="flex h-screen w-64 shrink-0 flex-col
             border-r border-slate-200 bg-white"
    >

      <!-- Logo -->
      <div
        class="flex h-16 items-center
               border-b border-slate-200 px-5"
      >
        <div
          class="flex h-9 w-9 items-center justify-center
                 rounded-lg bg-violet-600
                 text-sm font-bold text-white"
        >
          C
        </div>

        <div class="ml-3">
          <div class="text-sm font-bold text-slate-800">
            Clinic
          </div>

          <div class="mt-0.5 text-xs text-slate-400">
            <template v-if="currentRole === 'Receptionist'">
              Lễ tân
            </template>

            <template v-else-if="currentRole === 'Admin'">
              Quản trị hệ thống
            </template>

            <template v-else-if="currentRole === 'Doctor'">
              Bác sĩ
            </template>

            <template v-else-if="currentRole === 'Patient'">
              Bệnh nhân
            </template>

            <template v-else>
              Clinic System
            </template>
          </div>
        </div>
      </div>


      <!-- ================================================= -->
      <!-- MENU -->
      <!-- ================================================= -->
      <nav class="flex-1 overflow-y-auto p-4">

        <!-- ================= RECEPTIONIST ================= -->
        <template v-if="currentRole === 'Receptionist'">

          <div
            class="mb-2 px-3 text-xs font-semibold
                   uppercase tracking-wider text-slate-400"
          >
          </div>

          <div class="space-y-1">

            <!-- Tổng quan -->
            <RouterLink
              to="/reception"
              class="flex items-center gap-3
                     rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              exact-active-class="bg-violet-50 text-violet-700"
            >
              <!-- Grid icon -->
              <svg
                class="h-5 w-5"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
              >
                <rect x="3" y="3" width="7" height="7" />
                <rect x="14" y="3" width="7" height="7" />
                <rect x="3" y="14" width="7" height="7" />
                <rect x="14" y="14" width="7" height="7" />
              </svg>

              <span>Tổng quan</span>
            </RouterLink>


            <!-- Quản lý hàng đợi -->
            <RouterLink
              to="/reception/queue"
              class="flex items-center gap-3
                     rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              <!-- Users icon -->
              <svg
                class="h-5 w-5"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
              >
                <path
                  d="M16 21v-2a4 4 0 0 0-4-4H6
                     a4 4 0 0 0-4 4v2"
                />
                <circle
                  cx="9"
                  cy="7"
                  r="4"
                />

                <path
                  d="M22 21v-2a4 4 0 0 0-3-3.87"
                />

                <path
                  d="M16 3.13a4 4 0 0 1 0 7.75"
                />
              </svg>

              <span>Quản lý hàng đợi</span>
            </RouterLink>


            <!-- Đặt khám & Tiếp nhận -->
            <RouterLink
              to="/reception/booking"
              class="flex items-center gap-3
                     rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              <!-- Calendar plus icon -->
              <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M8 2v4" />
                <path d="M16 2v4" />
                <rect x="3" y="4" width="18" height="18" rx="2" />
                <path d="M3 10h18" />
                <path d="M12 14v4" />
                <path d="M10 16h4" />
              </svg>
              <span>Đặt khám</span>
            </RouterLink>

            <!-- Check-in -->
            <RouterLink
              to="/reception/checkin"
              class="flex items-center gap-3
                     rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              <!-- Badge icon -->
              <svg
                class="h-5 w-5"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
              >
                <rect
                  x="3"
                  y="4"
                  width="18"
                  height="16"
                  rx="2"
                />

                <circle
                  cx="8"
                  cy="10"
                  r="2"
                />

                <path d="M12 9h5" />
                <path d="M12 13h5" />
              </svg>

              <span>Check-in bệnh nhân</span>
            </RouterLink>

            <!-- Pending appointments -->
            <RouterLink
              to="/reception/appointments/pending"
              class="flex items-center gap-3
                     rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              <!-- Clock icon -->
              <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <circle cx="12" cy="12" r="10" />
                <path d="M12 6v6l4 2" />
              </svg>
              <span>Chờ xác nhận</span>
            </RouterLink>

          </div>
        </template>


        <!-- ================= ADMIN ================= -->
        <template v-else-if="currentRole === 'Admin'">

          <div
            class="mb-2 px-3 text-xs font-semibold
                   uppercase tracking-wider text-slate-400"
          >
          </div>

          <div class="space-y-1">

            <RouterLink
              to="/admin"
              class="block rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              exact-active-class="bg-violet-50 text-violet-700"
            >
              Tổng quan
            </RouterLink>

            <RouterLink
              to="/admin/accounts"
              class="block rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              Quản lý tài khoản
            </RouterLink>

            <RouterLink
              to="/admin/doctors"
              class="block rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              Quản lý bác sĩ
            </RouterLink>

            <RouterLink
              to="/admin/specialties"
              class="block rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              Chuyên khoa
            </RouterLink>

            <RouterLink
              to="/admin/schedule"
              class="block rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              Lịch làm việc
            </RouterLink>

            <RouterLink
              to="/admin/reports"
              class="block rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              Báo cáo
            </RouterLink>

            <RouterLink
              to="/admin/settings"
              class="block rounded-lg px-3 py-2.5
                     text-sm font-medium
                     text-slate-600
                     transition-colors
                     hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              Cài đặt
            </RouterLink>

          </div>
        </template>


        <!-- ================= DOCTOR ================= -->
        <template v-else-if="currentRole === 'Doctor'">

          <div
            class="mb-2 px-3 text-xs font-semibold
                   uppercase tracking-wider text-slate-400"
          >
            Bác sĩ
          </div>

          <div class="space-y-1">
            <RouterLink
              to="/doctor/examination"
              class="block rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50"
              exact-active-class="bg-violet-50 text-violet-700"
            >
              Khám bệnh & hàng đợi
            </RouterLink>
            <RouterLink
              to="/doctor/medical-records"
              class="block rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              Hồ sơ bệnh án
            </RouterLink>
            <RouterLink
              to="/doctor/schedule"
              class="block rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              Lịch làm việc
            </RouterLink>
            <RouterLink
              to="/doctor/shift-requests"
              class="block rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              Đề xuất ca làm việc
            </RouterLink>
            <RouterLink
              to="/doctor/profile"
              class="block rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              Hồ sơ cá nhân
            </RouterLink>
          </div>

        </template>


        <!-- ================= PATIENT ================= -->
        <template v-else-if="currentRole === 'Patient'">

          <div
            class="mb-2 px-3 text-xs font-semibold
                   uppercase tracking-wider text-slate-400"
          >
            Bệnh nhân
          </div>

          <div class="space-y-1">
            <RouterLink
              to="/patient"
              class="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50"
              exact-active-class="bg-violet-50 text-violet-700"
            >
              <!-- Home icon -->
              <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M3 11.5L12 4l9 7.5" />
                <path d="M5 21V12h14v9" />
              </svg>
              <span>Tổng quan</span>
            </RouterLink>

            <RouterLink
              to="/patient/booking"
              class="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              <!-- Calendar icon -->
              <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <rect x="3" y="4" width="18" height="18" rx="2" />
                <path d="M16 2v4M8 2v4M3 10h18" />
              </svg>
              <span>Đặt khám</span>
            </RouterLink>

            <RouterLink
              to="/patient/queue"
              class="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              <!-- Queue icon -->
              <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01" />
              </svg>
              <span>Hàng đợi</span>
            </RouterLink>

            <RouterLink
              to="/patient/history"
              class="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              <!-- History icon -->
              <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M21 12a9 9 0 1 1-3-6.7" />
                <path d="M12 7v5l4 2" />
              </svg>
              <span>Lịch sử khám</span>
            </RouterLink>

            <RouterLink
              to="/profile"
              class="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50"
              active-class="bg-violet-50 text-violet-700"
            >
              <!-- Profile icon -->
              <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <circle cx="12" cy="8" r="4" />
                <path d="M6 20a6 6 0 0 1 12 0" />
              </svg>
              <span>Hồ sơ</span>
            </RouterLink>
          </div>

        </template>

      </nav>

    </aside>


    <!-- ================================================= -->
    <!-- MAIN CONTENT -->
    <!-- ================================================= -->
    <div class="flex min-w-0 flex-1 flex-col">

      <!-- ================= HEADER ================= -->
      <header
        class="flex h-16 shrink-0 items-center
               justify-end gap-4
               border-b border-slate-200
               bg-white px-6"
      >

        <!-- Language -->
        <select
          aria-label="Language"
          :value="locale"
          class="h-9 rounded-lg
                 border border-slate-200
                 bg-white px-2
                 text-sm text-slate-600
                 outline-none
                 focus:border-violet-500"
          @change="onLocaleChange"
        >
          <option
            v-for="language in supportedLocales"
            :key="language"
            :value="language"
          >
            {{ language.toUpperCase() }}
          </option>
        </select>

        <!-- Notifications Bell -->
        <div v-if="auth.isAuthenticated" class="relative">
          <button
            type="button"
            aria-label="Notifications"
            class="relative flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-600 hover:border-violet-400 hover:text-violet-600 focus:outline-none"
            @click="isNotificationsOpen = !isNotificationsOpen"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
              <path d="M13.73 21a2 2 0 0 1-3.46 0" />
            </svg>
            <span
              v-if="unreadCount > 0"
              class="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-600 px-1 text-[10px] font-bold text-white"
            >
              {{ unreadCount > 99 ? '99+' : unreadCount }}
            </span>
          </button>

          <div
            v-if="isNotificationsOpen"
            class="absolute right-0 z-50 mt-2 w-80 rounded-xl border border-slate-200 bg-white p-3 shadow-lg"
          >
            <div class="mb-2 flex items-center justify-between border-b border-slate-100 pb-2">
              <span class="text-xs font-semibold text-slate-700">Thông báo</span>
              <button
                v-if="unreadCount > 0"
                type="button"
                class="text-xs font-medium text-violet-600 hover:underline"
                @click="handleMarkAllRead"
              >
                Đánh dấu đã đọc tất cả
              </button>
            </div>
            <div v-if="notifications.length === 0" class="py-4 text-center text-xs text-slate-400">
              Chưa có thông báo mới
            </div>
            <ul v-else class="max-h-72 space-y-1.5 overflow-y-auto">
              <li
                v-for="item in notifications"
                :key="item.id"
                class="cursor-pointer rounded-lg p-2 text-xs transition-colors hover:bg-slate-50"
                :class="item.isRead ? 'text-slate-500' : 'bg-violet-50/60 font-medium text-slate-800'"
                @click="handleMarkRead(item)"
              >
                <div class="truncate font-semibold">{{ item.title }}</div>
                <div class="mt-0.5 line-clamp-2 text-[11px] text-slate-600">{{ item.content }}</div>
              </li>
            </ul>
          </div>
        </div>


          <!-- USER -->
          <div
            v-if="auth.user"
            class="flex items-center gap-3"
          >

            <!-- Avatar -->
            <div
              class="flex h-9 w-9 shrink-0
                    items-center justify-center
                    rounded-full
                    bg-violet-100
                    text-sm font-semibold
                    text-violet-700"
            >
              {{ displayName.charAt(0)?.toUpperCase() || 'U' }}
            </div>

            <!-- Name + role -->
            <div class="min-w-0">

              <div
                class="max-w-32 truncate
                      text-sm font-medium
                      text-slate-800"
              >
                {{ displayName }}
              </div>

              <div class="text-xs text-slate-400">
                <template v-if="currentRole === 'Receptionist'">
                  Lễ tân
                </template>

                <template v-else-if="currentRole === 'Admin'">
                  Quản trị viên
                </template>

                <template v-else-if="currentRole === 'Doctor'">
                  Bác sĩ
                </template>

                <template v-else-if="currentRole === 'Patient'">
                  Bệnh nhân
                </template>
              </div>

            </div>

          </div>

        <div class="flex items-center gap-3">
          <!-- Đổi vai trò (khi có nhiều hơn 1 role) -->
          <RouterLink
            v-if="(auth.user?.roles?.length ?? 0) > 1"
            to="/select-role"
            class="text-sm font-medium text-slate-500 hover:text-violet-600 transition-colors cursor-pointer flex items-center gap-1.5 focus:outline-none"
            title="Đổi vai trò"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
              <circle cx="9" cy="7" r="4" />
              <polyline points="16 11 18 13 22 9" />
            </svg>
            <span class="hidden sm:inline">Đổi vai trò</span>
          </RouterLink>

          <!-- Đổi mật khẩu -->
          <button
            type="button"
            class="text-sm font-medium text-slate-500 hover:text-[#0E4D92] transition-colors cursor-pointer flex items-center gap-1.5 focus:outline-none"
            @click="isChangePasswordOpen = true"
            title="Đổi mật khẩu"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect width="18" height="11" x="3" y="11" rx="2" ry="2" />
              <path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
            <span class="hidden sm:inline">Đổi mật khẩu</span>
          </button>

          <!-- Logout -->
          <button
            type="button"
            class="text-sm font-medium text-slate-500 hover:text-red-600 transition-colors cursor-pointer focus:outline-none"
            @click="logout"
          >
            Đăng xuất
          </button>
        </div>

      </header>


      <!-- ================= PAGE CONTENT ================= -->
      <main class="min-h-0 flex-1 overflow-y-auto p-6">
        <RouterView />
      </main>

      <!-- Modal Đổi Mật Khẩu -->
      <ChangePasswordModal
        :open="isChangePasswordOpen"
        @close="isChangePasswordOpen = false"
      />

    </div>

  </div>
</template>
