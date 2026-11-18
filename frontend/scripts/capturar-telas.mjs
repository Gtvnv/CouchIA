// Captura as telas do app (demo com mock) em 1280px e 390px.
// Uso (com a stack do docker compose no ar):  node scripts/capturar-telas.mjs [baseUrl] [pastaSaida]
import { chromium } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';

const base = process.argv[2] ?? 'http://localhost:8080';
const saida = path.resolve(process.argv[3] ?? '../docs/screenshots');
await mkdir(saida, { recursive: true });

const respostas = [
  'Gosto de APIs, banco de dados e automação',
  'Fiz um estágio de um ano com C# e SQL',
  'Sou formada em Análise e Desenvolvimento de Sistemas',
  'Prefiro trabalhar em equipe, com tarefas bem definidas',
  'Consigo estudar umas 8 horas por semana',
  'Quero ser desenvolvedora backend em uma fintech',
];

const viewports = [
  { nome: '1280', width: 1280, height: 800 },
  { nome: '390', width: 390, height: 844 },
];

const browser = await chromium.launch();
try {
  for (const vp of viewports) {
    const ctx = await browser.newContext({ viewport: { width: vp.width, height: vp.height }, locale: 'pt-BR' });
    const page = await ctx.newPage();
    const foto = (nome, fullPage = true) =>
      page.screenshot({ path: path.join(saida, `${nome}-${vp.nome}.png`), fullPage });

    const enviar = async (texto) => {
      await page.getByLabel('Sua mensagem').fill(texto);
      await page.getByRole('button', { name: 'Enviar', exact: true }).click();
    };
    const aguardarResposta = async () => {
      await page.getByText('O mentor está respondendo…').waitFor({ state: 'hidden', timeout: 30_000 });
    };

    await page.goto(base);
    await foto('1-boas-vindas');

    await page.getByRole('button', { name: 'Iniciar entrevista' }).click();
    await page.getByLabel('Sua mensagem').waitFor();

    await enviar('Oi');
    // Meio do streaming: o texto do mentor aparece aos poucos.
    await page.getByText('O mentor está respondendo…').waitFor();
    await page.waitForTimeout(700);
    await foto('2a-conversa-streaming');
    await aguardarResposta();

    for (const [i, r] of respostas.entries()) {
      await enviar(r);
      if (i < respostas.length - 1) await aguardarResposta();
      if (i === 3) await foto('2b-conversa');
    }

    // Ao concluir a entrevista o app abre as recomendações sozinho.
    await page.getByRole('heading', { name: 'Recomendações' }).waitFor({ timeout: 30_000 });
    await page.getByRole('list', { name: 'Áreas recomendadas' }).waitFor();
    await foto('3-recomendacoes');

    await page.getByRole('button', { name: 'Ver mapa de habilidades' }).first().click();
    await page.getByRole('button', { name: 'Voltar às recomendações' }).waitFor();
    await page.waitForLoadState('networkidle');
    await foto('4-mapa');

    await page.getByRole('button', { name: 'Voltar às recomendações' }).click();
    await page.getByRole('button', { name: 'Gerar plano de desenvolvimento' }).first().click();
    await page.getByRole('heading', { name: 'Plano de desenvolvimento' }).waitFor({ timeout: 30_000 });
    await foto('5-plano');

    await ctx.close();
  }
} finally {
  await browser.close();
}
console.log('Capturas salvas em', saida);
