import fc from 'fast-check';
import { describe, expect, it, vi } from 'vitest';
import { ApiError, criarApiClient, lerEventosSse } from './client';
import type { EventoMensagem } from './types';

function streamDe(partes: string[]): ReadableStream<Uint8Array> {
  const enc = new TextEncoder();
  return new ReadableStream({
    start(c) {
      partes.forEach((p) => c.enqueue(enc.encode(p)));
      c.close();
    },
  });
}

async function coletar(s: ReadableStream<Uint8Array>) {
  const out: EventoMensagem[] = [];
  for await (const e of lerEventosSse(s)) out.push(e);
  return out;
}

describe('criarApiClient', () => {
  it('envia o cookie de sessão (credentials: include)', async () => {
    const fetchImpl = vi.fn(async () => new Response(JSON.stringify({ historico: [] }), { status: 200 }));
    await criarApiClient({ fetchImpl: fetchImpl as unknown as typeof fetch }).obterSessao();
    const [url, init] = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe('/api/sessoes/atual');
    expect(init.credentials).toBe('include');
  });

  it('converte ProblemDetails em ApiError', async () => {
    const fetchImpl = vi.fn(
      async () => new Response(JSON.stringify({ title: 'Inválido', status: 400 }), { status: 400 }),
    );
    const client = criarApiClient({ fetchImpl: fetchImpl as unknown as typeof fetch });
    await expect(client.listarRecomendacoes()).rejects.toMatchObject({
      name: 'ApiError',
      status: 400,
      message: 'Inválido',
    });
    await expect(client.listarRecomendacoes()).rejects.toBeInstanceOf(ApiError);
  });

  it('exclui a sessão com confirmação explícita no corpo', async () => {
    const fetchImpl = vi.fn(async () => new Response(null, { status: 204 }));
    await criarApiClient({ fetchImpl: fetchImpl as unknown as typeof fetch }).excluirSessao();
    const [, init] = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(init.method).toBe('DELETE');
    expect(init.body).toBe(JSON.stringify({ confirmar: true }));
  });
});

describe('lerEventosSse', () => {
  it('interpreta eventos mesmo quando divididos em pedaços arbitrários', async () => {
    await fc.assert(
      fc.asyncProperty(
        fc.array(fc.string({ minLength: 1, maxLength: 30 }), { minLength: 1, maxLength: 6 }),
        fc.integer({ min: 1, max: 7 }),
        async (textos, tamanho) => {
          const bruto = textos
            .map((texto) => `data: ${JSON.stringify({ tipo: 'trecho', texto })}\n\n`)
            .join('');
          const pedacos = bruto.match(new RegExp(`[\\s\\S]{1,${tamanho}}`, 'g')) ?? [];
          const eventos = await coletar(streamDe(pedacos));
          expect(eventos).toEqual(textos.map((texto) => ({ tipo: 'trecho', texto })));
        },
      ),
      { numRuns: 100 },
    );
  });
});
