import { http } from '@/lib/api/http'
import { env } from '@/config/env'
import { doctorsApi } from '@/features/doctors/doctors.api'
import { specialtiesApi } from '@/features/specialties/specialties.api'
import { usersApi } from '@/features/users/users.api'
import type {
  AppointmentExtended,
  DashboardData,
  StatisticsData,
  TotalAppointmentSummary,
} from './admin.types'

export const adminApi = {
  // GET /admin/dashboard (hoặc tổng hợp từ các API thực tế có sẵn)
  async getDashboard(): Promise<DashboardData> {
    if (env.enableMock) {
      return {
        totalAppointments: { value: 1248, change: 8.4 },
        totalPatients: { value: 892, change: 5.2 },
        totalDoctors: { value: 24, change: 0 },
        activeDoctor: { value: 22, change: 0 },
        completionRate: { value: 94.2, change: 2.1 },
        averageWaitingMinute: { value: 18, change: -3.0 },
      }
    }

    try {
      const res = await http.get<any>('/admin/dashboard').then((r) => r.data)
      const data = res?.result !== undefined ? res.result : res
      if (data && (data.totalAppointments || data.totalDoctors || data.totalPatients)) {
        return data
      }
      throw new Error('Fallback required')
    } catch {
      // Fallback tổng hợp số liệu thực từ các API thực tế
      try {
        const [docsRes, usersRes] = await Promise.all([
          doctorsApi.list({ pageSize: 100 }),
          usersApi.list({ pageSize: 100 }),
        ])

        const totalDoctors = docsRes?.totalCount ?? docsRes?.items?.length ?? 0
        const activeDoctor = (docsRes?.items || []).filter((d) => d.status === 0 || d.status === 'Active' || d.status === 'active').length
        const totalPatients = (usersRes?.items || []).filter((u) => u.roles?.some((r) => r.toLowerCase() === 'patient')).length || usersRes?.totalCount || 0
        const totalAppointments = 0

        return {
          totalAppointments: { value: totalAppointments, change: 0 },
          totalPatients: { value: totalPatients, change: 0 },
          totalDoctors: { value: totalDoctors, change: 0 },
          activeDoctor: { value: activeDoctor, change: 0 },
          completionRate: { value: totalAppointments > 0 ? 100 : 0, change: 0 },
          averageWaitingMinute: { value: 15, change: 0 },
        }
      } catch {
        return {
          totalAppointments: { value: 0, change: 0 },
          totalPatients: { value: 0, change: 0 },
          totalDoctors: { value: 0, change: 0 },
          activeDoctor: { value: 0, change: 0 },
          completionRate: { value: 0, change: 0 },
          averageWaitingMinute: { value: 0, change: 0 },
        }
      }
    }
  },

  // GET /admin/statistics?period=Week|Month
  async getStatistics(period: 'Week' | 'Month' = 'Week'): Promise<StatisticsData> {
    if (env.enableMock) {
      const days = period === 'Week' ? ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'] : ['T1', 'T2', 'T3', 'T4']
      return {
        chartLabels: days,
        chartData: [42, 38, 51, 47, 55, 28, 12],
        specialtyDistribution: [
          { label: 'Hoàn thành', count: 215 },
          { label: 'Đang chờ', count: 87 },
          { label: 'Đã hủy', count: 34 },
        ],
      }
    }

    try {
      const res = await http.get<any>('/admin/statistics', { params: { period } }).then((r) => r.data)
      const data = res?.result !== undefined ? res.result : res
      const byDay = data?.appointmentsByDay || data?.AppointmentsByDay || []
      const byStatus = data?.appointmentsByStatus || data?.AppointmentsByStatus || []

      const statusLabels: Record<number | string, string> = {
        0: 'Đang chờ',
        1: 'Đã xác nhận',
        2: 'Đã check-in',
        3: 'Đang khám',
        4: 'Hoàn thành',
        5: 'Đã hủy',
        6: 'Vắng mặt',
      }

      return {
        chartLabels: byDay.map((d: any) => {
          const dateStr = d.date || d.Date
          if (!dateStr) return ''
          const dt = new Date(dateStr)
          return `${dt.getDate()}/${dt.getMonth() + 1}`
        }),
        chartData: byDay.map((d: any) => d.count ?? d.Count ?? 0),
        specialtyDistribution: byStatus.map((s: any) => {
          const rawStatus = s.status ?? s.Status ?? 0
          return {
            label: statusLabels[rawStatus] || `Trạng thái ${rawStatus}`,
            count: s.count ?? s.Count ?? 0,
          }
        }),
      }
    } catch {
      // Fallback vẽ biểu đồ từ ca trực thực tế
      try {
        const specs = await specialtiesApi.list({ pageSize: 10 })
        const dist = (specs.items || []).map((s) => ({
          label: s.name,
          count: 1,
        }))
        const days = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN']
        return {
          chartLabels: days,
          chartData: [0, 0, 0, 0, 0, 0, 0],
          specialtyDistribution: dist.length ? dist : [{ label: 'Đa khoa', count: 0 }],
        }
      } catch {
        return {
          chartLabels: [],
          chartData: [],
          specialtyDistribution: [],
        }
      }
    }
  },

  // GET /admin/appointments?date=YYYY-MM-DD
  async getAppointmentsByDate(date?: string, pageNumber = 1, pageSize = 10): Promise<{ items: AppointmentExtended[]; totalPages: number; totalCount: number }> {
    if (env.enableMock) {
      return {
        items: [],
        totalPages: 1,
        totalCount: 0,
      }
    }

    try {
      const res = await http
        .get<any>('/admin/appointments', {
          params: {
            Date: date || new Date().toISOString().split('T')[0],
            PageNumber: pageNumber,
            PageSize: pageSize,
          },
        })
        .then((r) => r.data)

      const data = res?.result !== undefined ? res.result : res
      const rawItems = data?.items || data?.Items || []
      const items: AppointmentExtended[] = rawItems.map((a: any) => ({
        id: String(a.id || a.Id || ''),
        patientId: String(a.patientId || a.PatientId || ''),
        patientName: a.patientName || a.PatientName || a.patient?.person?.fullName || 'Bệnh nhân',
        doctorId: String(a.doctorId || a.DoctorId || ''),
        doctorName: a.doctorName || a.DoctorName || a.workSchedule?.doctor?.employee?.person?.fullName || 'Bác sĩ',
        specialtyId: String(a.specialtyId || a.SpecialtyId || ''),
        specialtyName: a.specialtyName || a.SpecialtyName || 'Đa khoa',
        date: a.date || a.Date || date || new Date().toISOString().split('T')[0],
        timeSlot: a.timeSlot || a.TimeSlot || '08:00',
        status: String(a.status ?? a.Status ?? 'Confirmed'),
        reason: a.reason || a.Reason,
      }))

      return {
        items,
        totalPages: data?.totalPages || data?.TotalPages || 1,
        totalCount: data?.totalCount ?? data?.TotalCount ?? items.length,
      }
    } catch {
      return {
        items: [],
        totalPages: 1,
        totalCount: 0,
      }
    }
  },

  // GET /admin/appointments/summary
  async getAppointmentSummary(
    startDate?: string,
    endDate?: string,
    doctorId?: string,
    specialtyId?: string,
  ): Promise<TotalAppointmentSummary> {
    try {
      const res = await http
        .get<any>('/admin/appointments/summary', {
          params: {
            StartDate: startDate,
            EndDate: endDate,
            DoctorId: doctorId,
            SpecialtyId: specialtyId,
          },
        })
        .then((r) => r.data)

      const data = res?.result !== undefined ? res.result : res
      if (!data) {
        return {
          total: 0,
          confirmed: 0,
          checkedIn: 0,
          completed: 0,
          cancelled: 0,
          noShow: 0,
        }
      }

      return {
        total: data.appointmentCount ?? data.AppointmentCount ?? data.total ?? 0,
        confirmed: data.appointmentOnlineCount ?? data.AppointmentOnlineCount ?? data.confirmed ?? 0,
        checkedIn: data.checkedIn ?? 0,
        completed: data.completedAppointments ?? data.CompletedAppointments ?? data.completed ?? 0,
        cancelled: data.canceledAppointments ?? data.CanceledAppointments ?? data.cancelled ?? 0,
        noShow: data.noShowAppointments ?? data.NoShowAppointments ?? data.noShow ?? 0,
      }
    } catch {
      return {
        total: 0,
        confirmed: 0,
        checkedIn: 0,
        completed: 0,
        cancelled: 0,
        noShow: 0,
      }
    }
  },
}

