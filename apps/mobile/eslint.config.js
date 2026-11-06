// https://docs.expo.dev/guides/using-eslint/
const { defineConfig } = require("eslint/config");
const expoConfig = require("eslint-config-expo/flat");
const prettierConfig = require("eslint-config-prettier/flat");

module.exports = defineConfig([
  expoConfig,
  // Formatação é do Prettier: desliga as regras de estilo que brigariam com ele.
  prettierConfig,
  { ignores: ["dist/*", ".expo/*"] },
  // Toda cor vem dos tokens (design/tokens.json), nos dois temas.
  {
    files: ["src/**/*.{ts,tsx}"],
    rules: {
      "no-restricted-syntax": [
        "error",
        {
          selector: "Literal[value=/^(#[0-9a-fA-F]{3,8}|rgba?\\(|hsla?\\()/]",
          message: "Cor literal: use os tokens do tema.",
        },
      ],
    },
  },
]);
