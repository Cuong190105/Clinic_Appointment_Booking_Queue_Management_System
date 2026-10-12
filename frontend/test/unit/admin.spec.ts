import { describe, expect, it } from 'vitest'

import {
  ACCOUNTS,
  DOCTORS,
  SPECIALTIES,
  SCHEDULE_ROWS,
} from '@/features/admin/admin.mock'

describe('Admin - Accounts', () => {
  it('should have account mock data', () => {
    expect(ACCOUNTS.length).toBeGreaterThan(0)
  })

  it('should search account by name', () => {
    const keyword = 'Nguyễn Văn An'

    const result = ACCOUNTS.filter((account) =>
      account.name
        .toLowerCase()
        .includes(keyword.toLowerCase()),
    )

    expect(result).toHaveLength(1)
    expect(result[0].name).toBe('Nguyễn Văn An')
  })

  it('should filter accounts by role', () => {
    const result = ACCOUNTS.filter(
      (account) => account.role === 'doctor',
    )

    expect(result.length).toBeGreaterThan(0)
    expect(
      result.every((account) => account.role === 'doctor'),
    ).toBe(true)
  })

  it('should filter accounts by status', () => {
    const result = ACCOUNTS.filter(
      (account) => account.status === 'inactive',
    )

    expect(result.length).toBeGreaterThan(0)
    expect(
      result.every(
        (account) => account.status === 'inactive',
      ),
    ).toBe(true)
  })
})

describe('Admin - Doctors', () => {
  it('should have doctor mock data', () => {
    expect(DOCTORS.length).toBeGreaterThan(0)
  })

  it('should search doctor by name', () => {
    const keyword = 'Phạm Minh Tuấn'

    const result = DOCTORS.filter((doctor) =>
      doctor.name
        .toLowerCase()
        .includes(keyword.toLowerCase()),
    )

    expect(result).toHaveLength(1)
    expect(result[0].name).toBe(
      'BS. Phạm Minh Tuấn',
    )
  })

  it('should filter doctors by specialty', () => {
    const result = DOCTORS.filter(
      (doctor) => doctor.specialty === 'Tim mạch',
    )

    expect(result.length).toBeGreaterThan(0)
    expect(
      result.every(
        (doctor) => doctor.specialty === 'Tim mạch',
      ),
    ).toBe(true)
  })

  it('should filter doctors by status', () => {
    const result = DOCTORS.filter(
      (doctor) => doctor.status === 'inactive',
    )

    expect(result.length).toBeGreaterThan(0)
    expect(
      result.every(
        (doctor) => doctor.status === 'inactive',
      ),
    ).toBe(true)
  })
})

describe('Admin - Specialties', () => {
  it('should have specialty mock data', () => {
    expect(SPECIALTIES.length).toBeGreaterThan(0)
  })

  it('should search specialty by name', () => {
    const keyword = 'Tim mạch'

    const result = SPECIALTIES.filter((specialty) =>
      specialty.name
        .toLowerCase()
        .includes(keyword.toLowerCase()),
    )

    expect(result).toHaveLength(1)
    expect(result[0].name).toBe('Tim mạch')
  })

  it('should filter specialties by status', () => {
    const result = SPECIALTIES.filter(
      (specialty) => specialty.status === 'inactive',
    )

    expect(result.length).toBeGreaterThan(0)
    expect(
      result.every(
        (specialty) => specialty.status === 'inactive',
      ),
    ).toBe(true)
  })
})

describe('Admin - Schedule', () => {
  it('should have schedule data', () => {
    expect(SCHEDULE_ROWS.length).toBeGreaterThan(0)
  })

  it('should search schedule by doctor name', () => {
    const keyword = 'Phạm Minh Tuấn'

    const result = SCHEDULE_ROWS.filter((doctor) =>
      doctor.name
        .toLowerCase()
        .includes(keyword.toLowerCase()),
    )

    expect(result).toHaveLength(1)
    expect(result[0].name).toBe(
      'BS. Phạm Minh Tuấn',
    )
  })

  it('should filter schedule by specialty', () => {
    const result = SCHEDULE_ROWS.filter(
      (doctor) => doctor.spec === 'Tim mạch',
    )

    expect(result.length).toBeGreaterThan(0)
    expect(
      result.every(
        (doctor) => doctor.spec === 'Tim mạch',
      ),
    ).toBe(true)
  })
})
