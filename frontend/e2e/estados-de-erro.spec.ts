import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import type { Locator, Page } from '@playwright/test';

// Requisitos 10.1 e 10.2: estados de erro a 320 e 1280 px (layout e acessibilidade WCAG 2.1 A/AA).
// A API é simulada por interceptação de rotas; nenhum backend real é necessário.

const LARGURAS = [320, 1280];
const TAGS_WCAG = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

const CATEGORIAS_PENDENTES = {
  interesses: 'pendente',
  experienciaPrevia: 'pendente',
  formacao: 'pendente',
  estiloDeTrabalho: 'pendente',
  disponibilidadeSemanal: 'pendente',
  objetivos: 'pendente',
};
const ZERO_REFORMULACOES = {
  interesses: 0,
  experienciaPrevia: 0,
  formacao: 0,
  estiloDeTrabalho: 0,
  disponibilidadeSemanal: 0,
  objetivos: 0,
};

const PERFIL = {
  interesses: ['dados'],
  experienciaPrevia: null,
  formacao: null,
  estiloDeTrabalho: null,
  horasEstudoPorSemana: 10,
  objetivos: null,
  habilidadesDominadas: [],
};

const ENTREVISTA_INICIAL = {
  estado: 'emAndamento',
  categorias: CATEGORIAS_PENDENTES,
  reformulacoes: ZERO_REFORMULACOES,
};
const ENTREVISTA_APOS_MENSAGEM = {
  estado: 'emAndamento',
  categorias: {
    ...CATEGORIAS_PENDENTES,
    interesses: 'respondida',
    experienciaPrevia: 'respondida',
    formacao: 'respondida',
  },
  reformulacoes: ZERO_REFORMULACOES,
};

const SESSAO_INICIAL = {
  criadaEm: '2025-01-01T00:00:00Z',
  historico: [],
  perfil: { ...PERFIL, interesses: [], horasEstudoPorSemana: null },
  entrevista: ENTREVISTA_INICIAL,
  recomendacoes: [],
  planos: [],
  feedbacks: [],
};
const SESSAO_ATUAL = { ...SESSAO_INICIAL, perfil: PERFIL, entrevista: ENTREVISTA_APOS_MENSAGEM };

const RECOMENDACOES = [
  {
    areaId: 'dados',
    nome: 'Engenharia de Dados',
    pontuacao: 88,
    justificativa:
      'Você demonstrou interesse em dados e uma justificativa longa com palavras como desenvolvimentoDeSoftwareOrientadoAServicosDistribuidosEmEscala para exercitar a quebra de linha.',
  },
  { areaId: 'web', nome: 'Desenvolvimento Web', pontuacao: 72, justificativa: 'Interesse em interfaces.' },
  { areaId: 'qa', nome: 'Qualidade de Software', pontuacao: 55, justificativa: 'Atenção a detalhes.' },
];

const MAPA = {
  areaId: 'dados',
  habilidades: [
    { id: 'sql', nome: 'SQL', descricao: 'Consultas relacionais.', prioridade: 'essencial', dominada: false },
    { id: 'py', nome: 'Python', descricao: 'Programação para dados.', prioridade: 'importante', dominada: false },
    { id: 'viz', nome: 'Visualização', descricao: 'Gráficos e painéis.', prioridade: 'desejavel', dominada: true },
  ],
};

const PLANO = {
  id: 'plano-1',
  areaId: 'dados',
  horasPorSemana: 10,
  duracaoTotalSemanas: 6,
  etapas: [
    {
      numero: 1,
      habilidades: ['sql'],
      duracaoSemanas: 3,
      recursos: [{ titulo: 'Curso de SQL', url: 'https://example.com/sql' }],
    },
    {
      numero: 2,
      habilidades: ['py'],
      duracaoSemanas: 3,
      recursos: [{ titulo: 'Curso de Python', url: 'https://example.com/python' }],
    },
  ],
};

function sse(eventos: unknown[]): string {
  return eventos.map((e) => `data: ${JSON.stringify(e)}

`).join('');
}

