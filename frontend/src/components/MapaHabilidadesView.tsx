import type { MapaHabilidades, PrioridadeHabilidade } from '../api/types';

const ROTULOS: Record<PrioridadeHabilidade, string> = {
  essencial: 'Essenciais',
  importante: 'Importantes',
  desejavel: 'Desejáveis',
};

const ORDEM: readonly PrioridadeHabilidade[] = ['essencial', 'importante', 'desejavel'];

export interface MapaHabilidadesViewProps {
  mapa: MapaHabilidades;
  nomeArea?: string;
}

/** Mapa_de_Habilidades agrupado por prioridade, mantendo a ordem recebida dentro de cada nível (Req. 4.2 a 4.4). */
export function MapaHabilidadesView({ mapa, nomeArea }: MapaHabilidadesViewProps) {
  return (
    <div>
      <h3>Habilidades{nomeArea ? ` de ${nomeArea}` : ''}</h3>
      {ORDEM.map((prioridade) => {
        const itens = mapa.habilidades.filter((h) => h.prioridade === prioridade);
        if (itens.length === 0) return null;
        return (
          <section key={prioridade} aria-label={`Habilidades ${ROTULOS[prioridade].toLowerCase()}`}>
            <h4>{ROTULOS[prioridade]}</h4>
            <ul>
              {itens.map((h) => (
                <li key={h.id} data-dominada={h.dominada}>
                  <strong>{h.nome}</strong>
                  {h.dominada && <span> (já dominada)</span>}
                  <p>{h.descricao}</p>
                </li>
              ))}
            </ul>
          </section>
        );
      })}
    </div>
  );
}
