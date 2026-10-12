import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'

const { authStore, patientStore, routerPush } = vi.hoisted(() => ({
  authStore: {
    user: {
      id: 1,
      name: 'Nguyễn Văn A',
      role: 'Patient',
    },
  },
  patientStore: {
    doctors: [
      {
        id: 1,
        name: 'BS. Trần Minh Hùng',
        specialty: 'Nội tổng quát',
        room: 'P.101',
      },
      {
        id: 2,
        name: 'BS. Lê Hồng Đăng',
        specialty: 'Tim mạch',
        room: 'P.205',
      },
    ],
    slots: [
      { id: 1, time: '08:00', available: true },
      { id: 2, time: '08:30', available: true },
      { id: 3, time: '09:00', available: false },
    ],
    upcomingAppointments: [] as any[],
    appointmentsLoading: false,
    appointmentsError: null,
    clearSlots: vi.fn(),
    loadDoctors: vi.fn().mockResolvedValue(undefined),
    loadSlots: vi.fn().mockResolvedValue(undefined),
    createAppointment: vi.fn().mockResolvedValue({
      id: 99,
      doctorId: 1,
      doctorName: 'BS. Trần Minh Hùng',
      specialty: 'Nội tổng quát',
      appointmentDate: '2026-08-20',
      appointmentTime: '08:00',
      status: 'Pending',
      queueNumber: 'A-12',
    }),
    cancelAppointment: vi.fn(),
    loadAppointments: vi.fn(),
    loadQueue: vi.fn(),
    loadHistory: vi.fn(),
  },
  routerPush: vi.fn(),
}))

vi.mock('vue-router', () => ({
  useRouter: () => ({
    push: routerPush,
  }),
  RouterLink: {
    name: 'RouterLink',
    props: ['to'],
    template: '<a><slot /></a>',
  },
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => authStore,
}))

vi.mock('@/stores/patient', () => ({
  usePatientStore: () => patientStore,
}))

import PatientBookingView from '@/views/patient/PatientBookingView.vue'

