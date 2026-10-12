export interface ValueWithChange<T = number> {
  value: T
  change?: number
}

export interface DashboardData {
  totalAppointments?: ValueWithChange<number>
  totalPatients?: ValueWithChange<number>
  totalDoctors?: ValueWithChange<number>
  activeDoctor?: ValueWithChange<number>
  completionRate?: ValueWithChange<number>
  averageWaitingMinute?: ValueWithChange<number>
}

export interface StatisticsData {
  chartLabels: string[]
  chartData: number[]
  specialtyDistribution: { label: string; count: number }[]
}

export interface AppointmentExtended {
  id: string
  patientId: string
  patientName: string
  doctorId: string
  doctorName: string
  specialtyId: string
  specialtyName: string
  date: string
  timeSlot: string
  status: string
  reason?: string
}

export interface TotalAppointmentSummary {
  total: number
  confirmed: number
  checkedIn: number
  completed: number
  cancelled: number
  noShow: number
}
