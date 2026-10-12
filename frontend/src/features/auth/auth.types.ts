// phân quyền người dùng
export type UserRole = 'Patient' | 'Receptionist' | 'Doctor' | 'Admin'

export interface AuthUser {
  // Support numeric IDs and string IDs (UUID) from different backends
  id: number | string
  name: string
  email: string
  role?: UserRole
  roles?: UserRole[]
  activeRole?: UserRole
}

export interface LoginPayload {
  username?: string
  email?: string
  password: string
  role?: UserRole
  // If true, persist tokens in localStorage; otherwise use sessionStorage for current session only
  rememberMe?: boolean
}

export interface LoginResponse {
  accessToken: string
  refreshToken?: string
  role?: UserRole
  roles?: UserRole[]
  user: AuthUser
}

export interface RegisterPayload {
  username?: string
  fullName: string
  phoneNumber: string
  email: string
  password: string
  gender: 'Male' | 'Female' | 'Other'
  dateOfBirth: string
  address?: string
  // Whether to persist tokens after registration (true = localStorage, false = sessionStorage)
  rememberMe?: boolean
}

export interface User {
  id: string
  email: string
  fullName: string
  phoneNumber?: string
  role?: UserRole
  roles?: UserRole[]
  activeRole?: UserRole
}

export interface LoginRequest {
  email: string
  password: string
  role?: UserRole
}

export interface RegisterRequest {
  fullName: string
  email: string
  password: string
  phoneNumber: string
}

export interface AuthResponse {
  token: string
  user: User
}