<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { adminApi } from '@/features/admin/admin.api'
import { specialtiesApi } from '@/features/specialties/specialties.api'
import type { DashboardData, StatisticsData, TotalAppointmentSummary } from '@/features/admin/admin.types'
import type { Specialty } from '@/features/specialties/specialties.types'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'

const period = ref<'week' | 'month' | 'quarter'>('week')
const selectedSpecialty = ref('all')
const loading = ref(false)
const exportSuccess = ref(false)
const errorMessage = ref('')

const dashboard = ref<DashboardData | null>(null)
const statistics = ref<StatisticsData | null>(null)
const summary = ref<TotalAppointmentSummary | null>(null)
const specialties = ref<Specialty[]>([])

// Date calculation based on selected period
function getDateRange(p: 'week' | 'month' | 'quarter') {
  const now = new Date()
  const end = now.toISOString().slice(0, 10)
  const startObj = new Date(now)

  if (p === 'week') {
    startObj.setDate(now.getDate() - 7)
  } else if (p === 'month') {
    startObj.setMonth(now.getMonth() - 1)
  } else {
    startObj.setMonth(now.getMonth() - 3)
  }

  const start = startObj.toISOString().slice(0, 10)
  return { start, end }
}

interface SpecialtyStatItem {
  id: string
  name: string
  visits: number
  avg: number
  pct: number
}

const specialtyStats = ref<SpecialtyStatItem[]>([])

async function loadData() {
  loading.value = true
  errorMessage.value = ''
  try {
    const { start, end } = getDateRange(period.value)
    const statPeriod = period.value === 'week' ? 'Week' : 'Month'

    const [dashRes, statsRes, sumRes, specRes] = await Promise.all([
      adminApi.getDashboard(),
      adminApi.getStatistics(statPeriod),
      adminApi.getAppointmentSummary(start, end),
      specialtiesApi.list({ pageSize: 100 }),
    ])

    dashboard.value = dashRes
    statistics.value = statsRes
    summary.value = sumRes
    specialties.value = specRes.items || []

    // Load actual statistics per specialty from Backend API
    const specItems = specRes.items || []
    if (specItems.length > 0) {
      const results = await Promise.all(
        specItems.map(async (s) => {
          try {
            const specSummary = await adminApi.getAppointmentSummary(start, end, undefined, s.id)
            const visits = specSummary.total || 0
            const completed = specSummary.completed || 0
            const pct = visits > 0 ? Math.round((completed / visits) * 100) : 0
            return {
              id: s.id,
              name: s.name,
              visits,
              avg: 0,
              pct,
            }
          } catch {
            return {
              id: s.id,
              name: s.name,
              visits: 0,
              avg: 0,
              pct: 0,
            }
          }
        })
      )
      specialtyStats.value = results
    } else {
      specialtyStats.value = []
    }
  } catch (err: any) {
    console.error('Failed to load report data:', err)
    errorMessage.value = 'Không thể tải toàn bộ dữ liệu báo cáo từ máy chủ.'
  } finally {
    loading.value = false
  }
}

watch(period, () => {
  loadData()
})

onMounted(() => {
  loadData()
})

// KPI Cards Data
const kpiCards = computed(() => {
  const d = dashboard.value
  const s = summary.value

  const totalAppts = s?.total ?? (d?.totalAppointments?.value ?? 0)
  const apptChange = d?.totalAppointments?.change ?? 0

  const completedAppts = s?.completed ?? (totalAppts > 0 ? totalAppts : 0)
  const completionRate = d?.completionRate?.value ?? (totalAppts > 0 ? Math.round((completedAppts / totalAppts) * 100) : 0)

  const cancelledAppts = s?.cancelled ?? 0
  const waitTime = d?.averageWaitingMinute?.value ?? 0

  return [
    {
      label: 'Tổng lượt khám',
      value: totalAppts.toLocaleString('vi-VN'),
      change: apptChange >= 0 ? `+${apptChange}%` : `${apptChange}%`,
      isPositive: apptChange >= 0,
      icon: '📊',
    },
    {
      label: 'Khám hoàn thành',
      value: completedAppts.toLocaleString('vi-VN'),
      change: `${completionRate}% tỷ lệ`,
      isPositive: true,
      icon: '✅',
    },
    {
      label: 'Lịch hẹn đã hủy',
      value: cancelledAppts.toLocaleString('vi-VN'),
      change: totalAppts > 0 ? `${Math.round((cancelledAppts / totalAppts) * 100)}%` : '0%',
      isPositive: cancelledAppts === 0,
      icon: '⚠️',
    },
    {
      label: 'Thời gian chờ TB',
      value: `${waitTime} phút`,
      change: 'Thực tế',
      isPositive: true,
      icon: '⏱️',
    },
  ]
})