type Falha = 'iniciar' | 'recomendacoes' | 'mapa' | 'plano' | 'exportacao' | 'exclusao';

async function simularApi(page: Page, falhas: Falha[] = []) {
  await page.route(/\/api\/(sessoes|areas)(\/|\?|$)/, async (route) => {
    const req = route.request();
    const { pathname } = new URL(req.url());
    const metodo = req.method();
    const json = (corpo: unknown, status = 200) =>
      route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(corpo) });

    const falha = () => json({ title: 'Falha', detail: 'Serviço indisponível no momento.' }, 500);

    if (pathname === '/api/sessoes' && metodo === 'POST') {
      return falhas.includes('iniciar') ? falha() : json(SESSAO_INICIAL, 201);
    }
    if (pathname === '/api/sessoes/atual' && metodo === 'GET') return json(SESSAO_ATUAL);
    if (pathname === '/api/sessoes/atual' && metodo === 'DELETE') {
      return falhas.includes('exclusao') ? falha() : route.fulfill({ status: 204 });
    }
    if (pathname.endsWith('/mensagens') && metodo === 'POST') {
      return route.fulfill({
        status: 200,
        contentType: 'text/event-stream',
        body: sse([
          { tipo: 'trecho', texto: 'Obrigado! ' },
          {
            tipo: 'final',
            mensagem: {
              clientMessageId: 'resp-1',
              ordem: 2,
              papel: 'assistant',
              texto: 'Obrigado! Qual é a sua formação?',
              criadaEm: '2025-01-01T00:00:01Z',
            },
            entrevista: ENTREVISTA_APOS_MENSAGEM,
            perfil: PERFIL,
          },
        ]),
      });
    }
    if (pathname.endsWith('/entrevista/encerrar')) return json({ categoriasSemResposta: [] });
    if (pathname.endsWith('/recomendacoes')) {
      return falhas.includes('recomendacoes') ? falha() : json(RECOMENDACOES);
    }
    if (pathname.endsWith('/exportar')) {
      return falhas.includes('exportacao')
        ? falha()
        : route.fulfill({ status: 200, contentType: 'application/pdf', body: 'pdf' });
    }
    if (pathname.endsWith('/habilidades')) {
      return falhas.includes('mapa') ? falha() : json(MAPA);
    }
    if (pathname.endsWith('/planos') && metodo === 'POST') {
      return falhas.includes('plano') ? falha() : json(PLANO, 201);
    }
    return json({ title: 'Rota não simulada' }, 404);
  });
}

const SELETOR_FOCAVEL =
  'a[href], button, input:not([type="hidden"]), select, textarea, [tabindex]:not([tabindex="-1"])';

async function semRolagemHorizontal(page: Page, tela: string) {
  const medidas = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
    bodyScrollWidth: document.body.scrollWidth,
  }));
  expect(medidas.scrollWidth, `${tela}: rolagem horizontal do documento`).toBeLessThanOrEqual(
    medidas.clientWidth,
  );
  expect(medidas.bodyScrollWidth, `${tela}: rolagem horizontal do body`).toBeLessThanOrEqual(
    medidas.clientWidth,
  );
}

async function nomesAcessiveis(page: Page, tela: string) {
  const semNome = await page.evaluate((seletor) => {
    const resultado: string[] = [];
    for (const el of Array.from(document.querySelectorAll<HTMLElement>(seletor))) {
      if ((el as HTMLButtonElement).disabled || el.offsetParent === null) continue;
      const labelledby = el.getAttribute('aria-labelledby');
      const porLabelledby = labelledby
        ? labelledby
            .split(/\s+/)
            .map((id) => document.getElementById(id)?.textContent ?? '')
            .join('')
            .trim()
        : '';
      const labels = (el as HTMLInputElement).labels
        ? Array.from((el as HTMLInputElement).labels!)
            .map((l) => l.textContent ?? '')
            .join('')
            .trim()
        : '';
      const nome =
        porLabelledby ||
        (el.getAttribute('aria-label') ?? '').trim() ||
        labels ||
        (el.textContent ?? '').trim() ||
        (el.getAttribute('title') ?? '').trim();
      if (!nome) resultado.push(el.outerHTML.slice(0, 120));
    }
    return resultado;
  }, SELETOR_FOCAVEL);
  expect(semNome, `${tela}: controles sem nome acessível`).toEqual([]);
}

