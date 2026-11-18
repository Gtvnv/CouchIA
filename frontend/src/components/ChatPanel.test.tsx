import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { EventoMensagem, MensagemEnviarRequest } from '../api/types';
import { ApiError } from '../api/client';
import { ChatPanel } from './ChatPanel';
import { ProgressoEntrevista } from './ProgressoEntrevista';
import type { Entrevista } from '../api/types';

const final: EventoMensagem = {
  tipo: 'final',
  mensagem: { clientMessageId: 'a1', ordem: 2, papel: 'assistant', texto: 'Olá!', criadaEm: '' },
  entrevista: {} as Entrevista,
};

async function* fluxo(...eventos: EventoMensagem[]) {
  for (const e of eventos) yield e;
}

afterEach(() => vi.useRealTimers());

describe('ChatPanel', () => {
  it('rejeita mensagem vazia, mostra o limite e mantém o texto', async () => {
    const enviar = vi.fn();
    render(<ChatPanel enviarMensagem={enviar} />);
    await userEvent.type(screen.getByLabelText('Sua mensagem'), '   ');
    await userEvent.click(screen.getByRole('button', { name: 'Enviar' }));
    expect(screen.getByRole('alert')).toHaveTextContent('1 a 2.000 caracteres');
    expect(screen.getByLabelText('Sua mensagem')).toHaveValue('   ');
    expect(enviar).not.toHaveBeenCalled();
  });

  it('rejeita mais de 2.000 caracteres preservando o texto e exibe o contador', async () => {
    const enviar = vi.fn();
    render(<ChatPanel enviarMensagem={enviar} />);
    const campo = screen.getByLabelText('Sua mensagem');
    await userEvent.click(campo);
    await userEvent.paste('x'.repeat(2001));
    expect(screen.getByText('2001/2000')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Enviar' }));
    expect(screen.getByRole('alert')).toHaveTextContent('1 a 2.000 caracteres');
    expect(campo).toHaveValue('x'.repeat(2001));
    expect(enviar).not.toHaveBeenCalled();
  });

  it('mostra indicador e bloqueia o envio enquanto processa; limpa ao concluir', async () => {
    let liberar!: () => void;
    const enviar = (_r: MensagemEnviarRequest) =>
      (async function* () {
        await new Promise<void>((r) => (liberar = r));
        yield final;
      })();
    render(<ChatPanel enviarMensagem={enviar} />);
    await userEvent.type(screen.getByLabelText('Sua mensagem'), 'Oi');
    await userEvent.click(screen.getByRole('button', { name: 'Enviar' }));
    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar' })).toBeDisabled();
    await act(async () => liberar());
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.getByText('Olá!')).toBeInTheDocument();
    expect(screen.getByLabelText('Sua mensagem')).toHaveValue('');
  });

  it('preserva o texto quando a API rejeita o envio', async () => {
    const enviar = () =>
      // eslint-disable-next-line require-yield
      (async function* (): AsyncGenerator<EventoMensagem> {
        throw new ApiError(400, 'Mensagem inválida');
      })();
    render(<ChatPanel enviarMensagem={enviar} />);
    await userEvent.type(screen.getByLabelText('Sua mensagem'), 'Oi');
    await userEvent.click(screen.getByRole('button', { name: 'Enviar' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Mensagem inválida');
    expect(screen.getByLabelText('Sua mensagem')).toHaveValue('Oi');
    expect(screen.queryByRole('button', { name: 'Reenviar mensagem' })).not.toBeInTheDocument();
  });

  it('aos 30 s sem primeiro trecho exibe erro e reenvia a mesma mensagem sem duplicar', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const chamadas: string[] = [];
    let n = 0;
    const enviar = (req: MensagemEnviarRequest) => {
      chamadas.push(req.clientMessageId);
      n++;
      return n === 1
        ? (async function* (): AsyncGenerator<EventoMensagem> {
            await new Promise(() => {});
          })()
        : fluxo(final);
    };
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<ChatPanel enviarMensagem={enviar} />);
    await user.type(screen.getByLabelText('Sua mensagem'), 'Oi');
    await user.click(screen.getByRole('button', { name: 'Enviar' }));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000);
    });
    expect(screen.getByRole('alert')).toHaveTextContent('demorou');
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.getAllByText(/Oi/)).toHaveLength(1);

    await user.click(screen.getByRole('button', { name: 'Reenviar mensagem' }));
    expect(chamadas[1]).toBe(chamadas[0]);
    expect(await screen.findByText('Olá!')).toBeInTheDocument();
    expect(screen.getAllByText(/Oi/)).toHaveLength(1);
  });
});

describe('ProgressoEntrevista', () => {
  it('lista as seis categorias com seus estados', () => {
    const entrevista: Entrevista = {
      estado: 'emAndamento',
      categorias: {
        interesses: 'respondida',
        experienciaPrevia: 'naoRespondida',
        formacao: 'pendente',
        estiloDeTrabalho: 'pendente',
        disponibilidadeSemanal: 'pendente',
        objetivos: 'pendente',
      },
      reformulacoes: {
        interesses: 0,
        experienciaPrevia: 1,
        formacao: 0,
        estiloDeTrabalho: 0,
        disponibilidadeSemanal: 0,
        objetivos: 0,
      },
    };
    render(<ProgressoEntrevista entrevista={entrevista} />);
    expect(screen.getAllByRole('listitem')).toHaveLength(6);
    expect(screen.getByText('2 de 6 categorias concluídas')).toBeInTheDocument();
    expect(screen.getByText('Experiência prévia: não respondida')).toBeInTheDocument();
  });
});
