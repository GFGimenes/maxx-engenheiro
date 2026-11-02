// https://docs.expo.dev/guides/using-eslint/
const { defineConfig } = require("eslint/config");
const expoConfig = require("eslint-config-expo/flat");
const prettierConfig = require("eslint-config-prettier/flat");

module.exports = defineConfig([
  expoConfig,
  // Formatação é do Prettier: desliga as regras de estilo que brigariam com ele.
  prettierConfig,
  { ignores: ["dist/*", ".expo/*"] },
]);
