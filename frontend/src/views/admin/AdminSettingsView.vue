<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import BaseInput from '@/components/ui/BaseInput.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'

const SETTINGS_KEY = 'clinic_admin_settings'

const DEFAULT_SETTINGS = {
  clinicName: 'Phòng khám Đa khoa MediCare',
  clinicCode: 'PK-MEDICARE-01',
  clinicEmail: 'contact@medicare.vn',
  clinicPhone: '024 1234 5678',
  address: 'Số 1 Đại Cồ Việt, Hai Bà Trưng, Hà Nội',
  workingHours: '07:30 - 17:30 (Thứ 2 - Thứ 7)',
  allowBooking: true,
  allowCancel: true,
  allowCheckin: true,
  queueNotification: true,
  soundAlert: true,
  voiceCall: true,
  emailReminder: true,
  smsReminder: false,
  slotDuration: '20',
  maxAdvanceDays: '30',
  minCancelHours: '2',
}

const form = reactive({ ...DEFAULT_SETTINGS })

const saved = ref(false)
const saving = ref(false)
const resetSuccess = ref(false)

onMounted(() => {
  const savedConfig = localStorage.getItem(SETTINGS_KEY)
  if (savedConfig) {
    try {
      const parsed = JSON.parse(savedConfig)
      Object.assign(form, parsed)
    } catch {
      // ignore
    }
  }
})

function saveSettings() {
  saving.value = true
  localStorage.setItem(SETTINGS_KEY, JSON.stringify(form))

  setTimeout(() => {
    saving.value = false
    saved.value = true
    setTimeout(() => {
      saved.value = false
    }, 4000)
  }, 400)
}

function resetDefaults() {
  if (confirm('Bạn có chắc chắn muốn khôi phục toàn bộ cài đặt về giá trị mặc định ban đầu?')) {
    Object.assign(form, DEFAULT_SETTINGS)
    localStorage.setItem(SETTINGS_KEY, JSON.stringify(DEFAULT_SETTINGS))
    resetSuccess.value = true
    setTimeout(() => {
      resetSuccess.value = false
    }, 3000)
  }
}
</script>

