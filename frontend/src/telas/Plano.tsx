import type { PlanoDesenvolvimento } from '../api/types';
import { AvisoIA } from '../components/AvisoIA';
import { DisponibilidadeForm } from '../components/DisponibilidadeForm';
import { PlanoView } from '../components/PlanoView';
import type { PlanoViewProps } from '../components/PlanoView';

export interface PlanoTelaProps extends Omit<PlanoViewProps, 'plano'> {
  plano: PlanoDesenvolvimento | null;
  /** Quando verdadeiro, pede a disponibilidade semanal antes de gerar o plano (Req. 5.6). */
  precisaDisponibilidade?: boolean;
  onInformarDisponibilidade?: (horasPorSemana: number) => void;
  /** Todas as habilidades já dominadas: informa em vez de gerar plano vazio (Req. 5.7). */
  semHabilidadesPendentes?: boolean;
  gerando?: boolean;
  /** Erro de geração (Req. 5.8). */
  erro?: string | null;
  onTentarNovamente?: () => void;
  onVoltar?: () => void;
}

export function Plano({
  plano,
  precisaDisponibilidade,
  onInformarDisponibilidade,
  semHabilidadesPendentes,
  gerando,
  erro,
  onTentarNovamente,
  onVoltar,
  ...view
}: PlanoTelaProps) {
  return (
    <section aria-labelledby="plano-titulo">
      <h2 id="plano-titulo">Plano de desenvolvimento</h2>
      {onVoltar && (
        <button type="button" onClick={onVoltar}>
          Voltar
        </button>
      )}
      {gerando && <p role="status">Gerando o plano…</p>}
      {erro ? (
        <div role="alert">
          <p>{erro}</p>
          {onTentarNovamente && (
            <button type="button" onClick={onTentarNovamente}>
              Tentar novamente
            </button>
          )}
        </div>
      ) : (
        <>
          <AvisoIA />
          {precisaDisponibilidade && onInformarDisponibilidade && (
            <div>
              <p>Para gerar o plano, informe quantas horas por semana você pode estudar.</p>
              <DisponibilidadeForm onSubmit={onInformarDisponibilidade} desabilitado={gerando} />
            </div>
          )}
          {semHabilidadesPendentes && (
            <p role="status">Não há habilidades pendentes: você já domina todas as habilidades desta área.</p>
          )}
          {plano && <PlanoView plano={plano} {...view} />}
        </>
      )}
    </section>
  );
}
