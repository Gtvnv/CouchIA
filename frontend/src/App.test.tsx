import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Mock } from 'vitest';
import { App } from './App';
import { ApiError } from './api/client';
import type { ApiClient } from './api/client';
import type {
  CategoriaEntrevista,
  Entrevista,
  EstadoCategoria,
  EventoMensagem,
  MapaHabilidades,
  PerfilUsuario,
  PlanoDesenvolvimento,
  RecomendacaoArea,
  Sessao,
} from './api/types';
import { CATEGORIAS_ENTREVISTA } from './api/types';

function entrevistaCom(respondidas: number, estado: Entrevista['estado'] = 'emAndamento'): Entrevista {
  const categorias = {} as Record<CategoriaEntrevista, EstadoCategoria>;
  const reformulacoes = {} as Record<CategoriaEntrevista, number>;
  CATEGORIAS_ENTREVISTA.forEach((c, i) => {
    categorias[c] = i < respondidas ? 'respondida' : 'pendente';
    reformulacoes[c] = 0;
  });
  return { estado, categorias, reformulacoes };
}

function perfil(horas: number | null = 10): PerfilUsuario {
  return {
    interesses: ['dados'],
    experienciaPrevia: null,
    formacao: null,
    estiloDeTrabalho: null,
    horasEstudoPorSemana: horas,
    objetivos: null,
    habilidadesDominadas: [],
  };
}

function sessao(respondidas = 0, horas: number | null = 10): Sessao {
  return {
    criadaEm: '2025-01-01T00:00:00Z',
    historico: [],
    perfil: perfil(horas),
    entrevista: entrevistaCom(respondidas),
    recomendacoes: [],
    planos: [],
    feedbacks: [],
  };
}

const recomendacoes: RecomendacaoArea[] = [
  { areaId: 'dados', nome: 'Engenharia de Dados', pontuacao: 90, justificativa: 'Você gosta de dados.' },
  { areaId: 'web', nome: 'Desenvolvimento Web', pontuacao: 70, justificativa: 'Você citou web.' },
];

const mapa: MapaHabilidades = {
  areaId: 'dados',
  habilidades: [
    { id: 'sql', nome: 'SQL', descricao: 'Consultas', prioridade: 'essencial', dominada: false },
    { id: 'py', nome: 'Python', descricao: 'Linguagem', prioridade: 'importante', dominada: false },
  ],
};

const plano: PlanoDesenvolvimento = {
  id: 'p1',
  areaId: 'dados',
  horasPorSemana: 10,
  duracaoTotalSemanas: 4,
  etapas: [
    {
      numero: 1,
      habilidades: ['sql'],
      duracaoSemanas: 4,
      recursos: [{ titulo: 'Curso de SQL', url: 'https://exemplo.com/sql' }],
    },
  ],
};

async function* respostaFinal(entrevista: Entrevista): AsyncGenerator<EventoMensagem> {
  yield { tipo: 'trecho', texto: 'Conte mais' };
  yield {
    tipo: 'final',
    mensagem: { clientMessageId: 'm-a', ordem: 2, papel: 'assistant', texto: 'Conte mais sobre você.', criadaEm: '' },
    entrevista,
    perfil: perfil(),
  };
}

function criarApi(over: Partial<Record<keyof ApiClient, unknown>> = {}) {
  const api = {
    criarSessao: vi.fn().mockResolvedValue(sessao(0)),
    obterSessao: vi.fn().mockResolvedValue(sessao(3)),
    enviarMensagem: vi.fn(() => respostaFinal(entrevistaCom(3))),
    encerrarEntrevista: vi.fn().mockResolvedValue({ categoriasSemResposta: [] }),
    listarRecomendacoes: vi.fn().mockResolvedValue(recomendacoes),
    registrarFeedback: vi.fn().mockResolvedValue({ areaId: 'dados', tipo: 'util', motivo: null, em: '' }),
    obterMapaHabilidades: vi.fn().mockResolvedValue(mapa),
    gerarPlano: vi.fn().mockResolvedValue(plano),
    exportarPlano: vi.fn().mockResolvedValue(new Blob(['# plano'])),
    excluirSessao: vi.fn().mockResolvedValue(undefined),
    ...over,
  };
  return api as unknown as { [K in keyof ApiClient]: Mock } & ApiClient;
}

async function iniciarEConversar(api: ApiClient) {
  render(<App api={api} />);
  await userEvent.click(screen.getByRole('button', { name: 'Iniciar entrevista' }));
  await screen.findByRole('heading', { name: 'Conversa' });
  await userEvent.type(screen.getByLabelText('Sua mensagem'), 'Gosto de dados');
  await userEvent.click(screen.getByRole('button', { name: 'Enviar' }));
  await screen.findByText(/Conte mais sobre você/);
}

async function irParaRecomendacoes(api: ApiClient) {
  await iniciarEConversar(api);
  await userEvent.click(screen.getByRole('button', { name: /Encerrar entrevista/ }));
  await screen.findByRole('heading', { name: 'Recomendações' });
  await screen.findByText('Engenharia de Dados');
}

