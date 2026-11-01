import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import DoctorExaminationView from '@/views/doctor/DoctorExaminationView.vue'
import { doctorsApi } from '@/features/doctors/doctors.api'
import { medicalRecordsApi } from '@/features/medical-records/medical-records.api'
import { ApiError } from '@/lib/api/http'

const mockPush = vi.fn()

vi.mock('vue-router', () => ({
  useRouter: () => ({
    push: mockPush,
  }),
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({
    user: { name: 'BS Tran Van Test' },
  }),
}))

vi.mock('@/features/doctors/doctors.api', () => ({
  doctorsApi: {
    getOwnProfile: vi.fn(),
    getQueue: vi.fn(),
    callNext: vi.fn(),
    startExam: vi.fn(),
    completeExam: vi.fn(),
    skip: vi.fn(),
    getAppointment: vi.fn(),
  },
}))

vi.mock('@/features/medical-records/medical-records.api', () => ({
  medicalRecordsApi: {
    getByAppointment: vi.fn(),
    createDraft: vi.fn(),
  },
}))

describe('DoctorExaminationView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(doctorsApi.getOwnProfile).mockResolvedValue({
      id: 'doc-1',
      fullName: 'BS Tran Van Test',
      specialty: 'Noi tong quat',
    } as any)
  })

  it('tải hồ sơ bác sĩ và hàng đợi khi mount', async () => {
    const mockTickets = [
      { id: 't-1', appointmentId: 'a-1', queueNumber: 1, patientName: 'Nguyen Van A', status: 'Waiting' },
      { id: 't-2', appointmentId: 'a-2', queueNumber: 2, patientName: 'Tran Thi B', status: 'Called' },
    ]
    vi.mocked(doctorsApi.getQueue).mockResolvedValue(mockTickets as any)

    const wrapper = mount(DoctorExaminationView)
    await flushPromises()

    expect(doctorsApi.getOwnProfile).toHaveBeenCalled()
    expect(doctorsApi.getQueue).toHaveBeenCalledWith('doc-1')
    expect(wrapper.text()).toContain('Nguyen Van A')
    expect(wrapper.text()).toContain('Tran Thi B')
    expect(wrapper.text()).toContain('BS Tran Van Test')
    wrapper.unmount()
  })

  it('gọi callNext khi bấm nút "Gọi số tiếp theo"', async () => {
    const mockTickets = [
      { id: 't-1', appointmentId: 'a-1', queueNumber: 1, patientName: 'Nguyen Van A', status: 'Waiting' },
    ]
    vi.mocked(doctorsApi.getQueue).mockResolvedValue(mockTickets as any)
    vi.mocked(doctorsApi.callNext).mockResolvedValue({ id: 't-1', status: 'Called' } as any)

    const wrapper = mount(DoctorExaminationView)
    await flushPromises()

    const callNextBtn = wrapper.findAll('button').find((b) => b.text().includes('Gọi số tiếp theo'))
    expect(callNextBtn?.exists()).toBe(true)

    await callNextBtn!.trigger('click')
    await flushPromises()

    expect(doctorsApi.callNext).toHaveBeenCalledWith('doc-1')
    expect(wrapper.text()).toContain('Đã gọi bệnh nhân tiếp theo.')
    wrapper.unmount()
  })

  it('cho phép startExam khi ticket có trạng thái Called', async () => {
    const mockTickets = [
      { id: 't-1', appointmentId: 'a-1', queueNumber: 1, patientName: 'Nguyen Van A', status: 'Called' },
    ]
    vi.mocked(doctorsApi.getQueue).mockResolvedValue(mockTickets as any)
    vi.mocked(doctorsApi.startExam).mockResolvedValue({})

    const wrapper = mount(DoctorExaminationView)
    await flushPromises()

    const startBtn = wrapper.findAll('button').find((b) => b.text().includes('Bắt đầu khám'))
    expect(startBtn?.exists()).toBe(true)

    await startBtn!.trigger('click')
    await flushPromises()

    expect(doctorsApi.startExam).toHaveBeenCalledWith('t-1')
    wrapper.unmount()
  })

  it('cho phép skip khi ticket có trạng thái Called sau khi confirm', async () => {
    const mockTickets = [
      { id: 't-1', appointmentId: 'a-1', queueNumber: 1, patientName: 'Nguyen Van A', status: 'Called' },
    ]
    vi.mocked(doctorsApi.getQueue).mockResolvedValue(mockTickets as any)
    vi.mocked(doctorsApi.skip).mockResolvedValue({} as any)
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true)

    const wrapper = mount(DoctorExaminationView)
    await flushPromises()

    const skipBtn = wrapper.findAll('button').find((b) => b.text().includes('Bỏ qua'))
    expect(skipBtn?.exists()).toBe(true)

    await skipBtn!.trigger('click')
    await flushPromises()

    expect(confirmSpy).toHaveBeenCalled()
    expect(doctorsApi.skip).toHaveBeenCalledWith('t-1')
    wrapper.unmount()
  })

  it('cho phép completeExam khi ticket có trạng thái InProgress', async () => {
    const mockTickets = [
      { id: 't-1', appointmentId: 'a-1', queueNumber: 1, patientName: 'Nguyen Van A', status: 'InProgress' },
    ]
    vi.mocked(doctorsApi.getQueue).mockResolvedValue(mockTickets as any)
    vi.mocked(doctorsApi.completeExam).mockResolvedValue({})

    const wrapper = mount(DoctorExaminationView)
    await flushPromises()

    const completeBtn = wrapper.findAll('button').find((b) => b.text().includes('Hoàn tất'))
    expect(completeBtn?.exists()).toBe(true)

    await completeBtn!.trigger('click')
    await flushPromises()

    expect(doctorsApi.completeExam).toHaveBeenCalledWith('t-1')
    wrapper.unmount()
  })

  it('mở hồ sơ bệnh án: điều hướng đến hồ sơ sẵn có nếu đã tồn tại', async () => {
    const mockTickets = [
      { id: 't-1', appointmentId: 'a-1', queueNumber: 1, patientName: 'Nguyen Van A', status: 'InProgress' },
    ]
    vi.mocked(doctorsApi.getQueue).mockResolvedValue(mockTickets as any)
    vi.mocked(medicalRecordsApi.getByAppointment).mockResolvedValue({ id: 'rep-existing' } as any)

    const wrapper = mount(DoctorExaminationView)
    await flushPromises()

    const reportBtn = wrapper.findAll('button').find((b) => b.text().includes('Tạo hồ sơ bệnh án'))
    expect(reportBtn?.exists()).toBe(true)

    await reportBtn!.trigger('click')
    await flushPromises()

    expect(medicalRecordsApi.getByAppointment).toHaveBeenCalledWith('a-1')
    expect(mockPush).toHaveBeenCalledWith('/doctor/medical-records/rep-existing')
    wrapper.unmount()
  })

  it('mở hồ sơ bệnh án: tự động tạo draft nếu chưa tồn tại (404 ApiError)', async () => {
    const mockTickets = [
      { id: 't-1', appointmentId: 'a-1', queueNumber: 1, patientName: 'Nguyen Van A', status: 'InProgress' },
    ]
    vi.mocked(doctorsApi.getQueue).mockResolvedValue(mockTickets as any)
    vi.mocked(medicalRecordsApi.getByAppointment).mockRejectedValueOnce(
      new ApiError(404, { message: 'Not found' }),
    )
    vi.mocked(medicalRecordsApi.createDraft).mockResolvedValue({ id: 'rep-draft-1' } as any)

    const wrapper = mount(DoctorExaminationView)
    await flushPromises()

    const reportBtn = wrapper.findAll('button').find((b) => b.text().includes('Tạo hồ sơ bệnh án'))
    expect(reportBtn?.exists()).toBe(true)

    await reportBtn!.trigger('click')
    await flushPromises()

    expect(medicalRecordsApi.getByAppointment).toHaveBeenCalledWith('a-1')
    expect(medicalRecordsApi.createDraft).toHaveBeenCalledWith('a-1')
    expect(mockPush).toHaveBeenCalledWith('/doctor/medical-records/rep-draft-1')
    wrapper.unmount()
  })
})
