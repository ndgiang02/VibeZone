import { describe, expect, it } from "vitest";
import en from "../../messages/en.json";
import vi from "../../messages/vi.json";

type Tree = { [key: string]: string | Tree };

function flatten(tree: Tree, prefix = ""): Record<string, string> {
  return Object.entries(tree).reduce<Record<string, string>>((acc, [key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    return typeof value === "string" ? { ...acc, [path]: value } : { ...acc, ...flatten(value, path) };
  }, {});
}

/** ICU argument names like {email} or {count, plural, …} — must match across languages. */
function argumentsOf(message: string) {
  return [...message.matchAll(/\{\s*(\w+)\s*[,}]/g)].map((m) => m[1]).sort();
}

describe("translation catalogues", () => {
  const enFlat = flatten(en);
  const viFlat = flatten(vi);

  it("vi and en define exactly the same keys", () => {
    expect(Object.keys(viFlat).sort()).toEqual(Object.keys(enFlat).sort());
  });

  it("no message is empty", () => {
    for (const [key, value] of [...Object.entries(enFlat), ...Object.entries(viFlat)]) {
      expect(value.trim(), key).not.toBe("");
    }
  });

  it("placeholders match between languages", () => {
    for (const key of Object.keys(enFlat)) {
      expect(argumentsOf(viFlat[key]!), key).toEqual(argumentsOf(enFlat[key]!));
    }
  });
});
