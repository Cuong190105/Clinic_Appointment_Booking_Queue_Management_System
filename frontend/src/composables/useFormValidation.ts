import { watch } from 'vue';
import { reactive, ref, type Ref } from 'vue'

export type ValidationResult = { isValid: boolean; message: string }
export type ValidatorFn = (value: any, formData?: any) => ValidationResult
export type ValidationRules<T> = Partial<Record<keyof T, ValidatorFn[]>>

export function useFormValidation<T extends Record<string, any>>(
  initialValues: T,
  rules: ValidationRules<T> = {},
  optional: { externalError?: Ref<string | null> } = {}
) {
  const formData = reactive<T>({ ...initialValues }) as T
  const errors = reactive<Record<string, string>>({})
  const touched = reactive<Record<string, boolean>>({})
  const isSubmitting = ref(false)

  function touch(field: keyof T) {
    touched[field as string] = true
  }

  function setError(field: keyof T, message: string) {
    errors[field as string] = message
  }

  function clearError(field: keyof T) {
    errors[field as string] = ''
  }

  function validateField(field: keyof T): boolean {
    const fieldRules = rules[field]
    if (!fieldRules || fieldRules.length === 0) return true

    let isValid = true
    let message = ''

    for (const rule of fieldRules) {
      const res = rule(formData[field], formData)
      if (!res.isValid) {
        isValid = false
        message = res.message
        break 
      }
    }

    errors[field as string] = message
    return isValid
  }

  function validateAll(): boolean {
    let isAllValid = true
    for (const field in rules) {
      touch(field as keyof T)
      if (!validateField(field as keyof T)) {
        isAllValid = false
      }
    }
    return isAllValid
  }

  function handleBlur(field: keyof T) {
    touch(field)
    validateField(field)
  }
  
  watch(
    formData,
    () => {
      // Xóa lỗi Backend nếu có
      if (optional.externalError && optional.externalError.value) {
        optional.externalError.value = null
      }

      // Re-validate các ô đã bị blur/touched trước đó
      for (const field in rules) {
        if (touched[field]) {
          validateField(field as keyof T)
        }
      }
    },
    { deep: true }
  )
  
  return {
    formData,
    errors,
    touched,
    isSubmitting,
    handleBlur,
    validateField,
    validateAll,
    setError,
    clearError,
  }
}