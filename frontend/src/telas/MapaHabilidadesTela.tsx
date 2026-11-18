import type { MapaHabilidades } from '../api/types';
import { AvisoIA } from '../components/AvisoIA';
import { MapaHabilidadesView } from '../components/MapaHabilidadesView';

export interface MapaHabilidadesTelaProps {
  mapa: MapaHabilidades | null;
  nomeArea?: string;
  carregando?: boolean;
  /** Erro de carregamento: não exibe mapa parcial (Req. 4.6). */
  erro?: string | null;
  onTentarNovamente?: () => void;
  onGerarPlano?: (areaId: string) => void;
  onVoltar?: () => void;
}

export function MapaHabilidadesTela({
  mapa,
  nomeArea,
  carregando,
  erro,
  onTentarNovamente,
  onGerarPlano,
  onVoltar,
}: MapaHabilidadesTelaProps) {
  return (
    <section aria-labelledby="mapa-titulo">
      <h2 id="mapa-titulo">Mapa de habilidades</h2>
      {onVoltar && (
        <button type="button" onClick={onVoltar}>
          Voltar às recomendações
        </button>
      )}
      {carregando && <p role="status">Carregando o mapa de habilidades…</p>}
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
        mapa && (
          <>
            <AvisoIA />
            <MapaHabilidadesView mapa={mapa} nomeArea={nomeArea} />
            {onGerarPlano && (
              <button type="button" onClick={() => onGerarPlano(mapa.areaId)}>
                Gerar plano de desenvolvimento
              </button>
            )}
          </>
        )
      )}
    </section>
  );
}
