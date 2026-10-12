export const SPECIALTY_TRANSLATIONS: Record<string, string> = {
  Cardiology: 'Tim mạch',
  Dermatology: 'Da liễu',
  Pediatrics: 'Nhi khoa',
  Orthopedics: 'Xương khớp',
  'General Practice': 'Nội tổng quát',
  'Internal Medicine': 'Nội tổng quát',
  Otolaryngology: 'Tai mũi họng',
  ENT: 'Tai mũi họng',
  Ophthalmology: 'Mắt',
  Neurology: 'Thần kinh',
}

/**
 * Format specialty name with Vietnamese label and English original name (e.g., "Tim mạch (Cardiology)")
 */
export function formatSpecialtyName(specialtyName?: string | null): string {
  if (!specialtyName) return 'Đa khoa'
  const trimmed = specialtyName.trim()
  if (SPECIALTY_TRANSLATIONS[trimmed]) {
    return `${SPECIALTY_TRANSLATIONS[trimmed]} (${trimmed})`
  }
  return trimmed
}

/**
 * Get Vietnamese display label for a specialty (e.g., "Tim mạch")
 */
export function getSpecialtyDisplay(specialtyName?: string | null): string {
  if (!specialtyName) return 'Đa khoa'
  const trimmed = specialtyName.trim()
  return SPECIALTY_TRANSLATIONS[trimmed] || trimmed
}

/**
 * Helper to match doctor specialty against a query or selected specialty flexibly
 */
export function matchesSpecialty(doctorSpecialty?: string | null, targetSpecialty?: string | null): boolean {
  if (!targetSpecialty || targetSpecialty === '' || targetSpecialty === 'all') return true
  if (!doctorSpecialty) return false

  const docNorm = doctorSpecialty.trim().toLowerCase()
  const targetNorm = targetSpecialty.trim().toLowerCase()

  if (docNorm === targetNorm) return true

  const docTrans = SPECIALTY_TRANSLATIONS[doctorSpecialty.trim()]?.toLowerCase()
  const targetTrans = SPECIALTY_TRANSLATIONS[targetSpecialty.trim()]?.toLowerCase()

  if (docTrans && (docTrans === targetNorm || targetNorm.includes(docTrans) || docTrans.includes(targetNorm))) {
    return true
  }

  if (targetTrans && (targetTrans === docNorm || docNorm.includes(targetTrans) || targetTrans.includes(docNorm))) {
    return true
  }

  return docNorm.includes(targetNorm) || targetNorm.includes(docNorm)
}
