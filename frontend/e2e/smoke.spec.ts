import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

test('página inicial sem violações WCAG 2.1 A/AA', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'IA Mentor de Carreira', exact: true })).toBeVisible();
  const resultado = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  expect(resultado.violations).toEqual([]);
});