describe('PatientBookingView', () => {
  beforeEach(() => {
    routerPush.mockClear()
    authStore.user = {
      id: 1,
      name: 'Nguyễn Văn A',
      role: 'Patient',
    }

    patientStore.doctors = [
      {
        id: 1,
        name: 'BS. Trần Minh Hùng',
        specialty: 'Nội tổng quát',
        room: 'P.101',
      },
      {
        id: 2,
        name: 'BS. Lê Hồng Đăng',
        specialty: 'Tim mạch',
        room: 'P.205',
      },
    ]
    patientStore.slots = [
      { id: 1, time: '08:00', available: true },
      { id: 2, time: '08:30', available: true },
      { id: 3, time: '09:00', available: false },
    ]
    patientStore.appointmentsLoading = false
    patientStore.appointmentsError = null
    patientStore.clearSlots.mockClear()
    patientStore.loadDoctors.mockClear()
    patientStore.loadSlots.mockClear()
    patientStore.createAppointment.mockClear()
  })

  it('loads danh sách bác sĩ khi mở trang đặt lịch', async () => {
    const wrapper = mount(PatientBookingView)

    await flushPromises()

    expect(patientStore.loadDoctors).toHaveBeenCalledTimes(1)
    expect(wrapper.text()).toContain('BS. Trần Minh Hùng')
    expect(wrapper.text()).toContain('Nội tổng quát')
  })

  it('cho phép bệnh nhân chọn bác sĩ, ngày và giờ rồi đi tới bước thông tin', async () => {
    const wrapper = mount(PatientBookingView)
    await flushPromises()

    const vm = wrapper.vm as any
    await vm.selectDoctor(1)
    vm.appointmentDate = '2026-08-20'
    await vm.changeDate()
    vm.chooseSlot({ id: 1, time: '08:00' })
    await nextTick()

    expect(patientStore.loadSlots).toHaveBeenCalledWith(1, '2026-08-20')

    const nextButton = Array.from(wrapper.findAll('button')).find((btn) =>
      btn.text().includes('Tiếp theo'),
    )
    expect(nextButton).toBeTruthy()
    await nextButton!.trigger('click')

    expect(wrapper.text()).toContain('Thông tin bệnh nhân')
    expect(wrapper.text()).toContain('Lý do khám / Triệu chứng')
  })

  it('không cho đi tiếp nếu chưa chọn bác sĩ, ngày hoặc giờ', async () => {
    const wrapper = mount(PatientBookingView)
    const vm = wrapper.vm as any

    vm.nextStep()
    expect(vm.step).toBe(1)

    await vm.selectDoctor(1)
    vm.nextStep()
    expect(vm.step).toBe(1)

    vm.appointmentDate = '2026-08-20'
    await vm.changeDate()
    vm.nextStep()
    expect(vm.step).toBe(1)
  })

  it('bệnh nhân có thể hủy lịch hẹn sắp tới từ màn hình home', async () => {
    patientStore.cancelAppointment = vi.fn().mockImplementation(async (id: number) => {
      patientStore.upcomingAppointments = patientStore.upcomingAppointments.map((appointment: any) =>
        appointment.id === id ? { ...appointment, status: 'Cancelled' } : appointment,
      )
    })
    patientStore.upcomingAppointments = [
      {
        id: 99,
        doctorId: 1,
        doctorName: 'BS. Trần Minh Hùng',
        specialty: 'Nội tổng quát',
        appointmentDate: '2026-08-20',
        appointmentTime: '08:00',
        status: 'Pending',
        queueNumber: 'A-12',
      },
    ]
    patientStore.loadAppointments = vi.fn().mockResolvedValue(undefined)
    patientStore.loadQueue = vi.fn().mockResolvedValue(undefined)
    patientStore.loadHistory = vi.fn().mockResolvedValue(undefined)

    const { default: PatientHomeView } = await import('@/views/patient/PatientHomeView.vue')
    const wrapper = mount(PatientHomeView, {
      global: {
        stubs: {
          RouterLink: {
            template: '<a><slot /></a>',
          },
        },
      },
    })

    const cancelButton = Array.from(wrapper.findAll('button')).find((button) =>
      button.text().includes('Hủy lịch'),
    )

    expect(cancelButton).toBeTruthy()
    await cancelButton!.trigger('click')

    expect(patientStore.cancelAppointment).toHaveBeenCalledWith(99)
  })

  it('xác nhận đặt lịch thành công và hiển thị màn hình success', async () => {
    const wrapper = mount(PatientBookingView)
    await flushPromises()

    const vm = wrapper.vm as any
    await vm.selectDoctor(1)
    vm.appointmentDate = '2026-08-20'
    await vm.changeDate()
    vm.chooseSlot({ id: 1, time: '08:00' })
    await nextTick()

    const nextButton = Array.from(wrapper.findAll('button')).find((btn) =>
      btn.text().includes('Tiếp theo'),
    )
    await nextButton!.trigger('click')

    const symptomInput = wrapper.get('textarea')
    await symptomInput.setValue('Sốt, ho nhẹ 3 ngày')

    const nextButton2 = Array.from(wrapper.findAll('button')).find((btn) =>
      btn.text().includes('Tiếp theo'),
    )
    await nextButton2!.trigger('click')

    const confirmButton = Array.from(wrapper.findAll('button')).find((btn) =>
      btn.text().includes('Xác nhận đặt lịch'),
    )
    expect(confirmButton).toBeTruthy()

    await confirmButton!.trigger('click')
    await flushPromises()

    expect(patientStore.createAppointment).toHaveBeenCalledWith(
      expect.objectContaining({
        doctorId: 1,
        appointmentDate: '2026-08-20',
        appointmentTime: '08:00',
        symptoms: 'Sốt, ho nhẹ 3 ngày',
      }),
    )

    expect(wrapper.text()).toContain('Đặt lịch thành công!')
    expect(wrapper.text()).toContain('A-12')
    expect(wrapper.text()).toContain('BS. Trần Minh Hùng')
  })

  it('giữ trạng thái không thành công khi API đặt lịch lỗi', async () => {
    patientStore.createAppointment.mockRejectedValueOnce(new Error('API error'))

    const wrapper = mount(PatientBookingView)
    await flushPromises()

    const vm = wrapper.vm as any
    await vm.selectDoctor(1)
    vm.appointmentDate = '2026-08-20'
    await vm.changeDate()
    vm.chooseSlot({ id: 1, time: '08:00' })
    vm.nextStep()
    vm.symptoms = 'Đau ngực'
    vm.nextStep()

    await vm.confirmBooking()
    await flushPromises()

    expect(vm.success).toBe(false)
    expect(patientStore.createAppointment).toHaveBeenCalledTimes(1)
  })
})
