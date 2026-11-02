<template>
  <!-- The AngularJS mfaModeController: the mode of a user or an organization and its effective status per store -->
  <VcBlade
    :title="$t(isOrganization ? 'mfa.blades.mode.organization-title' : 'mfa.blades.mode.user-title', { name: subjectName })"
    :subtitle="$t('mfa.blades.mode.subtitle')"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    width="50%"
  >
    <VcContainer>
      <VcForm class="tw-space-y-4">
        <VcSelect
          v-model="mode"
          :label="$t('mfa.blades.mode.labels.mode')"
          :placeholder="$t('mfa.blades.mode.placeholders.mode')"
          :options="modeOptions"
          option-value="value"
          option-label="label"
          :disabled="!canManage"
          :clearable="false"
        />
        <VcHint v-if="mode">{{ $t(`mfa.blades.mode.description.${mode}`) }}</VcHint>
      </VcForm>

      <section
        v-if="status"
        class="tw-mt-6 tw-space-y-3"
      >
        <h3 class="tw-text-base tw-font-semibold">{{ $t("mfa.blades.mode.status.title") }}</h3>
        <VcHint v-if="isDirty">{{ $t("mfa.blades.mode.status.preview") }}</VcHint>
        <VcHint>
          {{
            $t("mfa.blades.mode.status.global", {
              value: $t(status.isGloballyEnabled ? "mfa.blades.mode.status.global-on" : "mfa.blades.mode.status.global-off"),
            })
          }}
        </VcHint>

        <VcDataTable
          :items="rows"
          state-key="MFA_MODE_STATUS"
          :empty-state="{ icon: 'lucide-store', title: $t('mfa.blades.mode.status.no-stores') }"
        >
          <VcColumn
            id="store"
            :title="$t('mfa.blades.mode.status.columns.store')"
            mobile-role="title"
            always-visible
          >
            <template #body="{ data: row }">
              <div class="tw-flex tw-flex-col tw-gap-1">
                <span>{{ row.store }}</span>
                <span class="tw-text-xs tw-text-[color:var(--neutrals-500)]">{{ row.storeSetting }}</span>
              </div>
            </template>
          </VcColumn>
          <VcColumn
            id="result"
            :title="$t('mfa.blades.mode.status.columns.result')"
            mobile-role="field"
          >
            <template #body="{ data: row }">
              <div class="tw-flex tw-flex-col tw-gap-1">
                <VcStatus :variant="row.variant">{{ row.result }}</VcStatus>
                <span class="tw-text-xs tw-text-[color:var(--neutrals-500)]">{{ row.reason }}</span>
              </div>
            </template>
          </VcColumn>
        </VcDataTable>
      </section>
    </VcContainer>
  </VcBlade>
</template>

<script setup lang="ts">
import { useBlade, usePermissions, type IBladeToolbar } from "@vc-shell/framework";
import { VcBlade, VcColumn, VcContainer, VcDataTable, VcForm, VcHint, VcSelect, VcStatus } from "@vc-shell/framework/ui";
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { getMode, getStatus, MFA_MODES, setMode, type MfaMode, type MfaStatus, type MfaStoreStatus, type MfaSubjectType } from "../api";

// not routable: opened from the MFA widget of a user or an organization, which passes the subject in options
defineBlade({ url: "/mfa-mode", name: "MfaMode", permissions: ["mfa:read"], routable: false });

const { t } = useI18n({ useScope: "global" });
const { options } = useBlade<{ subjectType: MfaSubjectType; subjectId: string; subjectName?: string }>();
const { hasAccess } = usePermissions();

const isOrganization = computed(() => options.value?.subjectType === "organization");
const subjectType = computed<MfaSubjectType>(() => (isOrganization.value ? "organization" : "user"));
const subjectId = computed(() => options.value?.subjectId ?? "");
const subjectName = computed(() => options.value?.subjectName ?? "");

// mfa:read opens the blade to see the mode, changing it needs mfa:manage
const canManage = computed(() => hasAccess("mfa:manage"));

const loading = ref(false);
const savedMode = ref<MfaMode>();
const mode = ref<MfaMode>();
const status = ref<MfaStatus>();
const isDirty = computed(() => !!mode.value && mode.value !== savedMode.value);

