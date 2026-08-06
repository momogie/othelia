<script setup lang="ts">
import type {
  IngestionFilterRuleVM,
  IngestionSettingsVM,
  QuerySettingsVM,
  RetentionSettingsVM,
  StorageSettingsVM,
  TracingSettingsPatch,
} from '~/utils/settingsTypes'
import { FILTER_ACTIONS, FILTER_FIELDS, FILTER_OPERATORS } from '~/utils/settingsTypes'

const { settings, loading, saving, error, success, load, save, reset } = useSettings()

const form = reactive<{
  ingestion: IngestionSettingsVM
  storage: StorageSettingsVM
  query: QuerySettingsVM
  retention: RetentionSettingsVM
}>({
  ingestion: { enabled: true, endpoint: 'http://localhost:4318', rules: [] },
  storage: { provider: 'SqlServer', connectionString: null },
  query: {
    maxTracesPerRequest: 100,
    defaultLookbackSeconds: 3600,
    slowThresholdMs: 1000,
    dashboardWindowSeconds: 86400,
    dashboardBuckets: 24,
  },
  retention: { enabled: false, retentionDays: 7, cleanupIntervalHours: 24 },
})

const dirty = ref(false)
const showToast = ref<'success' | 'error' | null>(null)
const toastMsg = ref('')
let toastTimer: ReturnType<typeof setTimeout> | null = null

watch(
  settings,
  (s) => {
    if (!s) return
    form.ingestion = {
      enabled: s.ingestion?.enabled ?? true,
      endpoint: s.ingestion?.endpoint ?? 'http://localhost:4318',
      rules: (s.ingestion?.rules ?? []).map((r) => ({ ...r })),
    }
    form.storage = { ...s.storage }
    form.query = { ...s.query }
    form.retention = { ...s.retention }
    dirty.value = false
  },
  { immediate: true },
)

watch(form, () => {
  if (settings.value) dirty.value = true
}, { deep: true })

function flash(type: 'success' | 'error', msg: string) {
  showToast.value = type
  toastMsg.value = msg
  if (toastTimer) clearTimeout(toastTimer)
  toastTimer = setTimeout(() => {
    showToast.value = null
    toastTimer = null
  }, 3500)
}

function addRule() {
  form.ingestion.rules.push({
    enabled: true,
    field: 'ServiceName',
    operator: 'Equals',
    value: '',
    action: 'Drop',
  })
  dirty.value = true
}

function removeRule(index: number) {
  form.ingestion.rules.splice(index, 1)
  dirty.value = true
}

function buildPatch(): TracingSettingsPatch {
  return {
    ingestionEnabled: form.ingestion.enabled,
    ingestionEndpoint: form.ingestion.endpoint || undefined,
    ingestionRules: form.ingestion.rules,
    queryMaxTracesPerRequest: form.query.maxTracesPerRequest,
    queryDefaultLookbackSeconds: form.query.defaultLookbackSeconds,
    querySlowThresholdMs: form.query.slowThresholdMs,
    retentionEnabled: form.retention.enabled,
    retentionDays: form.retention.retentionDays,
    retentionCleanupIntervalHours: form.retention.cleanupIntervalHours,
  }
}

async function onSave() {
  await save(buildPatch())
  if (error.value) flash('error', error.value)
  else flash('success', 'Settings saved')
}

async function onReset() {
  await reset()
  if (error.value) flash('error', error.value)
  else flash('success', 'Settings reset to defaults')
}

onMounted(() => {
  if (!settings.value) load()
})
</script>

