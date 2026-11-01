import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useUsersStore } from '@/stores/users'
import { usersApi } from '@/features/users/users.api'

vi.mock('@/features/users/users.api', () => ({
  usersApi: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
  },
}))

describe('Users Store Unit Tests', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('khởi tạo store với trạng thái mặc định', () => {
    const store = useUsersStore()
    expect(store.items).toEqual([])
    expect(store.status).toBe('idle')
    expect(store.error).toBeNull()
    expect(store.mutating).toBe(false)
  })

  it('fetchAll tải danh sách tài khoản thành công', async () => {
    const mockUsers = [
      { id: 'u-1', username: 'admin1', fullName: 'Quản trị viên', email: 'admin@clinic.com', role: 'Admin' },
      { id: 'u-2', username: 'doctor1', fullName: 'Bác sĩ Nam', email: 'nam@clinic.com', role: 'Doctor' },
    ]
    vi.mocked(usersApi.list).mockResolvedValue({ items: mockUsers as any })

    const store = useUsersStore()
    await store.fetchAll()

    expect(usersApi.list).toHaveBeenCalled()
    expect(store.items).toHaveLength(2)
    expect(store.status).toBe('idle')
    expect(store.error).toBeNull()
  })

  it('fetchAll bắt lỗi khi API thất bại', async () => {
    vi.mocked(usersApi.list).mockRejectedValue(new Error('Mất kết nối'))

    const store = useUsersStore()
    await store.fetchAll()

    expect(store.status).toBe('error')
    expect(store.error).toBe('Mất kết nối')
    expect(store.items).toEqual([])
  })

  it('filtered lọc người dùng theo tên, username hoặc email', () => {
    const store = useUsersStore()
    store.items = [
      { id: 'u-1', username: 'an_nguyen', fullName: 'Nguyễn Văn An', email: 'an@gmail.com' } as any,
      { id: 'u-2', username: 'binh_tran', fullName: 'Trần Văn Bình', email: 'binh@gmail.com' } as any,
      { id: 'u-3', username: 'cuong_le', fullName: 'Lê Cường', email: 'cuong@clinic.com' } as any,
    ]

    // Tìm theo tên
    store.search = 'bình'
    expect(store.filtered).toHaveLength(1)
    expect(store.filtered[0]?.username).toBe('binh_tran')

    // Tìm theo username
    store.search = 'an_'
    expect(store.filtered).toHaveLength(1)
    expect(store.filtered[0]?.fullName).toBe('Nguyễn Văn An')

    // Tìm theo domain email
    store.search = 'clinic.com'
    expect(store.filtered).toHaveLength(1)
    expect(store.filtered[0]?.username).toBe('cuong_le')
  })

  it('create thêm người dùng mới vào đầu danh sách', async () => {
    const store = useUsersStore()
    store.items = [{ id: 'u-1', username: 'user1' } as any]

    const newUser = { id: 'u-2', username: 'user2', fullName: 'Người dùng 2' }
    vi.mocked(usersApi.create).mockResolvedValue(newUser as any)

    await store.create({ username: 'user2', password: '123' } as any)

    expect(usersApi.create).toHaveBeenCalled()
    expect(store.items).toHaveLength(2)
    expect(store.items[0]?.id).toBe('u-2')
  })

  it('update cập nhật thông tin người dùng trong danh sách', async () => {
    const store = useUsersStore()
    store.items = [
      { id: 'u-1', username: 'user1', fullName: 'Tên Cũ' } as any,
    ]

    vi.mocked(usersApi.update).mockResolvedValue({ fullName: 'Tên Mới' } as any)

    await store.update('u-1', { fullName: 'Tên Mới' } as any)

    expect(usersApi.update).toHaveBeenCalledWith('u-1', { fullName: 'Tên Mới' })
    expect(store.items[0]?.fullName).toBe('Tên Mới')
  })

  it('remove xóa người dùng khỏi danh sách', async () => {
    const store = useUsersStore()
    store.items = [
      { id: 'u-1', username: 'user1' } as any,
      { id: 'u-2', username: 'user2' } as any,
    ]

    vi.mocked(usersApi.remove).mockResolvedValue()

    await store.remove('u-1')

    expect(usersApi.remove).toHaveBeenCalledWith('u-1')
    expect(store.items).toHaveLength(1)
    expect(store.items[0]?.id).toBe('u-2')
  })
})
