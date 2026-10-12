import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import AppLayout from '@/components/layout/AppLayout.vue'
import type { UserRole } from '@/features/auth/auth.types'

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    roles?: UserRole[]
  }
}

const routes: RouteRecordRaw[] = [
  // Root
  {
    path: '/',
    redirect: '/login',
  },

  // Route public: Chọn vai trò
  {
    path: '/select-role',
    name: 'select-role',
    component: () => import('@/views/auth/SelectRoleView.vue'),
  },

  // Route public: Đăng nhập
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/auth/LoginView.vue'),
    meta: { public: true },
  },

  // Route public: Đăng ký
  {
    path: '/register',
    name: 'register',
    component: () => import('@/views/auth/RegisterView.vue'),
    meta: { public: true },
  },

  // Route public: Quên mật khẩu
  {
    path: '/forgot-password',
    name: 'forgot-password',
    component: () => import('@/views/auth/ForgotPasswordView.vue'),
    meta: { public: true },
  },

  // Route chức năng (yêu cầu đăng nhập, dùng AppLayout)
  {
    path: '/',
    component: AppLayout,

    children: [
      // Luồng bệnh nhân
      {
        path: 'patient',
        name: 'patient-home',
        component: () => import('@/views/patient/PatientHomeView.vue'),
        meta: {
          roles: ['Patient'],
        },
      },
      // Profile completion
      {
        path: 'profile',
        name: 'profile',
        component: () => import('@/views/ProfileView.vue'),
        meta: {
          roles: ['Patient'],
        },
      },
      {
        path: 'patient/booking',
        name: 'patient-booking',
        component: () => import('@/views/patient/PatientBookingView.vue'),
        meta: {
          roles: ['Patient'],
        },
      },
      {
        path: 'patient/queue',
        name: 'patient-queue',
        component: () => import('@/views/patient/PatientQueueView.vue'),
        meta: {
          roles: ['Patient'],
        },
      },
      {
        path: 'patient/history',
        name: 'patient-history',
        component: () => import('@/views/patient/PatientHistoryView.vue'),
        meta: {
          roles: ['Patient'],
        },
      },

      // Luồng lễ tân
      {
        path: 'reception',
        meta: {
          roles: ['Receptionist'],
        },
        children: [
          {
            path: '',
            name: 'reception-dashboard',
            component: () =>
              import('@/views/receptionist/ReceptionistDashboardView.vue'),
          },
          {
            path: 'queue',
            name: 'reception-queue',
            component: () =>
              import('@/views/receptionist/ReceptionistQueueView.vue'),
          },
          {
            path: 'checkin',
            name: 'reception-checkin',
            component: () =>
              import('@/views/receptionist/ReceptionistCheckinView.vue'),
          },
          {
            path: 'appointments/pending',
            name: 'reception-pending-appointments',
            component: () =>
              import('@/views/receptionist/ReceptionistPendingAppointmentsView.vue'),
          },
        ],
      },

      // Luồng bác sĩ
      {
        path: 'doctor/examination',
        name: 'doctor-examination',
        component: () =>
          import('@/views/doctor/DoctorExaminationView.vue'),
        meta: {
          roles: ['Doctor'],
        },
      },
      {
        path: 'doctor/schedule',
        name: 'doctor-schedule',
        component: () =>
          import('@/views/doctor/DoctorScheduleView.vue'),
        meta: {
          roles: ['Doctor'],
        },
      },
      {
        path: 'doctor/shift-requests',
        name: 'doctor-shift-requests',
        component: () =>
          import('@/views/doctor/DoctorShiftRequestsView.vue'),
        meta: {
          roles: ['Doctor'],
        },
      },
      {
        path: 'doctor/profile',
        name: 'doctor-profile',
        component: () =>
          import('@/views/doctor/DoctorProfileView.vue'),
        meta: {
          roles: ['Doctor'],
        },
      },

      // Luồng Admin
      {
        path: 'admin',
        name: 'admin-dashboard',
        component: () =>
          import('@/views/admin/AdminDashboardView.vue'),
        meta: {
          roles: ['Admin'],
        },
      },
      {
        path: 'admin/accounts',
        name: 'admin-accounts',
        component: () =>
          import('@/views/admin/AdminAccountsView.vue'),
        meta: {
          roles: ['Admin'],
        },
      },
      {
        path: 'admin/doctors',
        name: 'admin-doctors',
        component: () =>
          import('@/views/admin/AdminDoctorsView.vue'),
        meta: {
          roles: ['Admin'],
        },
      },
      {
        path: 'admin/specialties',
        name: 'admin-specialties',
        component: () =>
          import('@/views/admin/AdminSpecialtiesView.vue'),
        meta: {
          roles: ['Admin'],
        },
      },
      {
        path: 'admin/schedule',
        name: 'admin-schedule',
        component: () =>
          import('@/views/admin/AdminScheduleView.vue'),
        meta: {
          roles: ['Admin'],
        },
      },
      {
        path: 'admin/reports',
        name: 'admin-reports',
        component: () =>
          import('@/views/admin/AdminReportsView.vue'),
        meta: {
          roles: ['Admin'],
        },
      },
      {
        path: 'admin/settings',
        name: 'admin-settings',
        component: () =>
          import('@/views/admin/AdminSettingsView.vue'),
        meta: {
          roles: ['Admin'],
        },
      },
    ],
  },

  // Catch-all 404
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('@/views/NotFoundView.vue'),
    meta: {
      public: true,
    },
  },
]

