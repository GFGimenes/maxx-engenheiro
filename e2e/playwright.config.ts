import { defineConfig, devices } from "@playwright/test";

// Roda contra o build web servido pelo Caddy, com o compose de pé:
//   cp .env.example .env && docker compose up --build -d
//   npm test -w @maxx/e2e
// MAXX_E2E_BASE_URL troca o alvo (outra porta, ou a VPS).
const baseURL = process.env.MAXX_E2E_BASE_URL ?? "http://localhost";

const desktop = {
  ...devices["Desktop Chrome"],
  viewport: { width: 1280, height: 900 },
};
// Largura e altura das pranchetas do design.
const celular = {
  ...devices["Desktop Chrome"],
  viewport: { width: 390, height: 844 },
  isMobile: true,
  hasTouch: true,
};

export default defineConfig({
  testDir: "./tests",
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
  use: { baseURL, trace: "retain-on-failure" },
  // Claro e escuro, sempre os dois.
  projects: [
    {
      name: "desktop-claro",
      grep: /@desktop/,
      use: { ...desktop, colorScheme: "light" },
    },
    {
      name: "desktop-escuro",
      grep: /@desktop/,
      use: { ...desktop, colorScheme: "dark" },
    },
    {
      name: "celular-claro",
      grep: /@celular/,
      use: { ...celular, colorScheme: "light" },
    },
    {
      name: "celular-escuro",
      grep: /@celular/,
      use: { ...celular, colorScheme: "dark" },
    },
  ],
});
