import { defineConfig } from 'orval'

export default defineConfig({
  backend: {
    input: '../openapi.json',
    output: {
      target: './src/api/client.ts',
      client: 'fetch',
      override: {
        mutator: { path: './src/api/fetcher.ts', name: 'customFetch' },
      },
    },
  },
})
