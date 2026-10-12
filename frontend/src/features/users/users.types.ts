export type AccountRole =
  | 'Admin'
  | 'Doctor'
  | 'Receptionist'
  | 'Patient'
  | 'admin'
  | 'doctor'
  | 'receptionist'
  | 'patient'

export interface User {
  id: string
  username: string
  fullName?: string
  name?: string
  phoneNumber?: string | null
  email?: string | null
  gender?: number
  roles: string[]
  isActive?: boolean
  createdAt?: string
}

export interface CreateUserInput {
  name?: string
  fullName?: string
  username: string
  email?: string
  phoneNumber?: string
  password?: string
  role?: string
}

export interface UpdateUserInput {
  name?: string
  fullName?: string
  username?: string
  email?: string
  phoneNumber?: string
  isActive?: boolean
  role?: string
}

export interface PagedUsersResponse {
  items: User[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

export interface GetUsersParams {
  search?: string
  role?: string
  sortBy?: string
  orderBy?: string
  gender?: number
  pageNumber?: number
  pageSize?: number
}
