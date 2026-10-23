import { globalIgnores } from 'eslint/config'
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'
import pluginVue from 'eslint-plugin-vue'
import pluginVitest from '@vitest/eslint-plugin'
import pluginOxlint from 'eslint-plugin-oxlint'
import skipFormatting from 'eslint-config-prettier/flat'

// To allow more languages other than `ts` in `.vue` files, uncomment the following lines:
// import { configureVueProject } from '@vue/eslint-config-typescript'
// configureVueProject({ scriptLangs: ['ts', 'tsx'] })
// More info at https://github.com/vuejs/eslint-config-typescript/#advanced-setup

export default defineConfigWithVueTs(
  {
    name: 'app/files-to-lint',
    files: ['**/*.{vue,ts,mts,tsx}'],
  },

  globalIgnores(['**/dist/**', '**/dist-ssr/**', '**/coverage/**']),

  ...pluginVue.configs['flat/essential'],
  vueTsConfigs.recommended,

  {
    ...pluginVitest.configs.recommended,
    files: ['src/**/__tests__/*'],
  },

  // #26 : aucun texte en dur dans les gabarits, tout passe par t() (ADR 0033)
  {
    name: 'app/no-bare-strings',
    files: ['src/**/*.vue'],
    rules: {
      'vue/no-bare-strings-in-template': ['error', {
        allowlist: [
          'SongVault', 'E-mail', 'BPM', 'ISWC', 'ISRC', 'CSV', 'ZIP', 'cnil.fr',
          '·', '—', '–', '−', '+', '×', '/', '(', ')', ':', '.', ',', '…', '↑', '↓', '←', '→', '▶', '⏸', '✕', '♪', '⠿', '%', '#', '*', '|', '🎸', '♩', '=', '▴', '▾',
          '/\\bv$/',                     // « v3 » : le numéro de version
          '/^[MS]$/',                     // boutons Muet et Solo du mélangeur
          '/^[\\dA-Z.-]+$/',              // exemples de codes : T-000.000.000-0, FR-XXX-26-00001
          '/songvault\\w+/',              // songvault.auth… (le point est déjà retiré par la liste)
        ],
        attributes: { '/.+/': ['title', 'aria-label', 'aria-placeholder', 'aria-roledescription', 'aria-valuetext', 'placeholder', 'alt', 'label', 'message', 'submit-label'] },
        directives: ['v-text'],
      }],
    },
  },

  ...pluginOxlint.buildFromOxlintConfigFile('.oxlintrc.json'),

  skipFormatting,
)
