import type { FormEvent } from 'react';
import { LIMITES } from '../api/types';
import { useMensagemEnvio } from '../hooks/useMensagemEnvio';
import type { UseMensagemEnvioOptions } from '../hooks/useMensagemEnvio';

export type ChatPanelProps = UseMensagemEnvioOptions;

export function ChatPanel(props: ChatPanelProps) {
  const chat = useMensagemEnvio(props);
  const excedeu = chat.texto.length > LIMITES.mensagemMax;

  function aoSubmeter(e: FormEvent) {
    e.preventDefault();
    void chat.enviar();
  }

  return (
    <section aria-label="Conversa com o mentor">
      <div role="log" aria-live="polite" aria-label="Mensagens">
        <ul>
          {chat.mensagens.map((m) => (
            <li key={`${m.papel}-${m.clientMessageId}`} data-papel={m.papel}>
              <strong>{m.papel === 'user' ? 'Você' : 'Mentor'}:</strong> {m.texto}
            </li>
          ))}
          {chat.respostaParcial && (
            <li data-papel="assistant">
              <strong>Mentor:</strong> {chat.respostaParcial}
            </li>
          )}
        </ul>
      </div>

      {chat.processando && <p role="status">O mentor está respondendo…</p>}

      {chat.erro && <p role="alert">{chat.erro}</p>}
      {chat.podeReenviar && (
        <button type="button" onClick={() => void chat.reenviar()}>
          Reenviar mensagem
        </button>
      )}

      <form onSubmit={aoSubmeter}>
        <label htmlFor="chat-entrada">Sua mensagem</label>
        <textarea
          id="chat-entrada"
          value={chat.texto}
          onChange={(e) => chat.setTexto(e.target.value)}
          readOnly={chat.processando}
          aria-describedby="chat-contador"
          aria-invalid={excedeu}
          rows={3}
        />
        <span id="chat-contador" data-excedeu={excedeu}>
          {chat.texto.length}/{LIMITES.mensagemMax}
        </span>
        <button type="submit" disabled={chat.processando}>
          Enviar
        </button>
      </form>
    </section>
  );
}
