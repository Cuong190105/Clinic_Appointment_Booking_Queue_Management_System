import { http } from '@/lib/api/http'
import type {
  CreateUserInput,
  GetUsersParams,
  PagedUsersResponse,
  UpdateUserInput,
  User,
} from './users.types'

export const usersApi = {
  // GET /admin/users
  list: async (params?: GetUsersParams): Promise<PagedUsersResponse> => {
    try {
      const res = await http
        .get<any>('/admin/users', {
          params: {
            Search: params?.search || undefined,
            Role: params?.role || undefined,
            SortBy: params?.sortBy || 'fullName',
            OrderBy: params?.orderBy || 'asc',
            Gender: params?.gender,
            PageNumber: params?.pageNumber ?? 1,
            PageSize: params?.pageSize ?? 10,
          },
        })
        .then((r) => r.data)

      const data = res?.result !== undefined ? res.result : res
      const rawItems = data?.items || data?.Items || (Array.isArray(data) ? data : [])
      const items: User[] = rawItems.map((u: any) => ({
        id: String(u.userId || u.UserId || u.id || u.Id || ''),
        username: u.username || u.Username || '',
        fullName: u.fullName || u.FullName || u.name || u.Name || '',
        name: u.fullName || u.FullName || u.name || u.Name || '',
        phoneNumber: u.phoneNumber || u.PhoneNumber || null,
        email: u.email || u.Email || null,
        gender: typeof u.gender === 'string' ? (u.gender.toLowerCase() === 'female' ? 1 : 0) : (u.gender ?? 0),
        roles: Array.isArray(u.roles) ? u.roles : (Array.isArray(u.Roles) ? u.Roles : ['Patient']),
        isActive: u.isActive ?? u.IsActive ?? true,
        createdAt: u.createdAt || u.CreatedAt || '',
      }))

      // Đồng bộ trạng thái nhân viên (EmployeeStatus 2: Resigned -> isActive = false)
      try {
        const empRes = await http.get<any>('/employees', { params: { pageSize: 100 } }).then((r) => r.data)
        const empData = empRes?.result !== undefined ? empRes.result : empRes
        const empList = empData?.items || empData?.Items || (Array.isArray(empData) ? empData : [])

        if (Array.isArray(empList) && empList.length > 0) {
          items.forEach((item) => {
            const emp = empList.find(
              (e: any) =>
                (item.phoneNumber && e.phoneNumber === item.phoneNumber) ||
                (item.email && e.email === item.email) ||
                (item.fullName && e.fullName === item.fullName) ||
                String(e.id || e.Id) === item.id,
            )
            if (emp) {
              const statusVal = emp.status ?? emp.Status
              if (statusVal === 2 || statusVal === 'Resigned' || statusVal === 'Inactive') {
                item.isActive = false
              }
            }
          })
        }
      } catch {
        // ignore
      }

      const pageNumber = data?.pageNumber || data?.PageNumber || params?.pageNumber || 1
      const pageSize = data?.pageSize || data?.PageSize || params?.pageSize || 10
      const totalCount = data?.totalCount ?? data?.TotalCount ?? items.length
      const totalPages = data?.totalPages || data?.TotalPages || Math.ceil(totalCount / pageSize) || 1

      return {
        items,
        pageNumber,
        pageSize,
        totalCount,
        totalPages,
        hasPrevious: pageNumber > 1,
        hasNext: pageNumber < totalPages,
      }
    } catch {
      try {
        const briefList = await usersApi.listAllBrief()
        return {
          items: briefList,
          pageNumber: 1,
          pageSize: briefList.length || 10,
          totalCount: briefList.length,
          totalPages: 1,
          hasPrevious: false,
          hasNext: false,
        }
      } catch {
        return {
          items: [],
          pageNumber: 1,
          pageSize: 10,
          totalCount: 0,
          totalPages: 1,
          hasPrevious: false,
          hasNext: false,
        }
      }
    }
  },

  // GET /admin/users/all-brief
  listAllBrief: async (): Promise<User[]> => {
    const res = await http.get<any>('/admin/users/all-brief').then((r) => r.data)
    const data = (res as any)?.result !== undefined ? (res as any).result : res
    const rawList = Array.isArray(data) ? data : (data?.items || [])

    return rawList.map((u: any) => ({
      id: String(u.userId || u.UserId || u.id || u.Id || ''),
      username: u.username || u.Username || '',
      fullName: u.fullName || u.FullName || '',
      name: u.fullName || u.FullName || '',
      roles: Array.isArray(u.roles) ? u.roles : (Array.isArray(u.Roles) ? u.Roles : ['Patient']),
      isActive: u.isActive ?? u.IsActive ?? true,
    }))
  },

  // GET /admin/users/{userId}
  get: async (id: string): Promise<User> => {
    const res = await http.get<any>(`/admin/users/${id}`).then((r) => r.data)
    const u = res?.result !== undefined ? res.result : res
    return {
      id: String(u.userId || u.UserId || u.id || u.Id || ''),
      username: u.username || u.Username || '',
      fullName: u.fullName || u.FullName || u.name || u.Name || '',
      name: u.fullName || u.FullName || u.name || u.Name || '',
      phoneNumber: u.phoneNumber || u.PhoneNumber || null,
      email: u.email || u.Email || null,
      gender: typeof u.gender === 'string' ? (u.gender.toLowerCase() === 'female' ? 1 : 0) : (u.gender ?? 0),
      roles: Array.isArray(u.roles) ? u.roles : (Array.isArray(u.Roles) ? u.Roles : ['Patient']),
      isActive: u.isActive ?? u.IsActive ?? true,
      createdAt: u.createdAt || u.CreatedAt || '',
    }
  },

  // POST /employees (tạo nhân viên Lễ tân / Admin)
  create: (input: CreateUserInput) => {
    const payload = {
      username: input.username,
      password: input.password,
      fullName: input.fullName || input.name || input.username,
      phoneNumber: input.phoneNumber || '0901234567',
      email: input.email || `${input.username.toLowerCase()}@clinic.vn`,
      dateOfBirth: '2000-01-01',
      gender: 0,
      address: 'Cơ sở khám chữa bệnh',
      hireDate: new Date().toISOString().split('T')[0],
      roles: [input.role || 'Receptionist'],
    }
    return http.post<any>('/employees', payload).then((r) => r.data?.result ?? r.data)
  },

  // Cập nhật người dùng:
  // - Nếu là nhân viên đã tồn tại -> PUT /employees/{employeeId} và cập nhật trạng thái POST /employees/{employeeId}/status
  // - Nếu là bệnh nhân muốn phân quyền nhân viên -> POST /employees/from-user
  update: async (userId: string, input: UpdateUserInput) => {
    // 1. Lấy chi tiết user hiện tại
    const userDetail = await usersApi.get(userId).catch(() => null)

    // 2. Tìm Employee record nếu user đã là nhân viên
    let employeeData: any = null
    let employeeId: string | null = null

    try {
      const empRes = await http
        .get<any>('/employees', { params: { pageSize: 100 } })
        .then((r) => r.data)
      const empResult = empRes?.result !== undefined ? empRes.result : empRes
      const empList = empResult?.items || empResult?.Items || (Array.isArray(empResult) ? empResult : [])

      const matched = empList.find(
        (e: any) =>
          String(e.id || e.Id) === userId ||
          String(e.person?.user?.id || e.person?.user?.Id || e.userId) === userId ||
          (userDetail?.phoneNumber && e.phoneNumber === userDetail.phoneNumber) ||
          (userDetail?.email && e.email === userDetail.email) ||
          (userDetail?.fullName && e.fullName === userDetail.fullName),
      )

      if (matched?.id || matched?.Id) {
        employeeId = String(matched.id || matched.Id)
        employeeData = await http
          .get<any>(`/employees/${employeeId}`)
          .then((r) => (r.data?.result !== undefined ? r.data.result : r.data))
          .catch(() => matched)
      }
    } catch {
      // ignore
    }

    const actualFullName = input.fullName || employeeData?.fullName || userDetail?.fullName || userDetail?.username || 'Người dùng'
    const actualPhone = input.phoneNumber || employeeData?.phoneNumber || userDetail?.phoneNumber || '0901234567'
    const actualEmail = input.email || employeeData?.email || userDetail?.email || `${(input.username || userDetail?.username || 'user').toLowerCase()}@clinic.vn`
    const actualAddress = employeeData?.address || 'Cơ sở khám chữa bệnh'
    const actualDob = employeeData?.dateOfBirth || (userDetail?.createdAt ? '1995-01-01' : '2000-01-01')
    const actualGender = employeeData?.gender ?? userDetail?.gender ?? 0

    if (employeeId) {
      // Đã có hồ sơ nhân viên -> Cập nhật nhân viên
      const payload = {
        fullName: actualFullName,
        phoneNumber: actualPhone,
        email: actualEmail,
        dateOfBirth: actualDob,
        gender: actualGender,
        address: actualAddress,
        roles: [input.role || 'Receptionist'],
      }
      await http.put<any>(`/employees/${employeeId}`, payload)

      // Cập nhật trạng thái nhân viên qua POST /employees/{employeeId}/status (0: Active, 2: Resigned)
      if (input.isActive !== undefined) {
        const empStatus = input.isActive ? 0 : 2
        await http.post<any>(`/employees/${employeeId}/status`, empStatus).catch(() => null)
      }
      return
    } else {
      // Là tài khoản bệnh nhân -> Tạo hồ sơ nhân viên từ tài khoản hiện có
      const payload = {
        userId: userId,
        email: actualEmail,
        address: actualAddress,
        hireDate: new Date().toISOString().split('T')[0],
        roles: [input.role || 'Receptionist'],
      }
      return http.post<any>('/employees/from-user', payload).then((r) => r.data?.result ?? r.data)
    }
  },

  // Vô hiệu hóa / Ngừng hoạt động tài khoản nhân viên qua POST /employees/{employeeId}/status
  remove: async (userIdOrEmployeeId: string) => {
    let employeeId = userIdOrEmployeeId
    try {
      const empRes = await http
        .get<any>('/employees', { params: { pageSize: 100 } })
        .then((r) => r.data)
      const empResult = empRes?.result !== undefined ? empRes.result : empRes
      const empList = empResult?.items || empResult?.Items || (Array.isArray(empResult) ? empResult : [])

      const matched = empList.find(
        (e: any) =>
          String(e.id || e.Id) === userIdOrEmployeeId ||
          String(e.person?.user?.id || e.person?.user?.Id || e.userId) === userIdOrEmployeeId,
      )

      if (matched?.id || matched?.Id) {
        employeeId = String(matched.id || matched.Id)
      }
    } catch {
      // ignore
    }

    return http
      .post<any>(`/employees/${employeeId}/status`, 2) // 2: Resigned (Ngừng hoạt động)
      .then((r) => r.data)
      .catch(() => null)
  },
}
