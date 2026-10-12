<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import type { UserRole } from '@/features/auth/auth.types'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

interface RoleOption {
  role: UserRole
  title: string
  desc: string
  emoji: string
  tag: string
  tagColor: 'blue' | 'violet' | 'green' | 'amber'
  features: string[]
}

const ROLE_META: Record<UserRole, Omit<RoleOption, 'role'>> = {
  Patient: {
    title: 'Bệnh nhân',
    desc: 'Đặt lịch và theo dõi hàng đợi trực tuyến',
    emoji: '🧑‍⚕️',
    tag: 'Dùng miễn phí',
    tagColor: 'blue',
    features: ['Đặt lịch khám 24/7', 'Nhận thông báo SMS', 'Xem lịch sử khám'],
  },
  Receptionist: {
    title: 'Lễ tân',
    desc: 'Quản lý check-in và điều phối hàng đợi',
    emoji: '🗂️',
    tag: 'Nội bộ',
    tagColor: 'violet',
    features: ['Gọi số thứ tự', 'Check-in bệnh nhân', 'Ưu tiên khẩn cấp'],
  },
  Doctor: {
    title: 'Bác sĩ',
    desc: 'Xem lịch và ghi kết quả khám bệnh',
    emoji: '👨‍⚕️',
    tag: 'Nội bộ',
    tagColor: 'green',
    features: ['Danh sách bệnh nhân', 'Ghi chẩn đoán', 'Quản lý lịch'],
  },
  Admin: {
    title: 'Quản trị viên',
    desc: 'Quản lý toàn hệ thống và thống kê',
    emoji: '📊',
    tag: 'Quản lý',
    tagColor: 'amber',
    features: ['Báo cáo thống kê', 'Quản lý bác sĩ', 'Cấu hình hệ thống'],
  },
}

const availableRoles = computed<UserRole[]>(() =>
  auth.user?.roles?.length ? auth.user.roles : [auth.currentUserRole ?? 'Patient'],
)
const roles = computed<RoleOption[]>(() =>
  availableRoles.value.map((role) => ({ role, ...ROLE_META[role] })),
)

function selectRole(role: UserRole) {
  auth.setActiveRole(role)

  const redirect = route?.query?.redirect as string | undefined
  const routeMap: Record<UserRole, string> = {
    Patient: '/patient',
    Receptionist: '/reception/queue',
    Doctor: '/doctor/examination',
    Admin: '/admin',
  }

  if (router.replace) {
    router.replace(redirect || routeMap[role])
    return
  }

  if (router.push) {
    router.push(redirect || routeMap[role])
  }
}

function getTagClass(color: RoleOption['tagColor']) {
  const map = {
    blue: 'bg-blue-50 text-blue-700 border-blue-200',
    violet: 'bg-violet-50 text-violet-700 border-violet-200',
    green: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    amber: 'bg-amber-50 text-amber-700 border-amber-200',
  }
  return map[color]
}
</script>

<template>
  <div class="flex min-h-screen flex-col bg-slate-50 font-sans">
    <!-- Header -->
    <header
      class="sticky top-0 z-10 flex h-16 items-center justify-between border-b border-slate-200 bg-white/80 px-6 backdrop-blur"
    >
      <div class="flex items-center gap-2.5">
        <div
          class="flex h-8 w-8 items-center justify-center rounded-xl bg-[#0E4D92] text-lg font-bold text-white shadow-sm"
        >
          +
        </div>
        <div class="leading-tight">
          <p class="text-[15px] font-bold text-slate-800">ClinicQueue</p>
          <p class="hidden text-[10px] text-slate-400 sm:block">
            Đặt lịch & Quản lý hàng đợi khám bệnh
          </p>
        </div>
      </div>
      <div class="flex items-center gap-2.5">
        <button
          type="button"
          @click="router.push('/register')"
          class="rounded-xl px-3.5 py-1.5 text-sm font-semibold text-slate-700 transition-all hover:bg-slate-100"
        >
          Đăng ký
        </button>
        <button
          type="button"
          @click="router.push('/login')"
          class="rounded-xl border-2 border-[#0E4D92] px-3.5 py-1.5 text-sm font-semibold text-[#0E4D92] transition-all hover:bg-blue-50 active:scale-[0.98]"
        >
          Đăng nhập
        </button>
      </div>
    </header>

    <!-- Main Content -->
    <div class="flex flex-1 flex-col items-center justify-center px-4 py-4 lg:py-5">
      <!-- Hero -->
      <div class="mb-5 max-w-xl text-center">
        <h1 class="mb-2 text-2xl leading-tight font-bold text-slate-800 md:text-3xl">
          Bạn đang sử dụng hệ thống<br />với vai trò nào?
        </h1>
        <p class="text-base text-slate-500">
          Chọn vai trò để truy cập các chức năng phù hợp với bạn.
        </p>
      </div>

      <!-- Role Cards Grid -->
      <div class="grid w-full max-w-2xl grid-cols-1 gap-4 sm:grid-cols-2">
        <div
          v-for="item in roles"
          :key="item.role"
          @click="selectRole(item.role)"
          class="group flex cursor-pointer flex-col gap-3 rounded-2xl border-2 border-slate-200 bg-white p-4 transition-all duration-200 select-none hover:border-[#0E4D92] hover:shadow-xl hover:shadow-blue-100/60"
        >
          <div class="flex items-start justify-between">
            <div
              class="flex h-11 w-11 items-center justify-center rounded-xl bg-slate-50 text-2xl transition-colors duration-200 group-hover:bg-blue-50"
            >
              {{ item.emoji }}
            </div>
            <span
              :class="[
                'inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium',
                getTagClass(item.tagColor),
              ]"
            >
              {{ item.tag }}
            </span>
          </div>
          <div>
            <h3 class="mb-1 text-lg font-bold text-slate-800">
              {{ item.title }}
            </h3>
            <p class="mb-3 text-sm leading-relaxed text-slate-500">
              {{ item.desc }}
            </p>
            <ul class="space-y-1">
              <li
                v-for="f in item.features"
                :key="f"
                class="flex items-center gap-2 text-xs text-slate-500"
              >
                <span class="text-[#00A878]">✓</span>
                {{ f }}
              </li>
            </ul>
          </div>
          <div
            class="flex w-full items-center justify-center gap-2 rounded-xl border-2 border-slate-200 py-2.5 text-sm font-semibold text-slate-500 transition-all duration-200 group-hover:border-[#0E4D92] group-hover:bg-[#0E4D92] group-hover:text-white"
          >
            Truy cập ➔
          </div>
        </div>
      </div>

      <!-- Trust Badges -->
      <div class="mt-5 flex flex-wrap items-center justify-center gap-4 text-xs text-slate-400">
        <span class="flex items-center gap-1.5">🔒 Bảo mật SSL</span>
        <span class="flex items-center gap-1.5">📱 Hỗ trợ mobile</span>
        <span class="flex items-center gap-1.5">⚡ Cập nhật thời gian thực</span>
      </div>
    </div>

    <footer class="border-t border-slate-200 py-3 text-center text-xs text-slate-400">
      © 2026 ClinicQueue · Hệ thống quản lý phòng khám · v1.0
    </footer>
  </div>
</template>
