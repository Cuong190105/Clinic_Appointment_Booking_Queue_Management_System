export interface ValidationResult {
  isValid: boolean
  message: string
}

export const validators = {
  required(value: string, fieldName = 'Trường này'): ValidationResult {
    const isValid = value !== null && value !== undefined && value.trim() !== ''
    return {
      isValid,
      message: isValid ? '' : `${fieldName} không được để trống`,
    }
  },

  email(value: string): ValidationResult {
    if (!value)
      return { isValid: true, message: '' }
    const emailRegex = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/
    const isValid = emailRegex.test(value.trim())
    return {
      isValid,
      message: isValid ? '' : 'Email không đúng định dạng (VD: example@clinic.com)',
    }
  },

  phone(value: string): ValidationResult {
    if (!value) return { isValid: true, message: '' }
    const phoneRegex = /^(03|05|07|08|09)[0-9]{8}$/
    const isValid = phoneRegex.test(value.trim())
    return {
      isValid,
      message: isValid ? '' : 'Số điện thoại không hợp lệ (VD: 0912345678)',
    }
  },

  password(value: string, minLength = 6): ValidationResult {
    const isValid = value.length >= minLength
    return {
      isValid,
      message: isValid ? '' : `Mật khẩu phải chứa ít nhất ${minLength} ký tự`,
    }
  },

  confirmPassword(password: string, confirm: string): ValidationResult {
    const isValid = password === confirm
    return {
      isValid,
      message: isValid ? '' : 'Mật khẩu xác nhận không khớp',
    }
  },
}