<script setup lang="ts">
withDefaults(
  defineProps<{
    title?: string
    subtitle?: string
    heroTitle?: string
    heroSubtitle?: string
    heroBadge?: string
    features?: string[]
    brandName?: string
  }>(),
  {
    title: 'Đăng nhập',
    subtitle: '',
    heroTitle: 'Hệ thống Đặt lịch &\nQuản lý Hàng đợi',
    heroSubtitle: 'Giải pháp chuyển đổi số toàn diện cho quy trình khám chữa bệnh tại phòng khám.',
    heroBadge: 'Clinic Management',
    features: () => [
      'Bảo mật dữ liệu chuẩn y tế',
      'Phân quyền tài khoản theo vai trò (RBAC)',
      'Hỗ trợ đặt lịch & quản lý hàng đợi thời gian thực',
    ],
    brandName: 'ClinicQueue',
  },
)
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex font-sans">
    <!-- Panel bên trái: Hero Brand Cover (Ẩn trên Mobile/Tablet nhỏ, hiện trên Desktop lg) -->
    <div
      class="hidden lg:flex lg:w-2/5 bg-gradient-to-br from-[#0E4D92] via-[#1056a2] to-[#1565c0] flex-col justify-between p-10 text-white relative overflow-hidden transition-all duration-300"
    >
      <!-- Background pattern subtle decoration -->
      <div class="absolute -right-16 -top-16 w-64 h-64 rounded-full bg-white/5 blur-2xl pointer-events-none" />
      <div class="absolute -left-16 -bottom-16 w-64 h-64 rounded-full bg-blue-400/10 blur-2xl pointer-events-none" />

      <!-- Top: Logo & Brand -->
      <div class="flex items-center gap-3 relative z-10">
        <div class="w-9 h-9 bg-white/20 backdrop-blur-md rounded-xl flex items-center justify-center text-white font-bold text-xl shadow-inner">
          +
        </div>
        <span class="font-bold text-white text-lg tracking-wide">{{ brandName }}</span>
      </div>

      <!-- Center: Heading & Description & Features -->
      <div class="relative z-10 my-auto py-8">
        <span
          v-if="heroBadge"
          class="inline-block bg-white/15 text-blue-100 border border-white/20 text-xs px-3.5 py-1 rounded-full font-medium mb-5 backdrop-blur-sm"
        >
          {{ heroBadge }}
        </span>

        <h2 class="text-3xl xl:text-4xl font-extrabold text-white leading-tight mb-4 whitespace-pre-line">
          {{ heroTitle }}
        </h2>

        <p class="text-white/80 text-sm xl:text-base leading-relaxed max-w-md mb-8">
          {{ heroSubtitle }}
        </p>

        <!-- Feature checklist -->
        <div v-if="features && features.length > 0" class="space-y-3">
          <div
            v-for="feature in features"
            :key="feature"
            class="flex items-center gap-3 text-sm text-white/90"
          >
            <span class="flex h-5 w-5 items-center justify-center rounded-full bg-white/20 text-xs font-bold text-white">
              ✓
            </span>
            <span>{{ feature }}</span>
          </div>
        </div>
      </div>

      <!-- Bottom: Copyright -->
      <div class="relative z-10 flex items-center justify-between text-xs text-white/50 pt-4 border-t border-white/10">
        <span>© 2026 {{ brandName }}. All rights reserved.</span>
        <span>v1.0.0</span>
      </div>
    </div>

    <!-- Panel bên phải: Form Container -->
    <div class="flex-1 flex flex-col items-center justify-center px-4 sm:px-8 py-10 overflow-y-auto">
      <div class="w-full max-w-md">
        <!-- Logo hiển thị trên màn hình nhỏ (Mobile/Tablet) -->
        <div class="flex items-center gap-2.5 mb-6 lg:hidden">
          <div class="w-8 h-8 bg-[#0E4D92] rounded-xl flex items-center justify-center text-white font-bold text-lg shadow-sm">
            +
          </div>
          <span class="font-bold text-slate-800 text-base">{{ brandName }}</span>
        </div>

        <!-- Header: Tiêu đề & phụ đề -->
        <div class="mb-6">
          <slot name="header">
            <h1 class="text-2xl sm:text-3xl font-bold text-slate-800 tracking-tight">{{ title }}</h1>
            <p v-if="subtitle" class="text-sm text-slate-500 mt-1.5">{{ subtitle }}</p>
          </slot>
        </div>

        <!-- Slot Error Banner / Alert -->
        <slot name="alerts" />

        <!-- Form Body -->
        <slot />

        <!-- Footer link (Chuyển trang Đăng nhập / Đăng ký / Trợ giúp) -->
        <slot name="footer" />

        <!-- Technical Support Info -->
        <slot name="support">
          <p class="text-xs text-slate-400 text-center mt-6">
            Gặp vấn đề khi thao tác? Liên hệ <a href="mailto:hotro@phongkham.vn" class="text-[#0E4D92] font-medium hover:underline">hotro@phongkham.vn</a>
          </p>
        </slot>
      </div>
    </div>
  </div>
</template>
