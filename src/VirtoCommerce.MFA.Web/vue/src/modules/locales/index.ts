// The module's Backoffice translations, shared with the AngularJS admin, so both admins say the same in all languages.
// angular-translate writes placeholders as {{name}}; vue-i18n reads {name} and treats @, | and braces as syntax.
const files = import.meta.glob<Record<string, unknown>>("../../../../Localizations/*.VirtoCommerce.MFA.json", {
  eager: true,
  import: "default",
});

function toVueI18n(value: unknown): unknown {
  if (typeof value === "string") {
    const placeholders: string[] = [];
    return value
      .replace(/\{\{\s*(\w+)\s*\}\}/g, (_, name: string) => `\u0000${placeholders.push(name) - 1}\u0000`)
      .replace(/[@|{}]/g, (char) => `{'${char}'}`)
      .replace(/\u0000(\d+)\u0000/g, (_, index: string) => `{${placeholders[Number(index)]}}`);
  }

  if (value && typeof value === "object") {
    return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, toVueI18n(item)]));
  }

  return value;
}

export const locales = Object.fromEntries(
  Object.entries(files).map(([path, messages]) => [
    path.split("/").pop()!.split(".")[0],
    toVueI18n({ mfa: (messages as { mfa: unknown }).mfa }),
  ]),
) as Record<string, Record<string, unknown>>;
