<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { adminApi } from '@/features/admin/admin.api'
import type { AppointmentExtended, DashboardData, StatisticsData } from '@/features/admin/admin.types'
import AdminStatCard from '@/features/admin/components/AdminStatCard.vue'
import AdminBarChart from '@/features/admin/components/AdminBarChart.vue'
import AdminDonutChart from '@/features/admin/components/AdminDonutChart.vue'
import BaseStatusBadge from '@/components/ui/BaseStatusBadge.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'

const tab = ref<'week' | 'month'>('week')
const dashboardData = ref<DashboardData | null>(null)
const statisticsData = ref<StatisticsData | null>(null)
const appointments = ref<AppointmentExtended[]>([])
const loading = ref(false)
const error = ref('')

const barChartData = computed(() => {
  if (statisticsData.value?.chartLabels?.length) {
    return statisticsData.value.chartLabels.map((day, i) => ({
      day,
      value: statisticsData.value?.chartData[i] ?? 0,
    }))
  }
  return []
})

const maxBarValue = computed(() => {
  return Math.max(...barChartData.value.map((d) => d.value), 10)
})

const donutColors = ['#00A878', '#0E4D92', '#EF4444', '#F59E0B', '#8B5CF6', '#EC4899']

const donutChartData = computed(() => {
  const dist = statisticsData.value?.specialtyDistribution || []
  if (dist.length === 0) {
    return [
      { label: 'Chưa có dữ liệu', value: 0, color: '#94A3B8' },
    ]
  }
  return dist.map((item, index) => ({
    label: item.label,
    value: item.count,
    color: donutColors[index % donutColors.length] || '#0E4D92',
  }))
})

async function loadDashboard() {
  loading.value = true
  error.value = ''

  try {
    const [dash, stats, appts] = await Promise.all([
      adminApi.getDashboard(),
      adminApi.getStatistics(tab.value === 'week' ? 'Week' : 'Month'),
      adminApi.getAppointmentsByDate(),
    ])
    dashboardData.value = dash
    statisticsData.value = stats
    appointments.value = appts.items || []
  } catch (err: any) {
    console.error('Failed to load dashboard data:', err)
    error.value = err?.response?.data?.message || err?.message || 'Không thể tải dữ liệu thống kê từ máy chủ.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadDashboard()
})

watch(tab, async () => {
  try {
    statisticsData.value = await adminApi.getStatistics(tab.value === 'week' ? 'Week' : 'Month')
  } catch (err: any) {
    console.error('Failed to update stats period:', err)
  }
})

const statCards = computed(() => {
  const d = dashboardData.value

  const formatVal = (val: number | undefined, suffix = '') => {
    if (val === undefined || val === null) return '0' + suffix
    return val.toLocaleString('vi-VN') + suffix
  }

  const formatChange = (change: number | undefined) => {
    if (change === undefined || change === null || change === 0) return '0% so với kỳ trước'
    const sign = change > 0 ? '+' : ''
    return `${sign}${change.toFixed(1)}% so với kỳ trước`
  }

  return [
    {
      label: 'Tổng lịch hẹn (30 ngày)',
      value: formatVal(d?.totalAppointments?.value),
      sub: formatChange(d?.totalAppointments?.change),
    },
    {
      label: 'Tổng bệnh nhân',
      value: formatVal(d?.totalPatients?.value),
      sub: formatChange(d?.totalPatients?.change),
    },
    {
      label: 'Tổng số bác sĩ',
      value: formatVal(d?.totalDoctors?.value),
      sub: `${d?.activeDoctor?.value ?? 0} bác sĩ đang hoạt động`,
    },
    {
      label: 'Lịch hẹn hôm nay',
      value: String(appointments.value.length),
      sub: 'Được đặt trên hệ thống',
    },
    {
      label: 'Tỷ lệ hoàn thành',
      value: formatVal(d?.completionRate?.value, '%'),
      sub: 'Trong 30 ngày qua',
    },
    {
      label: 'Thời gian chờ TB',
      value: formatVal(d?.averageWaitingMinute?.value, ' phút'),
      sub: 'Thời gian chờ khám',
    },
  ]
})
</script>

