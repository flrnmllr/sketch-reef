<script setup lang="ts">
import { ref } from 'vue'
import axios from 'axios'
import {
  ArrowPathIcon as ArrowPathIconOutline,
  CameraIcon as CameraIconOutline
} from '@heroicons/vue/24/outline'

const loading = ref(false)
const progress = ref(0)
const errorMessage = ref('')

const fileInput = ref<HTMLInputElement | null>(null)

function openCamera(): void {
  fileInput.value?.click()
}

async function handleFile(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]

  if (!file) return

  const formData = new FormData()
  formData.append('file', file)

  loading.value = true
  progress.value = 0

  try {
    await axios.post('/api', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      },
      onUploadProgress: (progressEvent) => {
        if (!progressEvent.total) return
        progress.value = Math.round(
            (progressEvent.loaded * 100) / progressEvent.total
        )
      }
    })
    errorMessage.value = 'Upload erfolgreich!'
    setTimeout(() => {
      errorMessage.value = ''
    }, 5000)
  } catch (error: any) {
    errorMessage.value = error.response?.data.error
  } finally {
    loading.value = false
    input.value = ''
  }
}
</script>

<template>
  <div @click="openCamera"
       class="rounded-full bg-linear-to-br from-pink-500 to-purple-500 w-[50vmin] aspect-square grid place-items-center"
       :class="{ 'pointer-events-none': loading }"
  >
    <CameraIconOutline v-if="!loading" class="w-1/2 text-white" />
    <ArrowPathIconOutline v-else class="w-1/2 text-white animate-spin" />
  </div>
  <div v-if="errorMessage" class="absolute inset-x-0 bottom-[10vh] text-center">
    {{ errorMessage }}
  </div>
  <div v-if="loading" class="absolute inset-x-0 bottom-0">
    <div class="h-2 overflow-hidden">
      <div
          class="h-full bg-linear-to-br from-pink-500 to-purple-500 transition-all duration-150" :style="{ width: progress + '%' }"
      ></div>
    </div>
  </div>
  <input
      ref="fileInput"
      type="file"
      accept="image/*"
      capture="environment"
      class="hidden"
      @change="handleFile"
  />
</template>

<style scoped></style>
