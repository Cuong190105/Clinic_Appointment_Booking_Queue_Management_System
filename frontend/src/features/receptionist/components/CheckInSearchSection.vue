<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { receptionistApi } from '../receptionist.api'
import type { PatientSearchItem } from '../receptionist.types'

const props = defineProps<{
  selectedPatientId?: string | null
}>()

const emit = defineEmits<{
  (e: 'select-patient', patient: PatientSearchItem): void
  (e: 'clear'): void
}>()

const search = ref('')
const results = ref<PatientSearchItem[]>([])
const defaultPatients = ref<PatientSearchItem[]>([])
const loading = ref(false)
const loadingDefaults = ref(false)
const hasSearched = ref(false)

let debounceTimer: ReturnType<typeof setTimeout> | null = null

async function loadDefaultPatients() {
  loadingDefaults.value = true
  try {
    const res = await receptionistApi.searchPatients('', 1, 15)
    defaultPatients.value = res.items || []
  } catch {
    defaultPatients.value = []
  } finally {
    loadingDefaults.value = false
  }
}

function onInput() {
  if (debounceTimer) clearTimeout(debounceTimer)
  const query = search.value.trim()
  if (query.length === 0) {
    results.value = []
    hasSearched.value = false
    return
  }

  debounceTimer = setTimeout(async () => {
    loading.value = true
    hasSearched.value = true
    try {
      const res = await receptionistApi.searchPatients(query, 1, 15)
      results.value = res.items || []
    } catch {
      results.value = []
    } finally {
      loading.value = false
    }
  }, 300)
}

function selectPatient(patient: PatientSearchItem) {
  emit('select-patient', patient)
}

function clearSearch() {
  search.value = ''
  results.value = []
  hasSearched.value = false
  emit('clear')
}

onMounted(() => {
  void loadDefaultPatients()
})
</script>

<template>
  <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-2xs space-y-4">
    <!-- TITLE -->
    <div class="flex items-center justify-between">
      <div>
        <h2 class="text-sm font-bold uppercase tracking-wider text-slate-700">
          1. Danh sách & Tra cứu bệnh nhân
        </h2>
        <p class="text-xs text-slate-500 mt-0.5">
          Chọn nhanh từ danh sách mặc định hoặc nhập từ khóa để tìm kiếm
        </p>
      </div>

      <button
        type="button"
        class="text-xs text-slate-400 hover:text-slate-600 cursor-pointer flex items-center gap-1"
        title="Tải lại danh sách"
        @click="loadDefaultPatients"
      >
        <span>🔄</span>
      </button>
    </div>

    <!-- SEARCH INPUT -->
    <div class="relative">
      <input
        v-model="search"
        type="text"
        placeholder="Tìm theo tên, SĐT (vd: 0912...), mã BHYT..."
        class="w-full rounded-xl border border-slate-200 bg-slate-50/50 px-3.5 py-2.5 pl-10 pr-10 text-sm text-slate-800 placeholder:text-slate-400 focus:border-[#0E4D92] focus:bg-white focus:outline-none transition-colors"
        @input="onInput"
      />
      <svg
        class="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
      >
        <circle cx="11" cy="11" r="7" />
        <path d="m20 20-3.5-3.5" />
      </svg>
      <button
        v-if="search"
        type="button"
        class="absolute right-3 top-1/2 -translate-y-1/2 text-xs font-bold text-slate-400 hover:text-slate-600"
        @click="clearSearch"
      >
        ✕
      </button>
    </div>

    <!-- LIST HEADER LABEL -->
    <div class="flex items-center justify-between text-xs font-semibold text-slate-500 pt-1">
      <span v-if="hasSearched">
        Kết quả tìm kiếm ({{ results.length }})
      </span>
      <span v-else>
        Bệnh nhân trong hệ thống ({{ defaultPatients.length }})
      </span>
      <span v-if="loading || loadingDefaults" class="text-violet-600 flex items-center gap-1">
        <span class="animate-spin">🌀</span> Đang tải...
      </span>
    </div>

    <!-- SEARCH RESULTS OR DEFAULT LIST -->
    <div
      v-if="hasSearched ? results.length > 0 : defaultPatients.length > 0"
      class="max-h-80 overflow-y-auto divide-y divide-slate-100 rounded-xl border border-slate-200 bg-slate-50/30"
    >
      <div
        v-for="p in (hasSearched ? results : defaultPatients)"
        :key="p.id"
        class="p-3.5 hover:bg-blue-50/60 cursor-pointer transition-colors flex items-center justify-between group"
        :class="selectedPatientId === p.id ? 'bg-blue-50/80 border-l-4 border-[#0E4D92]' : ''"
        @click="selectPatient(p)"
      >
        <div class="space-y-0.5">
          <div class="font-bold text-slate-800 text-sm group-hover:text-[#0E4D92] transition-colors">
            {{ p.fullName }}
          </div>
          <div class="text-xs text-slate-500 flex flex-wrap gap-x-3">
            <span v-if="p.phoneNumber">📞 {{ p.phoneNumber }}</span>
            <span v-if="p.insuranceNumber">🏥 BHYT: {{ p.insuranceNumber }}</span>
          </div>
        </div>

        <button
          type="button"
          class="rounded-lg border px-2.5 py-1 text-xs font-semibold transition-all cursor-pointer"
          :class="
            selectedPatientId === p.id
              ? 'bg-[#0E4D92] text-white border-[#0E4D92]'
              : 'bg-white border-slate-200 text-slate-600 group-hover:border-[#0E4D92] group-hover:text-[#0E4D92]'
          "
        >
          {{ selectedPatientId === p.id ? 'Đang chọn' : 'Chọn' }}
        </button>
      </div>
    </div>

    <!-- EMPTY STATE WHEN SEARCH FINDS NOTHING -->
    <div
      v-else-if="hasSearched && !loading"
      class="rounded-xl border border-dashed border-slate-200 p-6 text-center text-xs text-slate-400"
    >
      <div class="text-xl mb-1">🔍</div>
      <div>Không tìm thấy bệnh nhân nào khớp với "{{ search }}"</div>
    </div>

    <!-- EMPTY STATE WHEN NO DEFAULT PATIENTS EXIST -->
    <div
      v-else-if="!loadingDefaults && defaultPatients.length === 0"
      class="rounded-xl border border-dashed border-slate-200 p-6 text-center text-xs text-slate-400"
    >
      <div class="text-xl mb-1">👥</div>
      <div>Chưa có hồ sơ bệnh nhân nào trong hệ thống</div>
    </div>
  </div>
</template>