// Tạo Router
const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

// Global Navigation Guard
router.beforeEach(async (to) => {
  const mod = await import('@/stores/auth')
  const auth = mod.useAuthStore()

  // Chờ Hydrate Auth Store
  if ((auth as any).hydrating) {
    await new Promise((resolve) => {
      const start = Date.now()
      const interval = setInterval(() => {
        if (!(auth as any).hydrating || Date.now() - start > 3000) {
          clearInterval(interval)
          resolve(true)
        }
      }, 50)
    })
  }

  // BƯỚC 1: CHƯA ĐĂNG NHẬP
  if (!auth.isAuthenticated) {
    // Chỉ cho phép vào các trang public (Login, Register, TV Queue)
    if (to.meta.public) return true

    // Truy cập bất kỳ trang nào khác -> Đẩy về /login
    return {
      name: 'login',
      query: to.path !== '/' ? { redirect: to.fullPath } : undefined,
    }
  }

  // BƯỚC 2: ĐÃ ĐĂNG NHẬP NHƯNG CỐ VÀO TRANG PUBLIC (Login/Register)
  if (to.meta.public) {
    if (!auth.currentUserRole) return { name: 'select-role' }
    return getHomeRouteByRole(auth)
  }

  // BƯỚC 3: ĐÃ ĐĂNG NHẬP NHƯNG CHƯA CHỌN ROLE
  if (!auth.currentUserRole && to.name !== 'select-role') {
    return {
      name: 'select-role',
      query: { redirect: to.fullPath },
    }
  }

  // BƯỚC 4: KIỂM TRA QUYỀN TRUY CẬP (RBAC)
  const requiredRoles = to.meta.roles
  if (requiredRoles && requiredRoles.length > 0) {
    if (!auth.hasRole(requiredRoles)) {
      return getHomeRouteByRole(auth)
    }
  }

  return true
})

// Hàm phụ trợ trả về Route phù hợp theo Role
function getHomeRouteByRole(auth: any) {
  if (auth.hasRole(['Admin'])) return { name: 'admin-dashboard' }
  if (auth.hasRole(['Receptionist'])) return { name: 'reception-dashboard' }
  if (auth.hasRole(['Doctor'])) return { name: 'doctor-examination' }
  if (auth.hasRole(['Patient'])) return { name: 'patient-home' }
  return { name: 'select-role' }
}

export default router