/** Percorre a página só com Tab e confirma que cada controle recebe foco visível. */
async function navegacaoPorTeclado(page: Page, tela: string) {
  // Clicar no título (não focável) reposiciona o ponto de partida da navegação sequencial.
  await page.locator('h1').click();
  const total = await page.evaluate((seletor) => {
    return Array.from(document.querySelectorAll<HTMLElement>(seletor)).filter(
      (el) => !(el as HTMLButtonElement).disabled && el.offsetParent !== null,
    ).length;
  }, SELETOR_FOCAVEL);
  expect(total, `${tela}: deve haver controles interativos`).toBeGreaterThan(0);

  const alcancados = new Set<number>();
  const semFocoVisivel: string[] = [];
  for (let i = 0; i < total; i++) {
    await page.keyboard.press('Tab');
    const info = await page.evaluate((seletor) => {
      const ativo = document.activeElement as HTMLElement | null;
      if (!ativo || ativo === document.body) return null;
      const lista = Array.from(document.querySelectorAll<HTMLElement>(seletor)).filter(
        (el) => !(el as HTMLButtonElement).disabled && el.offsetParent !== null,
      );
      const estilo = getComputedStyle(ativo);
      const outline = estilo.outlineStyle !== 'none' && parseFloat(estilo.outlineWidth) > 0;
      const sombra = estilo.boxShadow !== 'none';
      return {
        indice: lista.indexOf(ativo),
        visivel: outline || sombra,
        descricao: ativo.outerHTML.slice(0, 100),
      };
    }, SELETOR_FOCAVEL);
    if (!info || info.indice < 0) continue;
    alcancados.add(info.indice);
    if (!info.visivel) semFocoVisivel.push(info.descricao);
  }
  expect(alcancados.size, `${tela}: Tab deve alcançar todos os controles`).toBe(total);
  expect(semFocoVisivel, `${tela}: controles sem indicador de foco`).toEqual([]);
}

async function axe(page: Page, tela: string) {
  const resultado = await new AxeBuilder({ page }).withTags(TAGS_WCAG).analyze();
  expect(resultado.violations, `${tela}: violações axe`).toEqual([]);
}

async function verificarTela(page: Page, tela: string) {
  await semRolagemHorizontal(page, tela);
  await nomesAcessiveis(page, tela);
  await navegacaoPorTeclado(page, tela);
  await axe(page, tela);
}




/** Com Tab a partir do título, o foco deve chegar ao botão informado. */
async function tecladoAlcanca(page: Page, botao: Locator, tela: string) {
  await page.locator('h1').click();
  let alcancou = false;
  for (let i = 0; i < 40 && !alcancou; i++) {
    await page.keyboard.press('Tab');
    alcancou = await botao.evaluate((el) => el === document.activeElement);
  }
  expect(alcancou, `${tela}: Tab deve alcançar o botão de nova tentativa`).toBe(true);
}

async function verificarErro(page: Page, tela: string, mensagem: RegExp, botao: Locator) {
  const alerta = page.getByRole('alert').filter({ hasText: mensagem });
  await expect(alerta, `${tela}: role=alert visível`).toBeVisible();
  await expect(botao).toBeVisible();
  await verificarTela(page, tela);
  await tecladoAlcanca(page, botao, tela);
}

