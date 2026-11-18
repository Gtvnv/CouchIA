import type {
  AreaId,
  EntrevistaEncerrarResponse,
  EventoMensagem,
  FeedbackRecomendacao,
  FeedbackRegistrarRequest,
  FormatoExportacao,
  MapaHabilidades,
  MensagemEnviarRequest,
  PlanoDesenvolvimento,
  ProblemDetails,
  RecomendacaoArea,
  Sessao,
} from './types';

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}

export interface ApiClientOptions {
  baseUrl?: string;
  fetchImpl?: typeof fetch;
}

async function lerProblema(resp: Response): Promise<ApiError> {
  let problem: ProblemDetails | undefined;
  try {
    problem = (await resp.json()) as ProblemDetails;
  } catch {
    problem = undefined;
  }
  const mensagem = problem?.detail ?? problem?.title ?? `Falha na requisição (${resp.status})`;
  return new ApiError(resp.status, mensagem, problem);
}

/** Interpreta um fluxo SSE em eventos `data: {json}`. */
export async function* lerEventosSse(body: ReadableStream<Uint8Array>): AsyncGenerator<EventoMensagem> {
  const reader = body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      let idx: number;
      while ((idx = buffer.search(/\r?\n\r?\n/)) >= 0) {
        const bloco = buffer.slice(0, idx);
        buffer = buffer.slice(idx).replace(/^\r?\n\r?\n/, '');
        const evento = interpretarBloco(bloco);
        if (evento) yield evento;
      }
    }
    buffer += decoder.decode();
    const resto = interpretarBloco(buffer);
    if (resto) yield resto;
  } finally {
    reader.releaseLock();
  }
}

function interpretarBloco(bloco: string): EventoMensagem | null {
  const dados = bloco
    .split(/\r?\n/)
    .filter((l) => l.startsWith('data:'))
    .map((l) => l.slice(5).replace(/^ /, ''))
    .join('\n');
  if (!dados) return null;
  return JSON.parse(dados) as EventoMensagem;
}

/**
 * Cliente HTTP da API. A sessão é identificada por cookie HttpOnly definido pelo servidor,
 * por isso toda requisição usa `credentials: 'include'` e o frontend nunca lê o identificador.
 */
export function criarApiClient({ baseUrl = '', fetchImpl }: ApiClientOptions = {}) {
  const doFetch: typeof fetch = (input, init) => (fetchImpl ?? fetch)(input, init);

  async function request(path: string, init: RequestInit = {}): Promise<Response> {
    const headers = new Headers(init.headers);
    if (init.body !== undefined && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json');
    }
    const resp = await doFetch(`${baseUrl}${path}`, { ...init, headers, credentials: 'include' });
    if (!resp.ok) throw await lerProblema(resp);
    return resp;
  }

  async function json<T>(path: string, init?: RequestInit): Promise<T> {
    const resp = await request(path, init);
    return (await resp.json()) as T;
  }

  const sessaoAtual = '/api/sessoes/atual';

  return {
    criarSessao: () => json<Sessao>('/api/sessoes', { method: 'POST' }),
    obterSessao: () => json<Sessao>(sessaoAtual),

    /** Envia a mensagem e devolve os eventos SSE (trechos e evento final). */
    async *enviarMensagem(
      req: MensagemEnviarRequest,
      signal?: AbortSignal,
    ): AsyncGenerator<EventoMensagem> {
      const resp = await request(`${sessaoAtual}/mensagens`, {
        method: 'POST',
        headers: { Accept: 'text/event-stream' },
        body: JSON.stringify(req),
        signal,
      });
      if (!resp.body) throw new ApiError(resp.status, 'Resposta sem corpo');
      yield* lerEventosSse(resp.body);
    },

    encerrarEntrevista: () =>
      json<EntrevistaEncerrarResponse>(`${sessaoAtual}/entrevista/encerrar`, { method: 'POST' }),

    listarRecomendacoes: () => json<RecomendacaoArea[]>(`${sessaoAtual}/recomendacoes`),

    registrarFeedback: (areaId: AreaId, req: FeedbackRegistrarRequest) =>
      json<FeedbackRecomendacao>(
        `${sessaoAtual}/recomendacoes/${encodeURIComponent(areaId)}/feedback`,
        { method: 'PUT', body: JSON.stringify(req) },
      ),

    obterMapaHabilidades: (areaId: AreaId) =>
      json<MapaHabilidades>(`/api/areas/${encodeURIComponent(areaId)}/habilidades`),

    gerarPlano: (areaId: AreaId, horasPorSemana?: number) =>
      json<PlanoDesenvolvimento>(`${sessaoAtual}/planos`, {
        method: 'POST',
        body: JSON.stringify(horasPorSemana === undefined ? { areaId } : { areaId, horasPorSemana }),
      }),

    async exportarPlano(id: string, formato: FormatoExportacao): Promise<Blob> {
      const resp = await request(
        `${sessaoAtual}/planos/${encodeURIComponent(id)}/exportar?formato=${formato}`,
      );
      return resp.blob();
    },

    async excluirSessao(): Promise<void> {
      await request(sessaoAtual, { method: 'DELETE', body: JSON.stringify({ confirmar: true }) });
    },
  };
}

export type ApiClient = ReturnType<typeof criarApiClient>;
