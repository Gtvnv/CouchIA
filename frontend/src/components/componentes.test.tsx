import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import fc from 'fast-check';
import { describe, expect, it, vi } from 'vitest';
import type { MapaHabilidades, PlanoDesenvolvimento, RecomendacaoArea } from '../api/types';
import { validarMensagem } from '../hooks/useMensagemEnvio';
import { MapaHabilidadesTela } from '../telas/MapaHabilidadesTela';
import { Plano } from '../telas/Plano';
import { Recomendacoes } from '../telas/Recomendacoes';
import { CartaoRecomendacao } from './CartaoRecomendacao';
import { DisponibilidadeForm } from './DisponibilidadeForm';
import { ExclusaoDadosConfirmacao } from './ExclusaoDadosConfirmacao';

const rec: RecomendacaoArea = {
  areaId: 'dados',
  nome: 'Engenharia de Dados',
  pontuacao: 90,
  justificativa: 'Você gosta de dados.',
};

const mapa: MapaHabilidades = {
  areaId: 'dados',
  habilidades: [
    { id: 'sql', nome: 'SQL', descricao: 'Consultas', prioridade: 'essencial', dominada: true },
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

describe('validarMensagem', () => {
  // Feature: ia-mentor-carreira, Property 1: Validação do tamanho e do conteúdo da mensagem
  // **Validates: Requirements 1.6**
  it('aceita se e somente se trim não é vazio e o tamanho é no máximo 2000', () => {
    fc.assert(
      fc.property(fc.string({ unit: 'binary', maxLength: 2100 }), (s) => {
        expect(validarMensagem(s)).toBe(s.trim().length > 0 && s.length <= 2000);
      }),
      { numRuns: 200 },
    );
  });

  it('trata os limites 0, 1, 2000 e 2001', () => {
    expect(validarMensagem('')).toBe(false);
    expect(validarMensagem('a')).toBe(true);
    expect(validarMensagem('a'.repeat(2000))).toBe(true);
    expect(validarMensagem('a'.repeat(2001))).toBe(false);
  });
});

describe('AvisoIA nas telas (Req. 8.3, 8.5)', () => {
  it('aparece em recomendações, mapa e plano', () => {
    const onFeedback = vi.fn();
    const { unmount: u1 } = render(<Recomendacoes recomendacoes={[rec]} onFeedback={onFeedback} />);
    expect(screen.getByTestId('aviso-ia')).toBeInTheDocument();
    u1();
    const { unmount: u2 } = render(<MapaHabilidadesTela mapa={mapa} />);
    expect(screen.getByTestId('aviso-ia')).toBeInTheDocument();
    u2();
    render(<Plano plano={plano} />);
    expect(screen.getByTestId('aviso-ia')).toBeInTheDocument();
  });

  it('não aparece nos fluxos de erro', () => {
    const { unmount: u1 } = render(
      <Recomendacoes recomendacoes={[]} onFeedback={vi.fn()} erro="Falha" />,
    );
    expect(screen.queryByTestId('aviso-ia')).not.toBeInTheDocument();
    u1();
    const { unmount: u2 } = render(<MapaHabilidadesTela mapa={null} erro="Falha" />);
    expect(screen.queryByTestId('aviso-ia')).not.toBeInTheDocument();
    u2();
    render(<Plano plano={null} erro="Falha" />);
    expect(screen.queryByTestId('aviso-ia')).not.toBeInTheDocument();
  });
});

describe('Recomendacoes', () => {
  it('exibe o aviso de baixa confiança só quando indicado (Req. 3.6)', () => {
    const { rerender } = render(
      <Recomendacoes recomendacoes={[rec]} onFeedback={vi.fn()} baixaConfianca />,
    );
    expect(screen.getByTestId('aviso-baixa-confianca')).toBeInTheDocument();
    rerender(<Recomendacoes recomendacoes={[rec]} onFeedback={vi.fn()} baixaConfianca={false} />);
    expect(screen.queryByTestId('aviso-baixa-confianca')).not.toBeInTheDocument();
  });

  it('em erro não exibe a lista e permite tentar novamente (Req. 3.7)', async () => {
    const tentar = vi.fn();
    render(<Recomendacoes recomendacoes={[rec]} onFeedback={vi.fn()} erro="Falha" onTentarNovamente={tentar} />);
    expect(screen.queryByRole('list', { name: 'Áreas recomendadas' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    expect(tentar).toHaveBeenCalledOnce();
  });
});

describe('MapaHabilidadesTela', () => {
  it('agrupa por prioridade e marca habilidades dominadas (Req. 4.3, 4.4)', () => {
    render(<MapaHabilidadesTela mapa={mapa} />);
    expect(screen.getByRole('heading', { name: 'Essenciais' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Importantes' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Desejáveis' })).not.toBeInTheDocument();
    expect(screen.getByText('(já dominada)')).toBeInTheDocument();
  });

  it('em erro não exibe mapa parcial (Req. 4.6)', () => {
    render(<MapaHabilidadesTela mapa={mapa} erro="Catálogo indisponível" />);
    expect(screen.getByRole('alert')).toHaveTextContent('Catálogo indisponível');
    expect(screen.queryByText('SQL')).not.toBeInTheDocument();
  });
});

describe('Plano', () => {
  it('exibe etapas, durações, recursos e botões de exportação (Req. 5.1, 5.5)', async () => {
    const onExportar = vi.fn();
    render(<Plano plano={plano} nomesHabilidades={{ sql: 'SQL' }} onExportar={onExportar} />);
    expect(screen.getByText('Etapa 1 (4 semanas)')).toBeInTheDocument();
    expect(screen.getByText('SQL')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Curso de SQL' })).toHaveAttribute('href', 'https://exemplo.com/sql');
    await userEvent.click(screen.getByRole('button', { name: 'Exportar PDF' }));
    expect(onExportar).toHaveBeenCalledWith('pdf');
  });

  it('falha na exportação exibe erro e permite tentar novamente (Req. 5.8)', async () => {
    const tentar = vi.fn();
    render(<Plano plano={plano} erroExportacao="Falha na exportação" onTentarExportarNovamente={tentar} />);
    expect(screen.getByRole('alert')).toHaveTextContent('Falha na exportação');
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    expect(tentar).toHaveBeenCalledOnce();
  });
});

describe('CartaoRecomendacao: feedback (Req. 9.1, 9.3, 9.6)', () => {
  it('"não útil" pede motivo e envia com o motivo informado', async () => {
    const onFeedback = vi.fn().mockResolvedValue(undefined);
    render(<CartaoRecomendacao recomendacao={rec} onFeedback={onFeedback} />);
    await userEvent.click(screen.getByRole('button', { name: 'Não útil' }));
    await userEvent.type(screen.getByLabelText('Motivo (opcional)'), 'Não me interessa');
    await userEvent.click(screen.getByRole('button', { name: 'Enviar feedback' }));
    expect(onFeedback).toHaveBeenCalledWith('dados', { tipo: 'naoUtil', motivo: 'Não me interessa' });
    expect(await screen.findByRole('status')).toHaveTextContent('Feedback registrado');
  });

  it('aceita motivo de 500 caracteres e recusa 501 preservando o texto', async () => {
    const onFeedback = vi.fn().mockResolvedValue(undefined);
    render(<CartaoRecomendacao recomendacao={rec} onFeedback={onFeedback} />);
    await userEvent.click(screen.getByRole('button', { name: 'Não útil' }));
    const campo = screen.getByLabelText('Motivo (opcional)');
    await userEvent.click(campo);
    await userEvent.paste('x'.repeat(501));
    expect(screen.getByText('501/500')).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('no máximo 500');
    await userEvent.click(screen.getByRole('button', { name: 'Enviar feedback' }));
    expect(onFeedback).not.toHaveBeenCalled();
    expect(campo).toHaveValue('x'.repeat(501));

    await userEvent.clear(campo);
    await userEvent.paste('x'.repeat(500));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Enviar feedback' }));
    expect(onFeedback).toHaveBeenCalledOnce();
  });

  it('falha ao registrar mostra erro e permite tentar novamente (Req. 9.7)', async () => {
    const onFeedback = vi.fn().mockRejectedValueOnce(new Error('x')).mockResolvedValue(undefined);
    render(<CartaoRecomendacao recomendacao={rec} onFeedback={onFeedback} />);
    await userEvent.click(screen.getByRole('button', { name: 'Útil' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível registrar');
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    expect(onFeedback).toHaveBeenCalledTimes(2);
    expect(await screen.findByRole('status')).toBeInTheDocument();
  });
});

describe('ExclusaoDadosConfirmacao (Req. 7.3)', () => {
  it('só exclui após confirmação e cancelar não exclui', async () => {
    const onConfirmar = vi.fn().mockResolvedValue(undefined);
    render(<ExclusaoDadosConfirmacao onConfirmar={onConfirmar} />);
    await userEvent.click(screen.getByRole('button', { name: 'Excluir meus dados' }));
    expect(onConfirmar).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
    expect(onConfirmar).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Excluir meus dados' })).toBeInTheDocument();
  });

  it('falha na exclusão exibe erro e mantém a confirmação', async () => {
    const onConfirmar = vi.fn().mockRejectedValue(new Error('x'));
    render(<ExclusaoDadosConfirmacao onConfirmar={onConfirmar} />);
    await userEvent.click(screen.getByRole('button', { name: 'Excluir meus dados' }));
    await userEvent.click(screen.getByRole('button', { name: 'Confirmar exclusão' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível excluir');
  });
});

describe('DisponibilidadeForm (Req. 5.6)', () => {
  it.each(['', '0', '169', '2.5', '-3'])('recusa o valor "%s" sem enviar', async (valor) => {
    const onSubmit = vi.fn();
    render(<DisponibilidadeForm onSubmit={onSubmit} />);
    if (valor) await userEvent.type(screen.getByLabelText('Horas de estudo por semana'), valor);
    await userEvent.click(screen.getByRole('button', { name: 'Gerar plano' }));
    expect(screen.getByRole('alert')).toHaveTextContent('1 a 168');
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it.each([1, 168])('aceita o limite %i', async (n) => {
    const onSubmit = vi.fn();
    render(<DisponibilidadeForm onSubmit={onSubmit} />);
    await userEvent.type(screen.getByLabelText('Horas de estudo por semana'), String(n));
    await userEvent.click(screen.getByRole('button', { name: 'Gerar plano' }));
    expect(onSubmit).toHaveBeenCalledWith(n);
  });
});
