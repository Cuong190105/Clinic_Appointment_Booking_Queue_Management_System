<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ApiError } from '@/lib/api/http'
import { doctorsApi } from '@/features/doctors/doctors.api'
import type { DoctorDetail, Gender } from '@/features/doctors/doctors.types'
const doctor = ref<DoctorDetail | null>(null); const loading = ref(false); const saving = ref(false); const error = ref(''); const notice = ref('')
const form = reactive({ fullName: '', phoneNumber: '', email: '', dateOfBirth: '', gender: 'Male' as Gender, address: '', licenseNumber: '', qualification: '', experienceYears: 0, biography: '' })
function msg(e: unknown) { return e instanceof ApiError ? e.message : e instanceof Error ? e.message : 'Có lỗi xảy ra.' }
function fill(d: DoctorDetail) { Object.assign(form, { fullName: d.fullName, phoneNumber: d.phoneNumber, email: d.email, dateOfBirth: d.dateOfBirth?.slice(0,10) ?? '', gender: d.gender, address: d.address ?? '', licenseNumber: d.licenseNumber, qualification: d.qualification, experienceYears: d.experienceYears, biography: d.biography ?? '' }) }
async function load() { loading.value = true; error.value = ''; try { doctor.value = await doctorsApi.getOwnProfile(); fill(doctor.value) } catch (e) { error.value = msg(e) } finally { loading.value = false } }
async function save() { saving.value = true; error.value = ''; notice.value = ''; try { await doctorsApi.updateOwnProfile({ ...form, experienceYears: Number(form.experienceYears) }); await load(); notice.value = 'Đã cập nhật hồ sơ bác sĩ.' } catch (e) { error.value = msg(e) } finally { saving.value = false } }
onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <!-- Header -->
    <div>
      <p class="text-sm font-semibold text-violet-600">
        Bác sĩ
      </p>

      <h1 class="text-2xl font-bold text-slate-900">
        Hồ sơ cá nhân
      </h1>

      <p class="mt-1 text-sm text-slate-600">
        Thông tin được lấy trực tiếp từ API /doctors/me.
      </p>
    </div>

    <!-- Error -->
    <div
      v-if="error"
      class="rounded-xl border border-red-200 bg-red-50 p-4 text-sm font-medium text-red-700"
    >
      {{ error }}
    </div>

    <!-- Success -->
    <div
      v-if="notice"
      class="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm font-medium text-emerald-700"
    >
      {{ notice }}
    </div>

    <!-- Form -->
    <form
      class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
      @submit.prevent="save"
    >
      <div class="grid gap-5 md:grid-cols-2">

        <label class="text-sm font-semibold text-slate-800">
          Họ tên

          <input
            v-model="form.fullName"
            required
            class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none placeholder:text-slate-400 focus:border-violet-500"
          >
        </label>

        <label class="text-sm font-semibold text-slate-800">
          Số điện thoại

          <input
            v-model="form.phoneNumber"
            required
            class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none focus:border-violet-500"
          >
        </label>

        <label class="text-sm font-semibold text-slate-800">
          Email

          <input
            v-model="form.email"
            type="email"
            required
            class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none focus:border-violet-500"
          >
        </label>

        <label class="text-sm font-semibold text-slate-800">
          Ngày sinh

          <input
            v-model="form.dateOfBirth"
            type="date"
            class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none focus:border-violet-500"
          >
        </label>

        <label class="text-sm font-semibold text-slate-800">
          Giới tính

          <select
            v-model="form.gender"
            class="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-900 outline-none focus:border-violet-500"
          >
            <option value="Male">Nam</option>
            <option value="Female">Nữ</option>
            <option value="Other">Khác</option>
          </select>
        </label>

        <label class="text-sm font-semibold text-slate-800">
          Địa chỉ

          <input
            v-model="form.address"
            class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none focus:border-violet-500"
          >
        </label>

        <label class="text-sm font-semibold text-slate-800">
          Số giấy phép

          <input
            v-model="form.licenseNumber"
            required
            class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none focus:border-violet-500"
          >
        </label>

        <label class="text-sm font-semibold text-slate-800">
          Trình độ

          <input
            v-model="form.qualification"
            required
            class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none focus:border-violet-500"
          >
        </label>

        <label class="text-sm font-semibold text-slate-800">
          Số năm kinh nghiệm

          <input
            v-model.number="form.experienceYears"
            type="number"
            min="0"
            class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none focus:border-violet-500"
          >
        </label>

        <div>
          <p class="text-sm font-semibold text-slate-800">
            Chuyên khoa
          </p>

          <p
            class="mt-1 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-medium text-slate-700"
          >
            {{ doctor?.currentSpecialty || '—' }}
          </p>
        </div>

        <div class="md:col-span-2">
          <label class="text-sm font-semibold text-slate-800">
            Tiểu sử

            <textarea
              v-model="form.biography"
              rows="5"
              class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none placeholder:text-slate-400 focus:border-violet-500"
            />
          </label>
        </div>

      </div>

      <div class="mt-6 flex justify-end">
        <button
          class="rounded-lg bg-violet-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-violet-700 disabled:cursor-not-allowed disabled:opacity-50"
          :disabled="saving || loading"
        >
          {{ saving ? 'Đang lưu...' : 'Lưu hồ sơ' }}
        </button>
      </div>
    </form>
  </div>
</template>