<template>
  <div class="settings-page">
    <div v-if="loading" class="empty-state">
      <div class="empty-icon">⏳</div>
      <div class="empty-text">Loading settings...</div>
    </div>

    <template v-else>
      <div class="settings-header">
        <div>
          <div class="settings-title">Tracing Settings</div>
          <div class="settings-sub">
            Hot-reloadable options. Saved to the database and applied within a few seconds.
          </div>
        </div>
        <div class="settings-actions">
          <button
            class="hdr-btn"
            :disabled="saving"
            @click="onReset"
          >
            Reset to defaults
          </button>
          <button
            class="hdr-btn primary"
            :disabled="saving || !dirty"
            @click="onSave"
          >
            {{ saving ? 'Saving...' : 'Save changes' }}
          </button>
        </div>
      </div>

      <div v-if="error" class="settings-banner banner-error">
        {{ error }} — the API is unreachable or returned an error.
      </div>

      <div class="settings-grid">
        <section class="settings-card">
          <div class="sc-header">
            <span class="sc-title">Ingestion</span>
            <span class="sc-desc">OTLP receiver on :4318</span>
          </div>
          <div class="sc-body">
            <label class="field-toggle">
              <input v-model="form.ingestion.enabled" type="checkbox" @change="dirty = true">
              <span>Enabled</span>
            </label>
            <div class="field">
              <label class="field-label">Receiver endpoint</label>
              <input
                v-model="form.ingestion.endpoint"
                class="text-input"
                type="text"
                placeholder="http://localhost:4318"
                @input="dirty = true"
              >
            </div>
          </div>
        </section>

        <section class="settings-card">
          <div class="sc-header">
            <span class="sc-title">Storage</span>
            <span class="sc-desc">Read-only</span>
          </div>
          <div class="sc-body">
            <div class="field">
              <label class="field-label">Provider</label>
              <input
                :value="form.storage.provider"
                class="text-input"
                type="text"
                readonly
              >
            </div>
            <div class="field">
              <label class="field-label">Connection string</label>
              <input
                :value="form.storage.connectionString ?? ''"
                class="text-input"
                type="password"
                readonly
              >
            </div>
          </div>
        </section>

        <section class="settings-card">
          <div class="sc-header">
            <span class="sc-title">Query</span>
            <span class="sc-desc">/api/traces defaults</span>
          </div>
          <div class="sc-body">
            <div class="field">
              <label class="field-label">Max traces per request</label>
              <input
                v-model.number="form.query.maxTracesPerRequest"
                class="text-input"
                type="number"
                min="1"
                @input="dirty = true"
              >
            </div>
            <div class="field">
              <label class="field-label">Default lookback (seconds)</label>
              <input
                v-model.number="form.query.defaultLookbackSeconds"
                class="text-input"
                type="number"
                min="60"
                @input="dirty = true"
              >
            </div>
            <div class="field">
              <label class="field-label">Slow threshold (ms)</label>
              <input
                v-model.number="form.query.slowThresholdMs"
                class="text-input"
                type="number"
                min="1"
                @input="dirty = true"
              >
            </div>
            <div class="field-row">
              <div class="field">
                <label class="field-label">Dashboard window (s)</label>
                <input
                  :value="form.query.dashboardWindowSeconds"
                  class="text-input"
                  type="number"
                  readonly
                >
              </div>
              <div class="field">
                <label class="field-label">Dashboard buckets</label>
                <input
                  :value="form.query.dashboardBuckets"
                  class="text-input"
                  type="number"
                  readonly
                >
              </div>
            </div>
          </div>
        </section>

        <section class="settings-card">
          <div class="sc-header">
            <span class="sc-title">Retention</span>
            <span class="sc-desc">Old span cleanup</span>
          </div>
          <div class="sc-body">
            <label class="field-toggle">
              <input v-model="form.retention.enabled" type="checkbox" @change="dirty = true">
              <span>Enabled</span>
            </label>
            <div class="field-row">
              <div class="field">
                <label class="field-label">Retention (days)</label>
                <input
                  v-model.number="form.retention.retentionDays"
                  class="text-input"
                  type="number"
                  min="1"
                  @input="dirty = true"
                >
              </div>
              <div class="field">
                <label class="field-label">Cleanup interval (hours)</label>
                <input
                  v-model.number="form.retention.cleanupIntervalHours"
                  class="text-input"
                  type="number"
                  min="1"
                  @input="dirty = true"
                >
              </div>
            </div>
          </div>
        </section>
      </div>

      <section class="settings-card rules-card">
        <div class="sc-header">
          <span class="sc-title">Ingestion Filter Rules</span>
          <span class="sc-desc">First matching rule wins</span>
          <button class="hdr-btn" style="margin-left:auto" @click="addRule">
            + Add rule
          </button>
        </div>
        <div class="sc-body">
          <div v-if="!form.ingestion.rules.length" class="rules-empty">
            No rules — all spans are ingested. Add a rule to drop or keep matching spans.
          </div>
          <div v-else class="rules-table">
            <div class="rule-row rule-head">
              <span>Enabled</span>
              <span>Field</span>
              <span>Operator</span>
              <span>Value</span>
              <span>Action</span>
              <span />
            </div>
            <div v-for="(rule, i) in form.ingestion.rules" :key="i" class="rule-row">
              <input
                v-model="rule.enabled"
                type="checkbox"
                @change="dirty = true"
              >
              <select v-model="rule.field" @change="dirty = true">
                <option v-for="f in FILTER_FIELDS" :key="f" :value="f">{{ f }}</option>
              </select>
              <select v-model="rule.operator" @change="dirty = true">
                <option v-for="op in FILTER_OPERATORS" :key="op" :value="op">{{ op }}</option>
              </select>
              <input
                v-model="rule.value"
                class="text-input"
                type="text"
                placeholder="e.g. healthcheck"
                @input="dirty = true"
              >
              <select v-model="rule.action" @change="dirty = true">
                <option v-for="a in FILTER_ACTIONS" :key="a" :value="a">{{ a }}</option>
              </select>
              <button class="rule-del" title="Remove rule" @click="removeRule(i)">✕</button>
            </div>
          </div>
        </div>
      </section>
    </template>

    <div v-if="showToast" class="toast" :class="showToast">
      {{ toastMsg }}
    </div>
  </div>
</template>