const modeOptions = computed(() => MFA_MODES.map((value) => ({ value, label: t(`mfa.modes.${value}`) })));

async function load() {
  if (!subjectId.value) return;
  loading.value = true;
  try {
    savedMode.value = await getMode(subjectType.value, subjectId.value);
    mode.value = savedMode.value;
  } finally {
    loading.value = false;
  }
}

// The status follows the selected mode, so the administrator sees the effect before saving
let statusRequest = 0;
watch(mode, async (value) => {
  if (!value || !subjectId.value) return;
  statusRequest += 1;
  const request = statusRequest;
  const result = await getStatus(subjectType.value, subjectId.value, value).catch(() => undefined);
  if (request === statusRequest) {
    status.value = result;
  }
});

const isChosenStoreMode = (storeMode?: string) => storeMode === "AdminManaged" || storeMode === "CustomerChoice";

function getRequiredReason(store: MfaStoreStatus) {
  if (store.requiredBy === "Organization") {
    return isOrganization.value ? "OrganizationSelf" : "Organization";
  }

  if (store.requiredBy === "User") {
    return mode.value === "Required" ? "UserRequired" : "UserEnabled";
  }

  return store.requiredBy ?? "Global";
}

function getNotRequiredReason(store: MfaStoreStatus) {
  if (store.storeMode === "Disabled") {
    return "Disabled";
  }

  if (isChosenStoreMode(store.storeMode)) {
    return (isOrganization.value ? "Members" : "") + store.storeMode;
  }

  return "GlobalOff";
}

const storeModeLabel = (storeMode?: string) =>
  t(`mfa.store-modes.${storeMode === "Use global setting" ? "UseGlobalSetting" : storeMode}`);

function toRow(store: MfaStoreStatus) {
  // In these stores an organization that requires nothing leaves the choice to each member
  const perMember = isOrganization.value && !store.isRequired && isChosenStoreMode(store.storeMode);
  let result = "not-required";
  if (perMember) {
    result = "per-member";
  } else if (store.isRequired) {
    result = "required";
  }

  return {
    id: store.storeId,
    store: store.storeName || store.storeId,
    storeSetting: storeModeLabel(store.storeMode),
    result: t(`mfa.blades.mode.status.results.${result}`),
    reason: t(`mfa.blades.mode.status.reasons.${store.isRequired ? getRequiredReason(store) : getNotRequiredReason(store)}`),
    variant: perMember ? "warning" : store.isRequired ? "success" : "info",
  };
}

const rows = computed(() => {
  const value = status.value;
  if (!value) return [];

  const stores = (value.stores ?? []).map(toRow);
  if (value.isRequiredForBackoffice === null || value.isRequiredForBackoffice === undefined) {
    return stores;
  }

  // the Backoffice sign-in follows only the global setting
  const required = value.isRequiredForBackoffice;
  return [
    {
      id: "backoffice",
      store: t("mfa.blades.mode.status.backoffice"),
      storeSetting: t("mfa.blades.mode.status.global-setting"),
      result: t(`mfa.blades.mode.status.results.${required ? "required" : "not-required"}`),
      reason: t(`mfa.blades.mode.status.reasons.${required ? "Global" : "GlobalOff"}`),
      variant: required ? "success" : "info",
    },
    ...stores,
  ];
});

const bladeToolbar = ref<IBladeToolbar[]>([
  {
    id: "save",
    title: computed(() => t("mfa.blades.mode.commands.save")),
    icon: "lucide-save",
    isVisible: canManage,
    disabled: computed(() => !isDirty.value),
    async clickHandler() {
      loading.value = true;
      try {
        savedMode.value = await setMode(subjectType.value, subjectId.value, mode.value!);
        mode.value = savedMode.value;
      } finally {
        loading.value = false;
      }
    },
  },
  {
    id: "reset",
    title: computed(() => t("mfa.blades.mode.commands.reset")),
    icon: "lucide-undo-2",
    isVisible: canManage,
    disabled: computed(() => !isDirty.value),
    clickHandler() {
      mode.value = savedMode.value;
    },
  },
]);

load();
</script>