beforeEach(() => {
  URL.createObjectURL = vi.fn(() => 'blob:fake');
  URL.revokeObjectURL = vi.fn();
});
afterEach(() => vi.restoreAllMocks());

describe('App: fluxo completo', () => {
  it('inicia, conversa, encerra, vê recomendações, abre o mapa, gera o plano e exporta', async () => {
    const api = criarApi();
    await irParaRecomendacoes(api);

    expect(api.criarSessao).toHaveBeenCalledOnce();
    expect(api.enviarMensagem).toHaveBeenCalledOnce();
    expect(api.encerrarEntrevista).toHaveBeenCalledOnce();
    // 3 categorias respondidas: sem aviso de baixa confiança.
    expect(screen.queryByTestId('aviso-baixa-confianca')).not.toBeInTheDocument();

    await userEvent.click(screen.getAllByRole('button', { name: 'Ver mapa de habilidades' })[0]);
    await screen.findByRole('heading', { name: 'Mapa de habilidades' });
    expect(await screen.findByText('SQL')).toBeInTheDocument();
    expect(api.obterMapaHabilidades).toHaveBeenCalledWith('dados');

    await userEvent.click(screen.getByRole('button', { name: 'Gerar plano de desenvolvimento' }));
    await screen.findByRole('list', { name: 'Etapas do plano' });
    expect(api.gerarPlano).toHaveBeenCalledWith('dados', undefined);
    // Nome da habilidade vindo do mapa, não o id.
    expect(screen.getByText('SQL')).toBeInTheDocument();

    const clique = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    await userEvent.click(screen.getByRole('button', { name: 'Exportar Markdown' }));
    await waitFor(() => expect(api.exportarPlano).toHaveBeenCalledWith('p1', 'md'));
    await waitFor(() => expect(clique).toHaveBeenCalled());
    expect(URL.createObjectURL).toHaveBeenCalled();
  });

  it('exibe o aviso de baixa confiança com menos de três categorias respondidas', async () => {
    const api = criarApi({ enviarMensagem: vi.fn(() => respostaFinal(entrevistaCom(1))) });
    api.obterSessao.mockResolvedValue(sessao(1));
    await irParaRecomendacoes(api);
    expect(screen.getByTestId('aviso-baixa-confianca')).toBeInTheDocument();
    // Entrevista ainda em andamento: permite continuar.
    await userEvent.click(screen.getByRole('button', { name: 'Continuar entrevista' }));
    await screen.findByRole('heading', { name: 'Conversa' });
  });

  it('vai direto às recomendações quando a entrevista é concluída pela conversa', async () => {
    const api = criarApi({
      enviarMensagem: vi.fn(() => respostaFinal(entrevistaCom(6, 'concluida'))),
    });
    render(<App api={api} />);
    await userEvent.click(screen.getByRole('button', { name: 'Iniciar entrevista' }));
    await userEvent.type(await screen.findByLabelText('Sua mensagem'), 'Oi');
    await userEvent.click(screen.getByRole('button', { name: 'Enviar' }));
    await screen.findByText('Engenharia de Dados');
    expect(api.encerrarEntrevista).not.toHaveBeenCalled();
  });

  it('registra feedback e atualiza o cartão', async () => {
    const api = criarApi();
    await irParaRecomendacoes(api);
    await userEvent.click(screen.getAllByRole('button', { name: 'Útil' })[0]);
    await waitFor(() => expect(api.registrarFeedback).toHaveBeenCalledWith('dados', { tipo: 'util' }));
  });
});

