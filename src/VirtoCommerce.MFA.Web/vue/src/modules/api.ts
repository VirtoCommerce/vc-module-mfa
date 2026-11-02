// The module's REST API under api/mfa/modes: read with mfa:read, change with mfa:manage.
import { ref } from "vue";

export type MfaSubjectType = "user" | "organization";

export type MfaMode = "Off" | "Enabled" | "Required";

export const MFA_MODES: MfaMode[] = ["Off", "Enabled", "Required"];

export interface MfaStoreStatus {
  storeId?: string;
  storeName?: string;
  storeMode?: string;
  isRequired?: boolean;
  requiredBy?: string;
}

export interface MfaStatus {
  isGloballyEnabled?: boolean;
  isRequiredForBackoffice?: boolean | null;
  stores?: MfaStoreStatus[];
}

/** Bumped after a save, so the widgets of the changed subject read the new mode. */
export const modeVersion = ref(0);

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    credentials: "same-origin",
    headers:
      body === undefined ? { Accept: "application/json" } : { "Content-Type": "application/json", Accept: "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  if (!response.ok) {
    throw new Error(text || `${response.status} ${response.statusText}`);
  }
  return (text ? JSON.parse(text) : undefined) as T;
}

const segment = (subjectType: MfaSubjectType) => (subjectType === "organization" ? "organizations" : "users");
const modeUrl = (subjectType: MfaSubjectType, id: string) => `/api/mfa/modes/${segment(subjectType)}/${encodeURIComponent(id)}`;

export const getMode = async (subjectType: MfaSubjectType, id: string) =>
  (await request<{ mode?: MfaMode }>("GET", modeUrl(subjectType, id))).mode ?? "Off";

export const setMode = async (subjectType: MfaSubjectType, id: string, mode: MfaMode) => {
  const result = await request<{ mode?: MfaMode }>("PUT", modeUrl(subjectType, id), { mode });
  modeVersion.value += 1;
  return result.mode ?? mode;
};

/** The effective status per store; `mode` evaluates a mode before it is saved. */
export const getStatus = (subjectType: MfaSubjectType, id: string, mode?: MfaMode) =>
  request<MfaStatus>("GET", `${modeUrl(subjectType, id)}/status${mode ? `?mode=${encodeURIComponent(mode)}` : ""}`);
