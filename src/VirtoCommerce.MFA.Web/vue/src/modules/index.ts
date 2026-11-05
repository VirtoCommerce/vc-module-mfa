import { defineAppModule, registerExternalWidget, usePermissions } from "@vc-shell/framework";
import { defineComponent, h, markRaw } from "vue";
import * as blades from "./pages";
import { locales } from "./locales";
import MfaModeWidget from "./widgets/MfaModeWidget.vue";
import type { MfaSubjectType } from "./api";

// The AngularJS admin registers the same widget on accountDetail and organizationDetail2, shown with mfa:read on a saved
// subject: the Admin Shell's user blade and the Customer module's organization blade publish their item for it.
function registerModeWidget(id: string, subjectType: MfaSubjectType, targetBlade: string) {
  registerExternalWidget({
    id,
    title: "Multi-factor authentication",
    component: markRaw(defineComponent({ name: id, setup: () => () => h(MfaModeWidget, { subjectType }) })),
    targetBlades: [targetBlade],
    isVisible: (blade) => !!blade?.param && usePermissions().hasAccess("mfa:read"),
  });
}

registerModeWidget("MfaUserMode", "user", "PlatformSecurityUserDetails");
registerModeWidget("MfaOrganizationMode", "organization", "CustomerOrganizationDetails");

export default defineAppModule({
  blades,
  locales,
});