<template>
  <div class="space-y-5 font-sans">
    <!-- Header Title -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-slate-800 tracking-tight">Tổng quan Hệ thống (Dashboard)</h1>
        <p class="mt-1 text-sm text-slate-500">
          Số liệu khám chữa bệnh thực tế từ cơ sở dữ liệu và danh sách lịch hẹn hôm nay
        </p>
      </div>

      <button
        type="button"
        class="inline-flex items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-2xs hover:bg-slate-50 transition-colors cursor-pointer"
        @click="loadDashboard"
      >
        <span :class="{ 'animate-spin': loading }">🔄</span> Tải lại
      </button>
    </div>

    <!-- Alert nếu có lỗi kết nối -->
    <div v-if="error">
      <BaseAlert type="error" :message="error" dismissible @dismiss="error = ''" />
    </div>

    <!-- Loading State -->
    <div v-if="loading && !dashboardData" class="py-16 text-center text-slate-400 text-sm">
      <div class="inline-block animate-spin rounded-full h-8 w-8 border-3 border-[#0E4D92] border-t-transparent mb-2"></div>
      <p>Đang tải dữ liệu tổng quan từ hệ thống…</p>
    </div>

    <!-- Statistics Cards -->
    <div class="grid grid-cols-2 gap-4 lg:grid-cols-3 xl:grid-cols-6">
      <AdminStatCard
        v-for="stat in statCards"
        :key="stat.label"
        :label="stat.label"
        :value="stat.value"
        :sub="stat.sub"
      />
    </div>

    <!-- Charts Section -->
    <div class="grid grid-cols-1 gap-5 lg:grid-cols-5">
      <!-- Bar Chart -->
      <div class="rounded-2xl border border-slate-200 bg-white p-6 shadow-2xs lg:col-span-3">
        <div class="mb-5 flex items-center justify-between">
          <div>
            <h3 class="text-base font-bold text-slate-800">
              Lượt khám theo ngày
            </h3>
            <p class="mt-0.5 text-xs text-slate-400">
              Số lịch hẹn được ghi nhận trên hệ thống
            </p>
          </div>

          <div class="flex items-center gap-1 rounded-xl bg-slate-100 p-1">
            <button
              type="button"
              class="rounded-lg px-3 py-1 text-xs font-semibold transition-colors cursor-pointer"
              :class="tab === 'week' ? 'bg-white text-slate-800 shadow-xs' : 'text-slate-500 hover:text-slate-700'"
              @click="tab = 'week'"
            >
              7 ngày
            </button>
            <button
              type="button"
              class="rounded-lg px-3 py-1 text-xs font-semibold transition-colors cursor-pointer"
              :class="tab === 'month' ? 'bg-white text-slate-800 shadow-xs' : 'text-slate-500 hover:text-slate-700'"
              @click="tab = 'month'"
            >
              30 ngày
            </button>
          </div>
        </div>

        <div v-if="barChartData.length === 0" class="h-56 flex items-center justify-center text-xs text-slate-400">
          Chưa có dữ liệu lịch hẹn trong khoảng thời gian này.
        </div>
        <AdminBarChart v-else :data="barChartData" :max-val="maxBarValue" />
      </div>

      <!-- Donut Chart -->
      <div class="rounded-2xl border border-slate-200 bg-white p-6 shadow-2xs lg:col-span-2">
        <h3 class="text-base font-bold text-slate-800">
          Phân bố theo trạng thái lịch hẹn
        </h3>
        <p class="mt-0.5 text-xs text-slate-400">
          Tỷ lệ trạng thái tiếp nhận bệnh nhân trong kỳ
        </p>

        <div class="mt-4">
          <AdminDonutChart :data="donutChartData" />
        </div>
      </div>
    </div>

    <!-- Recent Appointments Table -->
    <div class="rounded-2xl border border-slate-200 bg-white p-6 shadow-2xs">
      <div class="mb-4 flex items-center justify-between">
        <div>
          <h3 class="text-base font-bold text-slate-800">
            Lịch hẹn khám hôm nay
          </h3>
          <p class="mt-0.5 text-xs text-slate-400">
            Danh sách bệnh nhân đăng ký khám trong ngày thực tế
          </p>
        </div>
      </div>

      <div class="overflow-x-auto rounded-xl border border-slate-100">
        <table class="w-full text-left text-sm border-collapse">
          <thead>
            <tr class="border-b border-slate-100 bg-slate-50/75 text-xs font-semibold uppercase tracking-wider text-slate-500">
              <th class="px-4 py-3.5">Mã lịch hẹn</th>
              <th class="px-4 py-3.5">Bệnh nhân</th>
              <th class="px-4 py-3.5">Bác sĩ phụ trách</th>
              <th class="px-4 py-3.5">Chuyên khoa</th>
              <th class="px-4 py-3.5">Khung giờ</th>
              <th class="px-4 py-3.5">Trạng thái</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            <tr
              v-for="appt in appointments"
              :key="appt.id"
              class="hover:bg-slate-50/80 transition-colors"
            >
              <td class="px-4 py-3.5 text-xs font-mono font-bold text-[#0E4D92]">
                {{ appt.id }}
              </td>
              <td class="px-4 py-3.5 font-bold text-slate-800">
                {{ appt.patientName }}
              </td>
              <td class="px-4 py-3.5 text-slate-700">
                {{ appt.doctorName }}
              </td>
              <td class="px-4 py-3.5">
                <span class="inline-flex items-center rounded-lg bg-blue-50 px-2.5 py-0.5 text-xs font-semibold text-[#0E4D92]">
                  {{ appt.specialtyName }}
                </span>
              </td>
              <td class="px-4 py-3.5 text-xs font-mono text-slate-600">
                {{ appt.timeSlot }}
              </td>
              <td class="px-4 py-3.5">
                <BaseStatusBadge :status="appt.status" />
              </td>
            </tr>
            <tr v-if="appointments.length === 0">
              <td colspan="6" class="px-4 py-12 text-center text-slate-400 text-sm">
                Chưa có lịch hẹn nào được ghi nhận cho ngày hôm nay.
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
