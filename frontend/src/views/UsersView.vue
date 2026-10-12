<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useUsersStore } from '@/stores/users'
import BaseButton from '@/components/ui/BaseButton.vue'
import BaseInput from '@/components/ui/BaseInput.vue'
import BaseCard from '@/components/ui/BaseCard.vue'
import BaseSpinner from '@/components/ui/BaseSpinner.vue'
import UserFormDialog from '@/features/users/UserFormDialog.vue'
import type { CreateUserInput, UpdateUserInput, User } from '@/features/users/users.types'

const { t } = useI18n()
const store = useUsersStore()
const { filtered, status, mutating, search } = storeToRefs(store)

const dialogOpen = ref(false)
const editing = ref<User | null>(null)

onMounted(() => {
  if (store.items.length === 0) store.fetchAll()
})

function openCreate() {
  editing.value = null
  dialogOpen.value = true
}

function openEdit(user: User) {
  editing.value = user
  dialogOpen.value = true
}

async function handleSubmit(input: CreateUserInput | UpdateUserInput) {
  if (editing.value) await store.update(editing.value.id, input)
  else await store.create(input as CreateUserInput)
  dialogOpen.value = false
}
</script>

<template>
  <section class="flex flex-col gap-4">
    <div class="flex items-center justify-between gap-4">
      <h1 class="text-2xl font-semibold">{{ t('users.title') }}</h1>
      <BaseButton @click="openCreate">{{ t('users.create') }}</BaseButton>
    </div>

    <BaseInput v-model="search" :placeholder="t('users.search')" class="max-w-sm" />

    <BaseSpinner v-if="status === 'loading'" />

    <div v-else-if="status === 'error'" class="flex flex-col items-center gap-3 p-8">
      <p class="text-red-600">{{ t('common.error') }}</p>
      <BaseButton variant="secondary" @click="store.fetchAll()">
        {{ t('common.retry') }}
      </BaseButton>
    </div>

    <BaseCard v-else>
      <table class="w-full text-left text-sm">
        <thead
          class="border-b border-gray-200 bg-gray-50 text-gray-500 dark:border-gray-800 dark:bg-gray-800/50"
        >
          <tr>
            <th class="px-4 py-3 font-medium">{{ t('users.name') }}</th>
            <th class="px-4 py-3 font-medium">{{ t('users.username') }}</th>
            <th class="px-4 py-3 font-medium">{{ t('users.email') }}</th>
            <th class="px-4 py-3 text-right font-medium">{{ t('users.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="filtered.length === 0">
            <td colspan="4" class="px-4 py-8 text-center text-gray-400">
              {{ t('users.empty') }}
            </td>
          </tr>
          <tr
            v-for="u in filtered"
            :key="u.id"
            class="border-b border-gray-100 last:border-0 dark:border-gray-800"
          >
            <td class="px-4 py-3 font-medium">{{ u.fullName || u.name }}</td>
            <td class="px-4 py-3 text-gray-500">@{{ u.username }}</td>
            <td class="px-4 py-3 text-gray-500">{{ u.email }}</td>
            <td class="px-4 py-3">
              <div class="flex justify-end gap-2">
                <BaseButton size="sm" variant="secondary" @click="openEdit(u)">
                  {{ t('users.edit') }}
                </BaseButton>
                <BaseButton size="sm" variant="danger" @click="store.remove(u.id)">
                  {{ t('users.delete') }}
                </BaseButton>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </BaseCard>

    <UserFormDialog
      :open="dialogOpen"
      :initial="editing"
      :submitting="mutating"
      @close="dialogOpen = false"
      @submit="handleSubmit"
    />
  </section>
</template>