<template>
  <div class="max-w-4xl space-y-6 font-sans">
    <!-- Header Title -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-slate-800 tracking-tight">Cài đặt Hệ thống</h1>
        <p class="mt-1 text-sm text-slate-500">
          Quản lý thông tin cơ sở khám chữa bệnh, quy tắc vận hành hàng đợi và thông báo
        </p>
      </div>

      <div class="flex items-center gap-2.5">
        <BaseButton
          type="button"
          variant="outline"
          size="md"
          @click="resetDefaults"
        >
          🔄 Khôi phục mặc định
        </BaseButton>

        <BaseButton
          type="button"
          variant="primary"
          size="md"
          :loading="saving"
          loading-text="Đang lưu…"
          @click="saveSettings"
        >
          💾 Lưu cấu hình
        </BaseButton>
      </div>
    </div>

    <!-- Alerts -->
    <div v-if="saved">
      <BaseAlert type="success" message="Đã lưu cấu hình hệ thống thành công!" dismissible @dismiss="saved = false" />
    </div>
    <div v-if="resetSuccess">
      <BaseAlert type="info" message="Đã khôi phục cài đặt về giá trị mặc định ban đầu!" dismissible @dismiss="resetSuccess = false" />
    </div>

    <!-- Clinic Info Section -->
    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-2xs">
      <div class="mb-5 flex items-center justify-between">
        <div>
          <h2 class="text-base font-bold text-slate-800 flex items-center gap-2">
            <span>🏥 Thông tin Phòng khám</span>
          </h2>
          <p class="mt-0.5 text-xs text-slate-400">
            Thông tin cơ bản hiển thị trên giao diện bệnh nhân, phiếu tiếp đón và hóa đơn
          </p>
        </div>
      </div>

      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <BaseInput
          v-model="form.clinicName"
          label="Tên phòng khám"
          required
          placeholder="Phòng khám Đa khoa MediCare"
        />

        <BaseInput
          v-model="form.clinicCode"
          label="Mã định danh cơ sở y tế"
          required
          placeholder="PK-MEDICARE-01"
        />

        <BaseInput
          v-model="form.clinicEmail"
          label="Email tiếp nhận & Chăm sóc"
          type="email"
          required
          placeholder="contact@medicare.vn"
        />

        <BaseInput
          v-model="form.clinicPhone"
          label="Hotline / Tổng đài tư vấn"
          required
          placeholder="024 1234 5678"
        />

        <BaseInput
          v-model="form.workingHours"
          label="Thời gian hoạt động tiêu chuẩn"
          required
          placeholder="07:30 - 17:30 (Thứ 2 - Thứ 7)"
        />

        <BaseInput
          v-model="form.address"
          label="Địa chỉ phòng khám"
          required
          placeholder="Số 1 Đại Cồ Việt, Hai Bà Trưng, Hà Nội"
        />
      </div>
    </section>

    <!-- Operational Rules Section -->
    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-2xs">
      <div class="mb-5">
        <h2 class="text-base font-bold text-slate-800 flex items-center gap-2">
          <span>⚙️ Quy tắc Đặt lịch & Tiếp đón Bệnh nhân</span>
        </h2>
        <p class="mt-0.5 text-xs text-slate-400">
          Thiết lập quyền tự phục vụ cho bệnh nhân và cơ chế phân luồng tiếp đón tại quầy
        </p>
      </div>

      <div class="divide-y divide-slate-100">
        <!-- Allow Booking -->
        <div class="flex items-center justify-between py-4">
          <div>
            <div class="text-sm font-semibold text-slate-800">
              Cho phép đặt lịch trực tuyến
            </div>
            <div class="mt-0.5 text-xs text-slate-400">
              Bệnh nhân có thể tra cứu bác sĩ, chọn ca khám và tự đặt hẹn trên cổng thông tin
            </div>
          </div>

          <label class="relative inline-flex items-center cursor-pointer">
            <input type="checkbox" v-model="form.allowBooking" class="sr-only peer" />
            <div class="w-11 h-6 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-[#0E4D92]"></div>
          </label>
        </div>

        <!-- Allow Cancel -->
        <div class="flex items-center justify-between py-4">
          <div>
            <div class="text-sm font-semibold text-slate-800">
              Cho phép hủy lịch hẹn trước giờ khám
            </div>
            <div class="mt-0.5 text-xs text-slate-400">
              Bệnh nhân được tự hủy lịch khám tối thiểu {{ form.minCancelHours }} giờ trước giờ hẹn
            </div>
          </div>

          <label class="relative inline-flex items-center cursor-pointer">
            <input type="checkbox" v-model="form.allowCancel" class="sr-only peer" />
            <div class="w-11 h-6 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-[#0E4D92]"></div>
          </label>
        </div>

        <!-- Allow Check-in -->
        <div class="flex items-center justify-between py-4">
          <div>
            <div class="text-sm font-semibold text-slate-800">
              Tiếp đón & Check-in tự động tại quầy
            </div>
            <div class="mt-0.5 text-xs text-slate-400">
              Lễ tân có thể xác nhận số thứ tự và in phiếu tiếp đón ngay khi bệnh nhân đến
            </div>
          </div>

          <label class="relative inline-flex items-center cursor-pointer">
            <input type="checkbox" v-model="form.allowCheckin" class="sr-only peer" />
            <div class="w-11 h-6 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-[#0E4D92]"></div>
          </label>
        </div>

        <!-- Slot Duration & Max Advance Days -->
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-4">
          <div>
            <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
              Thời lượng mỗi ca khám tiêu chuẩn
            </label>
            <select
              v-model="form.slotDuration"
              class="w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-xs font-semibold text-slate-700 focus:border-[#0E4D92] focus:outline-none transition-colors"
            >
              <option value="15">15 phút / bệnh nhân</option>
              <option value="20">20 phút / bệnh nhân (Khuyên dùng)</option>
              <option value="30">30 phút / bệnh nhân</option>
              <option value="45">45 phút / bệnh nhân</option>
            </select>
          </div>

          <div>
            <label class="block text-xs font-bold text-slate-700 uppercase tracking-wider mb-1.5">
              Đặt hẹn trước tối đa
            </label>
            <select
              v-model="form.maxAdvanceDays"
              class="w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-xs font-semibold text-slate-700 focus:border-[#0E4D92] focus:outline-none transition-colors"
            >
              <option value="7">7 ngày tới</option>
              <option value="14">14 ngày tới</option>
              <option value="30">30 ngày tới (1 tháng)</option>
              <option value="60">60 ngày tới (2 tháng)</option>
            </select>
          </div>
        </div>
      </div>
    </section>

    <!-- Notification & Sound Section -->
    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-2xs">
      <div class="mb-5">
        <h2 class="text-base font-bold text-slate-800 flex items-center gap-2">
          <span>🔔 Âm thanh & Thông báo Hàng đợi</span>
        </h2>
        <p class="mt-0.5 text-xs text-slate-400">
          Cấu hình chuông báo gọi số, phát thanh và tin nhắn nhắc nhở tự động
        </p>
      </div>

      <div class="divide-y divide-slate-100">
        <!-- Sound Alert -->
        <div class="flex items-center justify-between py-4">
          <div>
            <div class="text-sm font-semibold text-slate-800">
              Chuông báo âm thanh khi gọi số khám
            </div>
            <div class="mt-0.5 text-xs text-slate-400">
              Phát âm thanh thông báo trên màn hình chờ sảnh tiếp đón
            </div>
          </div>

          <label class="relative inline-flex items-center cursor-pointer">
            <input type="checkbox" v-model="form.soundAlert" class="sr-only peer" />
            <div class="w-11 h-6 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-[#0E4D92]"></div>
          </label>
        </div>

        <!-- Voice Call -->
        <div class="flex items-center justify-between py-4">
          <div>
            <div class="text-sm font-semibold text-slate-800">
              Đọc loa tự động tên & số thứ tự bệnh nhân
            </div>
            <div class="mt-0.5 text-xs text-slate-400">
              Tự động phát giọng nói "Mời bệnh nhân số... vào phòng khám..."
            </div>
          </div>

          <label class="relative inline-flex items-center cursor-pointer">
            <input type="checkbox" v-model="form.voiceCall" class="sr-only peer" />
            <div class="w-11 h-6 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-[#0E4D92]"></div>
          </label>
        </div>

        <!-- Email Reminder -->
        <div class="flex items-center justify-between py-4">
          <div>
            <div class="text-sm font-semibold text-slate-800">
              Email nhắc hẹn tự động trước 24 giờ
            </div>
            <div class="mt-0.5 text-xs text-slate-400">
              Gửi email hướng dẫn chuẩn bị và thông tin ca khám tới bệnh nhân
            </div>
          </div>

          <label class="relative inline-flex items-center cursor-pointer">
            <input type="checkbox" v-model="form.emailReminder" class="sr-only peer" />
            <div class="w-11 h-6 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-[#0E4D92]"></div>
          </label>
        </div>
      </div>
    </section>

    <!-- Save Actions -->
    <div class="flex items-center justify-end gap-3 pt-2">
      <BaseButton
        type="button"
        variant="outline"
        size="lg"
        @click="resetDefaults"
      >
        Khôi phục mặc định
      </BaseButton>

      <BaseButton
        type="button"
        variant="primary"
        size="lg"
        :loading="saving"
        loading-text="Đang lưu…"
        @click="saveSettings"
      >
        Lưu cấu hình hệ thống
      </BaseButton>
    </div>
  </div>
</template>

