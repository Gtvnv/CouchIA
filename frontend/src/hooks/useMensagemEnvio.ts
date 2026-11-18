import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../api/client';
import { LIMITES } from '../api/types';
import type { EventoMensagem, MensagemEnviarRequest, PapelMensagem } from '../api/types';

export const TIMEOUT_PRIMEIRO_TRECHO_MS = 30_000;

export const MSG_LIMITE = 'A mensagem deve ter de 1 a 2.000 caracteres.';
export const MSG_DEMORA =
  'O mentor demorou demais para responder. Sua mensagem foi preservada; tente reenviá-la.';
export const MSG_FALHA =
  'Não foi possível obter a resposta do mentor. Sua mensagem foi preservada; tente reenviá-la.';

export type EnviarMensagemFn = (
  req: MensagemEnviarRequest,
  signal?: AbortSignal,
) => AsyncIterable<EventoMensagem>;

export interface MensagemChat {
  clientMessageId: string;
  papel: PapelMensagem;
  texto: string;
}

export type EventoFinal = Extract<EventoMensagem, { tipo: 'final' }>;

export interface UseMensagemEnvioOptions {
  enviarMensagem: EnviarMensagemFn;
  historicoInicial?: MensagemChat[];
  timeoutMs?: number;
  onFinal?: (evento: EventoFinal) => void;
}

export type StatusEnvio = 'ocioso' | 'processando' | 'erro';

/** Mensagem aceita se, sem espaços nas pontas, não é vazia e tem no máximo 2.000 caracteres. */
export function validarMensagem(texto: string): boolean {
  return texto.trim().length > 0 && texto.length <= LIMITES.mensagemMax;
}

const TEMPO_ESGOTADO = Symbol('tempo-esgotado');

function novoId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

/**
 * Controla o envio de mensagens do Usuário.
 *
 * - Rejeição (validação local ou recusa da API): o texto permanece no campo de entrada.
 * - Falha após o envio (demora de 30 s sem primeiro trecho, erro de rede ou do fluxo): a
 *   mensagem fica no histórico e `reenviar` repete a mesma entrada (mesmo clientMessageId).
 */
export function useMensagemEnvio({
  enviarMensagem,
  historicoInicial = [],
  timeoutMs = TIMEOUT_PRIMEIRO_TRECHO_MS,
  onFinal,
}: UseMensagemEnvioOptions) {
  const [texto, setTexto] = useState('');
  const [mensagens, setMensagens] = useState<MensagemChat[]>(historicoInicial);
  const [respostaParcial, setRespostaParcial] = useState('');
  const [status, setStatus] = useState<StatusEnvio>('ocioso');
  const [erro, setErro] = useState<string | null>(null);
  const [pendente, setPendente] = useState<MensagemEnviarRequest | null>(null);

  const emAndamento = useRef(false);
  const controller = useRef<AbortController | null>(null);
  const onFinalRef = useRef(onFinal);
  onFinalRef.current = onFinal;
  const enviarRef = useRef(enviarMensagem);
  enviarRef.current = enviarMensagem;

  useEffect(() => () => controller.current?.abort(), []);

  const executar = useCallback(
    async (req: MensagemEnviarRequest, jaNoHistorico: boolean) => {
      emAndamento.current = true;
      setStatus('processando');
      setErro(null);
      setPendente(null);
      setRespostaParcial('');

      const ctrl = new AbortController();
      controller.current = ctrl;
      let recebeuEvento = false;
      let registrada = jaNoHistorico;

      const registrar = () => {
        if (registrada) return;
        registrada = true;
        setMensagens((m) => [...m, { clientMessageId: req.clientMessageId, papel: 'user', texto: req.texto }]);
      };

      const falhar = (mensagem: string) => {
        registrar();
        setTexto('');
        setPendente(req);
        setErro(mensagem);
        setStatus('erro');
      };

      let timer: ReturnType<typeof setTimeout> | undefined;
      const esgotou = new Promise<typeof TEMPO_ESGOTADO>((resolve) => {
        timer = setTimeout(() => resolve(TEMPO_ESGOTADO), timeoutMs);
      });

      try {
        const iterador = enviarRef.current(req, ctrl.signal)[Symbol.asyncIterator]();
        let primeiro = true;
        for (;;) {
          const passo = primeiro
            ? await Promise.race([iterador.next(), esgotou])
            : await iterador.next();
          if (passo === TEMPO_ESGOTADO) {
            ctrl.abort();
            falhar(MSG_DEMORA);
            return;
          }
          if (primeiro) {
            primeiro = false;
            clearTimeout(timer);
          }
          if (passo.done) break;
          const evento = passo.value;
          if (!recebeuEvento) {
            recebeuEvento = true;
            registrar();
            setTexto('');
          }
          if (evento.tipo === 'trecho') {
            setRespostaParcial((p) => p + evento.texto);
          } else if (evento.tipo === 'final') {
            setMensagens((m) => [
              ...m,
              {
                clientMessageId: evento.mensagem.clientMessageId,
                papel: 'assistant',
                texto: evento.mensagem.texto,
              },
            ]);
            setRespostaParcial('');
            setStatus('ocioso');
            onFinalRef.current?.(evento);
            return;
          } else {
            setRespostaParcial('');
            falhar(evento.mensagem || MSG_FALHA);
            return;
          }
        }
        // Fluxo encerrado sem evento final.
        setRespostaParcial('');
        falhar(MSG_FALHA);
      } catch (e) {
        setRespostaParcial('');
        if (e instanceof ApiError && !recebeuEvento) {
          // Rejeição: nada foi registrado; o texto permanece no campo.
          setErro(e.message);
          setStatus('erro');
          setPendente(null);
          return;
        }
        falhar(MSG_FALHA);
      } finally {
        clearTimeout(timer);
        emAndamento.current = false;
      }
    },
    [timeoutMs],
  );

  const enviar = useCallback(async () => {
    if (emAndamento.current) return;
    if (!validarMensagem(texto)) {
      setErro(MSG_LIMITE);
      setStatus('erro');
      setPendente(null);
      return;
    }
    await executar({ clientMessageId: novoId(), texto }, false);
  }, [texto, executar]);

  const reenviar = useCallback(async () => {
    if (emAndamento.current || !pendente) return;
    await executar(pendente, true);
  }, [pendente, executar]);

  return {
    texto,
    setTexto,
    mensagens,
    respostaParcial,
    status,
    processando: status === 'processando',
    erro,
    podeReenviar: pendente !== null && status === 'erro',
    enviar,
    reenviar,
  };
}
