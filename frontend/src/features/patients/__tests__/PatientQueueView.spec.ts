import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const patientStore = vi.hoisted(() => ({
  queue: null as any,
  queueError: null as string | null,
  loadQueue: vi.fn(),
}))

vi.mock('@/stores/patient', () => ({
  usePatientStore: () => patientStore,
}))

describe('PatientQueueView', () => {
  beforeEach(() => {
    patientStore.queue = null
    patientStore.queueError = null
    patientStore.loadQueue.mockReset()

    vi.useFakeTimers()

    Object.defineProperty(globalThis, 'Notification', {
      value: vi.fn().mockImplementation((title: string, options?: { body?: string }) => ({
        title,
        options,
      })),
      writable: true,
      configurable: true,
    })

    Object.defineProperty(globalThis.Notification, 'permission', {
      value: 'granted',
      writable: true,
      configurable: true,
    })

    Object.defineProperty(globalThis.Notification, 'requestPermission', {
      value: vi.fn().mockResolvedValue('granted'),
      writable: true,
      configurable: true,
    })
  })

  afterEach(() => {
    vi.runOnlyPendingTimers()
    vi.useRealTimers()
  })

  it('loads queue on mount and calls patient.loadQueue', async () => {
    patientStore.loadQueue.mockImplementation(async () => {
      patientStore.queue = {
        myTicket: 'A-12',
        position: 3,
        estimatedWaitMinutes: 15,
        doctorName: 'BS. Trần Minh Hùng',
        appointmentTime: '08:00',
        currentTicket: 'A-10',
        entries: [
          {
            ticket: 'A-10',
            patientName: 'Nguyễn Văn B',
            doctorId: 1,
            doctorName: 'BS. Trần Minh Hùng',
            appointmentTime: '08:00',
            status: 'Waiting',
            estimatedWaitMinutes: 10,
            position: 1,
            urgent: false,
          },
        ],
      }
    })

    const wrapper = mount(await import('@/views/patient/PatientQueueView.vue').then((m) => m.default))

    await flushPromises()

    expect(patientStore.loadQueue).toHaveBeenCalledTimes(1)
    expect(patientStore.queue?.myTicket).toBe('A-12')
    wrapper.unmount()
  })

  it('does not expose a cancel-queue action on the current queue screen', async () => {
    patientStore.queue = {
      myTicket: 'A-12',
      position: 2,
      estimatedWaitMinutes: 5,
      doctorName: 'BS. Trần Minh Hùng',
      appointmentTime: '08:00',
      currentTicket: 'A-10',
      entries: [
        {
          ticket: 'A-10',
          patientName: 'Nguyễn Văn B',
          doctorId: 1,
          doctorName: 'BS. Trần Minh Hùng',
          appointmentTime: '08:00',
          status: 'Waiting',
          estimatedWaitMinutes: 5,
          position: 1,
          urgent: false,
        },
        {
          ticket: 'A-12',
          patientName: 'Nguyễn Văn A',
          doctorId: 1,
          doctorName: 'BS. Trần Minh Hùng',
          appointmentTime: '08:00',
          status: 'Waiting',
          estimatedWaitMinutes: 5,
          position: 2,
          urgent: false,
        },
      ],
    }

    const wrapper = mount(await import('@/views/patient/PatientQueueView.vue').then((m) => m.default))
    await flushPromises()

    expect(wrapper.text()).not.toContain('Hủy hàng đợi')
    expect(wrapper.text()).not.toContain('Rời hàng đợi')
    wrapper.unmount()
  })

  it('sends notification when patient is near the front of the queue', async () => {
    patientStore.loadQueue.mockImplementation(async () => {
      patientStore.queue = {
        myTicket: 'A-12',
        position: 2,
        estimatedWaitMinutes: 5,
        doctorName: 'BS. Trần Minh Hùng',
        appointmentTime: '08:00',
        currentTicket: 'A-10',
        entries: [
          {
            ticket: 'A-10',
            patientName: 'Nguyễn Văn B',
            doctorId: 1,
            doctorName: 'BS. Trần Minh Hùng',
            appointmentTime: '08:00',
            status: 'Waiting',
            estimatedWaitMinutes: 5,
            position: 1,
            urgent: false,
          },
          {
            ticket: 'A-12',
            patientName: 'Nguyễn Văn A',
            doctorId: 1,
            doctorName: 'BS. Trần Minh Hùng',
            appointmentTime: '08:00',
            status: 'Waiting',
            estimatedWaitMinutes: 5,
            position: 2,
            urgent: false,
          },
        ],
      }
    })

    const wrapper = mount(await import('@/views/patient/PatientQueueView.vue').then((m) => m.default))
    await flushPromises()

    expect(globalThis.Notification).toHaveBeenCalledWith(
      'Sắp đến lượt khám!',
      expect.objectContaining({
        body: expect.stringContaining('vị trí thứ #2'),
        icon: '/favicon.ico',
      }),
    )

    wrapper.unmount()
  })

  it('does not send notification when patient is not near the front of the queue', async () => {
    patientStore.loadQueue.mockImplementation(async () => {
      patientStore.queue = {
        myTicket: 'A-12',
        position: 5,
        estimatedWaitMinutes: 30,
        doctorName: 'BS. Trần Minh Hùng',
        appointmentTime: '08:00',
        currentTicket: 'A-10',
        entries: [
          {
            ticket: 'A-10',
            patientName: 'Nguyễn Văn B',
            doctorId: 1,
            doctorName: 'BS. Trần Minh Hùng',
            appointmentTime: '08:00',
            status: 'Waiting',
            estimatedWaitMinutes: 10,
            position: 1,
            urgent: false,
          },
          {
            ticket: 'A-12',
            patientName: 'Nguyễn Văn A',
            doctorId: 1,
            doctorName: 'BS. Trần Minh Hùng',
            appointmentTime: '08:00',
            status: 'Waiting',
            estimatedWaitMinutes: 30,
            position: 5,
            urgent: false,
          },
        ],
      }
    })

    const wrapper = mount(await import('@/views/patient/PatientQueueView.vue').then((m) => m.default))
    await flushPromises()

    expect(globalThis.Notification).not.toHaveBeenCalledWith(
      'Sắp đến lượt khám!',
      expect.any(Object),
    )
    wrapper.unmount()
  })

  it('marks error state when queue API fails', async () => {
    patientStore.loadQueue.mockRejectedValueOnce(new Error('Queue API error'))

    const wrapper = mount(await import('@/views/patient/PatientQueueView.vue').then((m) => m.default))
    await flushPromises()

    const vm = wrapper.vm as any
    expect(vm.isError).toBe(true)
    wrapper.unmount()
  })

  it('re-fetches queue after 30 seconds while mounted', async () => {
    patientStore.loadQueue.mockResolvedValue(undefined)

    const wrapper = mount(await import('@/views/patient/PatientQueueView.vue').then((m) => m.default))
    await flushPromises()

    expect(patientStore.loadQueue).toHaveBeenCalledTimes(1)

    vi.advanceTimersByTime(30000)
    await flushPromises()

    expect(patientStore.loadQueue).toHaveBeenCalledTimes(2)
    wrapper.unmount()
  })

  it('can request notification permission', async () => {
    const wrapper = mount(await import('@/views/patient/PatientQueueView.vue').then((m) => m.default))

    const requestPermission = vi.spyOn(globalThis.Notification, 'requestPermission')
    const vm = wrapper.vm as any

    await vm.requestNotificationPermission?.()
    await flushPromises()

    expect(requestPermission).toHaveBeenCalledTimes(1)
    wrapper.unmount()
  })
})