describe('App: erros e nova tentativa', () => {
  it('falha ao iniciar a entrevista e permite tentar novamente', async () => {
    const api = criarApi();
    api.criarSessao.mockRejectedValueOnce(new ApiError(500, 'Erro interno'));
    render(<App api={api} />);
    await userEvent.click(screen.getByRole('button', { name: 'Iniciar entrevista' }));
    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent('Erro interno');
    await userEvent.click(within(alerta).getByRole('button', { name: 'Tentar novamente' }));
    await screen.findByRole('heading', { name: 'Conversa' });
    expect(api.criarSessao).toHaveBeenCalledTimes(2);
  });

  it('recusa encerrar sem nenhuma categoria respondida e permanece na conversa (Req. 2.7)', async () => {
    const api = criarApi();
    api.encerrarEntrevista.mockRejectedValueOnce(
      new ApiError(400, 'Responda ao menos uma categoria antes de encerrar.'),
    );
    render(<App api={api} />);
    await userEvent.click(screen.getByRole('button', { name: 'Iniciar entrevista' }));
    await screen.findByRole('heading', { name: 'Conversa' });
    await userEvent.click(screen.getByRole('button', { name: /Encerrar entrevista/ }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Responda ao menos uma categoria');
    expect(screen.getByRole('heading', { name: 'Conversa' })).toBeInTheDocument();
    expect(api.listarRecomendacoes).not.toHaveBeenCalled();
  });

  it('falha ao gerar recomendações sem exibir lista e tenta novamente (Req. 3.7)', async () => {
    const api = criarApi();
    api.listarRecomendacoes.mockRejectedValueOnce(new ApiError(503, 'IA indisponível'));
    await iniciarEConversar(api);
    await userEvent.click(screen.getByRole('button', { name: /Encerrar entrevista/ }));
    expect(await screen.findByRole('alert')).toHaveTextContent('IA indisponível');
    expect(screen.queryByRole('list', { name: 'Áreas recomendadas' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    await screen.findByText('Engenharia de Dados');
  });

  it('falha ao carregar o mapa sem exibir mapa parcial e tenta novamente (Req. 4.6)', async () => {
    const api = criarApi();
    await irParaRecomendacoes(api);
    api.obterMapaHabilidades.mockRejectedValueOnce(new ApiError(503, 'Catálogo indisponível'));
    await userEvent.click(screen.getAllByRole('button', { name: 'Ver mapa de habilidades' })[0]);
    expect(await screen.findByRole('alert')).toHaveTextContent('Catálogo indisponível');
    expect(screen.queryByText('SQL')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    await screen.findByText('SQL');
  });

  it('falha ao gerar o plano e tenta novamente (Req. 5.8)', async () => {
    const api = criarApi();
    await irParaRecomendacoes(api);
    api.gerarPlano.mockRejectedValueOnce(new ApiError(500, 'Falha ao gerar'));
    await userEvent.click(screen.getAllByRole('button', { name: 'Gerar plano de desenvolvimento' })[0]);
    expect(await screen.findByRole('alert')).toHaveTextContent('Falha ao gerar');
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    await screen.findByRole('list', { name: 'Etapas do plano' });
    expect(api.gerarPlano).toHaveBeenCalledTimes(2);
  });

  it('pede a disponibilidade quando o perfil não a tem e gera o plano com o valor informado (Req. 5.6)', async () => {
    const api = criarApi({
      enviarMensagem: vi.fn(() => {
        return (async function* (): AsyncGenerator<EventoMensagem> {
          yield {
            tipo: 'final',
            mensagem: { clientMessageId: 'm-a', ordem: 2, papel: 'assistant', texto: 'Conte mais sobre você.', criadaEm: '' },
            entrevista: entrevistaCom(3),
            perfil: perfil(null),
          };
        })();
      }),
    });
    api.obterSessao.mockResolvedValue(sessao(3, null));
    await irParaRecomendacoes(api);
    await userEvent.click(screen.getAllByRole('button', { name: 'Gerar plano de desenvolvimento' })[0]);
    await userEvent.type(await screen.findByLabelText('Horas de estudo por semana'), '10');
    await userEvent.click(screen.getByRole('button', { name: 'Gerar plano' }));
    await screen.findByRole('list', { name: 'Etapas do plano' });
    expect(api.gerarPlano).toHaveBeenCalledOnce();
    expect(api.gerarPlano).toHaveBeenCalledWith('dados', 10);
  });

  it('informa quando não há habilidades pendentes (Req. 5.7)', async () => {
    const api = criarApi();
    await irParaRecomendacoes(api);
    api.gerarPlano.mockRejectedValueOnce(
      new ApiError(422, 'Nada a fazer', { type: 'https://couchia.app/problemas/PlanoSemHabilidadesPendentes' }),
    );
    await userEvent.click(screen.getAllByRole('button', { name: 'Gerar plano de desenvolvimento' })[0]);
    expect(await screen.findByText(/Não há habilidades pendentes/)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('falha ao exportar o plano e tenta novamente', async () => {
    const api = criarApi();
    await irParaRecomendacoes(api);
    await userEvent.click(screen.getAllByRole('button', { name: 'Gerar plano de desenvolvimento' })[0]);
    await screen.findByRole('list', { name: 'Etapas do plano' });
    api.exportarPlano.mockRejectedValueOnce(new ApiError(500, 'Falha na exportação'));
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    await userEvent.click(screen.getByRole('button', { name: 'Exportar PDF' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Falha na exportação');
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    await waitFor(() => expect(api.exportarPlano).toHaveBeenCalledTimes(2));
    expect(api.exportarPlano).toHaveBeenLastCalledWith('p1', 'pdf');
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument());
  });
});

describe('App: exclusão de dados', () => {
  it('exclui com confirmação e volta às boas-vindas', async () => {
    const api = criarApi();
    await irParaRecomendacoes(api);
    await userEvent.click(screen.getByRole('button', { name: 'Excluir meus dados' }));
    expect(api.excluirSessao).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Confirmar exclusão' }));
    await screen.findByRole('button', { name: 'Iniciar entrevista' });
    expect(api.excluirSessao).toHaveBeenCalledOnce();
  });
});