const filteredStats = computed(() => {
  if (selectedSpecialty.value === 'all') {
    return specialtyStats.value
  }
  return specialtyStats.value.filter((item) => item.name === selectedSpecialty.value)
})

const maxVisits = computed(() => {
  return Math.max(...filteredStats.value.map((item) => item.visits), 1)
})


// Daily chart series (shortened to 7 days for clean view when in week mode)
const dailyChart = computed(() => {
  const st = statistics.value
  if (!st || !st.chartLabels || st.chartLabels.length === 0) {
    return {
      labels: ['14/8', '15/8', '16/8', '17/8', '18/8', '19/8', '20/8'],
      data: [0, 0, 0, 0, 0, 0, 0],
    }
  }

  // If in week mode or default, present last 7 days
  const labels = period.value === 'week' ? st.chartLabels.slice(-7) : st.chartLabels
  const data = period.value === 'week' ? st.chartData.slice(-7) : st.chartData

  return {
    labels,
    data,
  }
})

const maxDailyCount = computed(() => {
  return Math.max(...dailyChart.value.data, 10)
})

// Status Distribution
const statusDistribution = computed(() => {
  const dist = statistics.value?.specialtyDistribution || []
  if (dist.length > 0) {
    return dist
  }
  return [
    { label: 'Hoàn thành', count: summary.value?.completed || 0 },
    { label: 'Đã check-in', count: summary.value?.checkedIn || 0 },
    { label: 'Đã hủy', count: summary.value?.cancelled || 0 },
  ]
})

function exportReport() {
  const headers = ['Chuyen khoa', 'Luot kham', 'Thoi gian TB (phut)', 'Ty le hoan thanh (%)']
  const rows = filteredStats.value.map((s) => [
    `"${s.name.replace(/"/g, '""')}"`,
    s.visits,
    s.avg,
    s.pct,
  ])

  // \uFEFF ensures Excel displays UTF-8 Vietnamese characters correctly
  const csvContent = '\uFEFF' + [headers.join(','), ...rows.map((e) => e.join(','))].join('\r\n')
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.setAttribute('href', url)
  link.setAttribute('download', `Bao_cao_kham_benh_${period.value}_${new Date().toISOString().slice(0, 10)}.csv`)
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)

  exportSuccess.value = true
  setTimeout(() => {
    exportSuccess.value = false
  }, 4000)
}
</script>

