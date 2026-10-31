<template>
  <!-- the mode is text, not a count, so the icon shows whether MFA is on and the title names the mode -->
  <VcWidget
    :icon="mode && mode !== 'Off' ? 'lucide-shield-check' : 'lucide-shield'"
    :title="mode ? `${$t('mfa.widgets.mode.short-name')}: ${$t(`mfa.modes.${mode}`)}` : $t('mfa.widgets.mode.title')"
    :disabled="!subject?.id"
    @click="open"
  />
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { injectBladeContext, useBlade } from "@vc-shell/framework";
import { VcWidget } from "@vc-shell/framework/ui";
import { getMode, modeVersion, type MfaMode, type MfaSubjectType } from "../api";

// The AngularJS mfaModeWidgetController on accountDetail and organizationDetail2: the subject's mode, opening its blade.
const props = defineProps<{ subjectType: MfaSubjectType }>();

const ctx = injectBladeContext();
const subject = computed(() => ctx.value.item as { id?: string; userName?: string; name?: string } | undefined);
const { openBlade } = useBlade();
const mode = ref<MfaMode>();

watch(
  [() => subject.value?.id, modeVersion],
  async ([id]) => {
    if (!id) return;
    mode.value = await getMode(props.subjectType, id).catch(() => undefined);
  },
  { immediate: true },
);

const open = () =>
  openBlade({
    name: "MfaMode",
    options: {
      subjectType: props.subjectType,
      subjectId: subject.value?.id,
      subjectName: subject.value?.userName ?? subject.value?.name,
    },
  });
</script>
