import type { FeedbackRecomendacao, RecomendacaoArea } from '../api/types';
import { AvisoIA } from '../components/AvisoIA';
import { CartaoRecomendacao } from '../components/CartaoRecomendacao';
import type { CartaoRecomendacaoProps } from '../components/CartaoRecomendacao';

export interface RecomendacoesProps extends Pick<CartaoRecomendacaoProps, 'onFeedback' | 'onVerMapa' | 'onGerarPlano'> {
  recomendacoes: RecomendacaoArea[];
  feedbacks?: FeedbackRecomendacao[];
  /** Verdadeiro quando o perfil tem menos de três categorias respondidas (Req. 3.6). */
  baixaConfianca?: boolean;
  onContinuarEntrevista?: () => void;
  /** Erro de geração: substitui totalmente a tela, sem lista nem aviso de IA (Req. 3.7, 8.5). */
  erro?: string | null;
  onTentarNovamente?: () => void;
}

export function Recomendacoes({
  recomendacoes,
  feedbacks = [],
  baixaConfianca,
  onContinuarEntrevista,
  erro,
  onTentarNovamente,
  ...cartao
}: RecomendacoesProps) {
  if (erro) {
    return (
      <section aria-labelledby="recomendacoes-titulo">
        <h2 id="recomendacoes-titulo">Recomendações</h2>
        <div role="alert">
          <p>{erro}</p>
          {onTentarNovamente && (
            <button type="button" onClick={onTentarNovamente}>
              Tentar novamente
            </button>
          )}
        </div>
      </section>
    );
  }

  return (
    <section aria-labelledby="recomendacoes-titulo">
      <h2 id="recomendacoes-titulo">Recomendações</h2>
      <AvisoIA />
      {baixaConfianca && (
        <div role="status" data-testid="aviso-baixa-confianca">
          <p>
            Estas recomendações têm baixa confiança, pois seu perfil tem menos de três categorias
            respondidas. Continue a entrevista para obter sugestões mais precisas.
          </p>
          {onContinuarEntrevista && (
            <button type="button" onClick={onContinuarEntrevista}>
              Continuar entrevista
            </button>
          )}
        </div>
      )}
      <ol aria-label="Áreas recomendadas">
        {recomendacoes.map((r) => (
          <li key={r.areaId}>
            <CartaoRecomendacao
              recomendacao={r}
              feedback={feedbacks.find((f) => f.areaId === r.areaId) ?? null}
              {...cartao}
            />
          </li>
        ))}
      </ol>
    </section>
  );
}
