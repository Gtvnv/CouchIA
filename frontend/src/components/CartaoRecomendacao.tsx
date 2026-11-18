import { useId, useState } from 'react';
import type { FormEvent } from 'react';
import { LIMITES } from '../api/types';
import type {
  FeedbackRecomendacao,
  FeedbackRegistrarRequest,
  RecomendacaoArea,
  TipoFeedback,
} from '../api/types';

export interface CartaoRecomendacaoProps {
  recomendacao: RecomendacaoArea;
  /** Feedback ativo para a área (no máximo um por área). */
  feedback?: FeedbackRecomendacao | null;
  /** Registra o feedback; deve rejeitar a promise em caso de falha. */
  onFeedback: (areaId: string, req: FeedbackRegistrarRequest) => Promise<void>;
  onVerMapa?: (areaId: string) => void;
  onGerarPlano?: (areaId: string) => void;
}

/** Cartão de uma Recomendacao_de_Area com pontuação, justificativa e feedback (Req. 3.2, 9.1 a 9.3, 9.6, 9.7). */
export function CartaoRecomendacao({
  recomendacao,
  feedback,
  onFeedback,
  onVerMapa,
  onGerarPlano,
}: CartaoRecomendacaoProps) {
  const uid = useId();
  const [pedindoMotivo, setPedindoMotivo] = useState(false);
  const [motivo, setMotivo] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [ultimo, setUltimo] = useState<FeedbackRegistrarRequest | null>(null);
  const [confirmacao, setConfirmacao] = useState<string | null>(null);

  const nome = recomendacao.nome ?? recomendacao.areaId;
  const excedeu = motivo.length > LIMITES.motivoFeedbackMax;
  const ativo: TipoFeedback | undefined = feedback?.tipo;

  async function enviar(req: FeedbackRegistrarRequest) {
    setEnviando(true);
    setErro(null);
    setConfirmacao(null);
    setUltimo(req);
    try {
      await onFeedback(recomendacao.areaId, req);
      setConfirmacao('Feedback registrado. Obrigado!');
      setPedindoMotivo(false);
      setUltimo(null);
    } catch {
      setErro('Não foi possível registrar o feedback. Tente novamente.');
    } finally {
      setEnviando(false);
    }
  }

  function aoMarcarUtil() {
    setPedindoMotivo(false);
    void enviar({ tipo: 'util' });
  }

  function aoMarcarNaoUtil() {
    setErro(null);
    setConfirmacao(null);
    setPedindoMotivo(true);
  }

  function aoSubmeterMotivo(e: FormEvent) {
    e.preventDefault();
    if (excedeu) return; // texto preservado; o erro de limite já está visível
    const texto = motivo.trim();
    void enviar({ tipo: 'naoUtil', motivo: texto ? texto : null });
  }

  return (
    <article aria-labelledby={`${uid}-titulo`} data-area-id={recomendacao.areaId}>
      <h3 id={`${uid}-titulo`}>{nome}</h3>
      <p>
        Aderência: <strong>{recomendacao.pontuacao}</strong> de 100
      </p>
      <p>{recomendacao.justificativa}</p>

      <div role="group" aria-label={`Feedback sobre ${nome}`}>
        <button type="button" aria-pressed={ativo === 'util'} onClick={aoMarcarUtil} disabled={enviando}>
          Útil
        </button>
        <button
          type="button"
          aria-pressed={ativo === 'naoUtil'}
          onClick={aoMarcarNaoUtil}
          disabled={enviando}
        >
          Não útil
        </button>
      </div>

      {pedindoMotivo && (
        <form onSubmit={aoSubmeterMotivo}>
          <label htmlFor={`${uid}-motivo`}>Motivo (opcional)</label>
          <textarea
            id={`${uid}-motivo`}
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            rows={3}
            aria-invalid={excedeu}
            aria-describedby={`${uid}-contador${excedeu ? ` ${uid}-limite` : ''}`}
          />
          <span id={`${uid}-contador`}>
            {motivo.length}/{LIMITES.motivoFeedbackMax}
          </span>
          {excedeu && (
            <p id={`${uid}-limite`} role="alert">
              O motivo deve ter no máximo {LIMITES.motivoFeedbackMax} caracteres.
            </p>
          )}
          <button type="submit" disabled={enviando}>
            Enviar feedback
          </button>
        </form>
      )}

      {confirmacao && <p role="status">{confirmacao}</p>}
      {erro && (
        <div role="alert">
          <p>{erro}</p>
          {ultimo && (
            <button type="button" onClick={() => void enviar(ultimo)} disabled={enviando}>
              Tentar novamente
            </button>
          )}
        </div>
      )}

      {(onVerMapa || onGerarPlano) && (
        <div>
          {onVerMapa && (
            <button type="button" onClick={() => onVerMapa(recomendacao.areaId)}>
              Ver mapa de habilidades
            </button>
          )}
          {onGerarPlano && (
            <button type="button" onClick={() => onGerarPlano(recomendacao.areaId)}>
              Gerar plano de desenvolvimento
            </button>
          )}
        </div>
      )}
    </article>
  );
}