<template>
  <div class="space-y-6 font-sans">
    <!-- Header Title -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-slate-800 tracking-tight flex items-center gap-2.5">
          <span>Báo cáo & Phân tích</span>
          <span v-if="loading" class="inline-block h-4 w-4 animate-spin rounded-full border-2 border-[#0E4D92] border-t-transparent"></span>
        </h1>
        <p class="mt-1 text-sm text-slate-500">
          Tổng hợp lượt khám chữa bệnh, thời gian chờ và hiệu suất hoạt động phòng khám
        </p>
      </div>

      <div class="flex items-center gap-2.5">
        <select
          v-model="period"
          class="rounded-xl border border-slate-200 bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 shadow-2xs focus:border-[#0E4D92] focus:outline-none transition-colors"
        >
          <option value="week">7 ngày qua</option>
          <option value="month">30 ngày qua</option>
          <option value="quarter">90 ngày qua</option>
        </select>

        <BaseButton
          type="button"
          variant="outline"
          size="md"
          :loading="loading"
          @click="loadData"
        >
          🔄 Làm mới
        </BaseButton>

        <BaseButton
          type="button"
          variant="primary"
          size="md"
          @click="exportReport"
        >
          📥 Xuất CSV (Excel)
        </BaseButton>
      </div>
    </div>

    <!-- Alert Messages -->
    <div v-if="exportSuccess">
      <BaseAlert type="success" message="Đã xuất báo cáo CSV thành công (hỗ trợ hiển thị tiếng Việt trên Excel)!" dismissible @dismiss="exportSuccess = false" />
    </div>
    <div v-if="errorMessage">
      <BaseAlert type="info" :message="errorMessage" dismissible @dismiss="errorMessage = ''" />
    </div>

    <!-- Summary KPI Cards -->
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <div
        v-for="item in kpiCards"
        :key="item.label"
        class="rounded-2xl border border-slate-200 bg-white p-5 shadow-2xs transition-all hover:shadow-md"
      >
        <div class="flex items-center justify-between">
          <div class="text-xs font-bold uppercase tracking-wider text-slate-400">
            {{ item.label }}
          </div>
          <span class="text-xl">{{ item.icon }}</span>
        </div>
        <div class="mt-2 text-2xl font-black text-slate-800 tracking-tight">
          {{ item.value }}
        </div>
        <div
          class="mt-1 text-xs font-semibold flex items-center gap-1"
          :class="item.isPositive ? 'text-emerald-600' : 'text-amber-600'"
        >
          <span>{{ item.change }}</span>
          <span class="text-slate-400 font-normal">so với kỳ trước</span>
        </div>
      </div>
    </div>

    <!-- Visual Chart Trends Section -->
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <!-- Daily Appointment Trends -->
      <div class="lg:col-span-2 rounded-2xl border border-slate-200 bg-white p-6 shadow-2xs min-w-0 overflow-hidden">
        <div class="flex items-center justify-between mb-4">
          <div>
            <h2 class="text-base font-bold text-slate-800">
              Xu hướng lượt khám theo ngày
            </h2>
            <p class="mt-0.5 text-xs text-slate-400">
              Biểu đồ số lượt tiếp nhận bệnh nhân theo từng ngày
            </p>
          </div>
          <span class="text-xs font-semibold px-2.5 py-1 rounded-full bg-blue-50 text-[#0E4D92] border border-blue-100 shrink-0">
            {{ period === 'week' ? '7 ngày gần nhất' : period === 'month' ? '30 ngày gần nhất' : '90 ngày gần nhất' }}
          </span>
        </div>

        <!-- Custom CSS/SVG Bar Chart with comfortable spacing -->
        <div class="w-full overflow-x-auto overflow-y-hidden pb-2">
          <div class="h-48 flex items-end justify-between gap-3 sm:gap-6 pt-6 border-b border-slate-100 pb-2 px-2 min-w-full">
            <div
              v-for="(count, idx) in dailyChart.data"
              :key="idx"
              class="flex-1 max-w-[44px] min-w-[24px] flex flex-col items-center gap-2 group relative"
            >
              <!-- Tooltip -->
              <div class="opacity-0 group-hover:opacity-100 transition-opacity absolute -top-8 bg-slate-800 text-white text-[11px] font-semibold py-1 px-2.5 rounded pointer-events-none whitespace-nowrap z-20 shadow-lg">
                {{ dailyChart.labels[idx] }}: {{ count }} lượt
              </div>
              
              <!-- Bar -->
              <div class="w-full bg-slate-100 rounded-t-lg h-36 flex items-end overflow-hidden">
                <div
                  class="w-full bg-gradient-to-t from-[#0E4D92] to-blue-400 rounded-t-lg transition-all duration-500 hover:brightness-110"
                  :style="{ height: `${Math.max((count / maxDailyCount) * 100, count > 0 ? 10 : 4)}%` }"
                />
              </div>
              <!-- Label -->
              <span class="text-xs font-semibold text-slate-600 truncate max-w-full text-center select-none">
                {{ dailyChart.labels[idx] }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Status Breakdown -->
      <div class="rounded-2xl border border-slate-200 bg-white p-6 shadow-2xs flex flex-col justify-between min-w-0">
        <div>
          <h2 class="text-base font-bold text-slate-800">
            Phân bổ trạng thái
          </h2>
          <p class="mt-0.5 text-xs text-slate-400">
            Tỷ lệ lịch hẹn theo trạng thái thực tế
          </p>

          <div class="mt-6 space-y-3.5">
            <div
              v-for="(statusItem, idx) in statusDistribution"
              :key="idx"
              class="flex items-center justify-between p-3 rounded-xl bg-slate-50/75 border border-slate-100 hover:bg-slate-50 transition-colors"
            >
              <div class="flex items-center gap-2.5">
                <span class="h-2.5 w-2.5 rounded-full shrink-0" :class="idx === 0 ? 'bg-emerald-500' : idx === 1 ? 'bg-blue-500' : 'bg-amber-500'"></span>
                <span class="text-xs font-semibold text-slate-700">{{ statusItem.label }}</span>
              </div>
              <span class="text-xs font-bold text-slate-800 shrink-0">{{ statusItem.count }} lượt</span>
            </div>
          </div>
        </div>

        <div class="mt-4 pt-4 border-t border-slate-100 text-center">
          <span class="text-xs text-slate-400">
            Dữ liệu đồng bộ trực tiếp từ hệ thống hàng đợi
          </span>
        </div>
      </div>
    </div>

    <!-- Performance by Specialty -->
    <div class="rounded-2xl border border-slate-200 bg-white p-6 shadow-2xs">
      <div class="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 class="text-base font-bold text-slate-800">
            Lượt khám theo chuyên khoa
          </h2>
          <p class="mt-0.5 text-xs text-slate-400">
            Số lượng bệnh nhân tiếp nhận và phân bổ năng lực khám
          </p>
        </div>

        <select
          v-model="selectedSpecialty"
          class="rounded-xl border border-slate-200 bg-slate-50/50 px-3 py-2 text-xs font-semibold text-slate-700 focus:border-[#0E4D92] focus:bg-white focus:outline-none transition-colors"
        >
          <option value="all">Tất cả chuyên khoa</option>
          <option
            v-for="s in specialties"
            :key="s.id"
            :value="s.name"
          >
            {{ s.name }}
          </option>
        </select>
      </div>

      <div class="space-y-5">
        <div
          v-for="item in filteredStats"
          :key="item.name"
          class="space-y-1.5"
        >
          <div class="flex items-center justify-between text-xs">
            <div class="flex items-center gap-2">
              <span class="font-bold text-slate-800">{{ item.name }}</span>
              <span class="text-slate-400">({{ item.visits }} lượt khám)</span>
            </div>
            <span class="font-bold text-[#0E4D92]">{{ item.pct }}%</span>
          </div>

          <div class="h-2.5 w-full overflow-hidden rounded-full bg-slate-100">
            <div
              class="h-full rounded-full bg-[#0E4D92] transition-all duration-500"
              :style="{ width: `${(item.visits / maxVisits) * 100}%` }"
            />
          </div>

          <p class="text-xs text-slate-400">
            Thời gian khám trung bình: <strong class="text-slate-600">{{ item.avg }} phút</strong>
          </p>
        </div>
      </div>
    </div>

    <!-- Detail Table -->
    <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-2xs">
      <div class="mb-4">
        <h2 class="text-base font-bold text-slate-800">
          Chi tiết hiệu suất theo chuyên khoa
        </h2>
      </div>

      <div class="overflow-x-auto rounded-xl border border-slate-100">
        <table class="w-full text-left text-sm border-collapse">
          <thead>
            <tr class="border-b border-slate-100 bg-slate-50/75 text-xs font-semibold uppercase tracking-wider text-slate-500">
              <th class="px-4 py-3.5">Chuyên khoa</th>
              <th class="px-4 py-3.5 text-right">Lượt khám</th>
              <th class="px-4 py-3.5 text-right">Thời gian TB</th>
              <th class="px-4 py-3.5 text-right">Tỷ lệ hoàn thành</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            <tr
              v-for="item in filteredStats"
              :key="item.name"
              class="hover:bg-slate-50/80 transition-colors"
            >
              <td class="px-4 py-3.5 font-bold text-slate-800">
                {{ item.name }}
              </td>
              <td class="px-4 py-3.5 text-right font-medium text-slate-700">
                {{ item.visits.toLocaleString('vi-VN') }}
              </td>
              <td class="px-4 py-3.5 text-right font-mono text-slate-600">
                {{ item.avg }} phút
              </td>
              <td class="px-4 py-3.5 text-right font-bold text-emerald-600">
                {{ item.pct }}%
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