async function ate(page: Page, destino: 'conversa' | 'recomendacoes' | 'mapa' | 'plano') {
  await page.goto('/');
  await page.getByRole('button', { name: 'Iniciar entrevista' }).click();
  await expect(page.getByRole('heading', { name: 'Conversa' })).toBeVisible();
  if (destino === 'conversa') return;
  await page.getByRole('button', { name: /Encerrar entrevista/ }).click();
  await expect(page.getByRole('heading', { name: 'Recomendações' })).toBeVisible();
  if (destino === 'recomendacoes') return;
  await page.getByRole('button', { name: 'Ver mapa de habilidades' }).first().click();
  await expect(page.getByRole('heading', { name: 'Mapa de habilidades' })).toBeVisible();
}

for (const largura of LARGURAS) {
  test.describe(`estados de erro a ${largura}px`, () => {
    test.beforeEach(async ({ page }) => {
      await page.setViewportSize({ width: largura, height: 800 });
    });

    test('erro ao iniciar a entrevista', async ({ page }) => {
      await simularApi(page, ['iniciar']);
      await page.goto('/');
      await page.getByRole('button', { name: 'Iniciar entrevista' }).click();
      await verificarErro(
        page,
        'erro ao iniciar',
        /Serviço indisponível/,
        page.getByRole('button', { name: 'Tentar novamente' }),
      );
    });

    test('erro nas recomendações', async ({ page }) => {
      await simularApi(page, ['recomendacoes']);
      await ate(page, 'conversa');
      await page.getByRole('button', { name: /Encerrar entrevista/ }).click();
      await expect(page.getByRole('heading', { name: 'Recomendações' })).toBeVisible();
      await verificarErro(
        page,
        'erro nas recomendações',
        /Serviço indisponível/,
        page.getByRole('button', { name: 'Tentar novamente' }),
      );
    });

    test('erro no mapa de habilidades', async ({ page }) => {
      await simularApi(page, ['mapa']);
      await ate(page, 'recomendacoes');
      await page.getByRole('button', { name: 'Ver mapa de habilidades' }).first().click();
      await expect(page.getByRole('heading', { name: 'Mapa de habilidades' })).toBeVisible();
      await verificarErro(
        page,
        'erro no mapa',
        /Serviço indisponível/,
        page.getByRole('button', { name: 'Tentar novamente' }),
      );
    });

    test('erro no plano', async ({ page }) => {
      await simularApi(page, ['plano']);
      await ate(page, 'mapa');
      await page.getByRole('button', { name: 'Gerar plano de desenvolvimento' }).click();
      await expect(page.getByRole('heading', { name: 'Plano de desenvolvimento' })).toBeVisible();
      await verificarErro(
        page,
        'erro no plano',
        /Serviço indisponível/,
        page.getByRole('button', { name: 'Tentar novamente' }),
      );
    });

    test('erro na exportação', async ({ page }) => {
      await simularApi(page, ['exportacao']);
      await ate(page, 'mapa');
      await page.getByRole('button', { name: 'Gerar plano de desenvolvimento' }).click();
      await expect(page.getByRole('heading', { name: 'Etapa 1 (3 semanas)' })).toBeVisible();
      await page.getByRole('button', { name: 'Exportar PDF' }).click();
      await verificarErro(
        page,
        'erro na exportação',
        /Serviço indisponível/,
        page.getByRole('button', { name: 'Tentar novamente' }),
      );
    });

    test('confirmação e erro da exclusão de dados', async ({ page }) => {
      await simularApi(page, ['exclusao']);
      await ate(page, 'conversa');
      await page.getByRole('button', { name: 'Excluir meus dados' }).click();
      const dialogo = page.getByRole('alertdialog', { name: 'Excluir meus dados?' });
      await expect(dialogo).toBeVisible();
      await expect(dialogo).toHaveAccessibleDescription(/não poderão ser recuperados/);
      await verificarTela(page, 'confirmação de exclusão');

      await page.getByRole('button', { name: 'Confirmar exclusão' }).click();
      await verificarErro(
        page,
        'erro na exclusão',
        /Não foi possível excluir/,
        page.getByRole('button', { name: 'Confirmar exclusão' }),
      );
    });
  });
}
