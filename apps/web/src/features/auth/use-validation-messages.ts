import { useTranslations } from "next-intl";
import { PASSWORD_MAX, PASSWORD_MIN, type ValidationMessages } from "./schemas";

export function useValidationMessages(): ValidationMessages {
  const t = useTranslations("validation");
  return {
    required: t("required"),
    email: t("email"),
    passwordMin: t("passwordMin", { min: PASSWORD_MIN }),
    passwordMax: t("passwordMax", { max: PASSWORD_MAX }),
    handle: t("handle"),
    displayName: t("displayName"),
    passwordsMismatch: t("passwordsMismatch"),
  };
}
