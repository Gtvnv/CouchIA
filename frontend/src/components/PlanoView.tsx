import type { FormatoExportacao, PlanoDesenvolvimento } from '../api/types';

export interface PlanoViewProps {
  plano: PlanoDesenvolvimento;
  /** Mapa id -> nome das habilidades, para exibição. Sem ele, mostra o id. */
  nomesHabilidades?: Record<string, string>;
  /** Avisos exibidos acima das etapas (ex.: sem habilidades pendentes). */
  avisos?: string[];
  onExportar?: (formato: FormatoExportacao) => void;
  exportando?: boolean;
  erroExportacao?: string | null;
  onTentarExportarNovamente?: () => void;
}

/** Plano_de_Desenvolvimento em etapas, com exportação PDF/Markdown (Req. 5.1, 5.5, 5.8). */
export function PlanoView({
  plano,
  nomesHabilidades = {},
  avisos = [],
  onExportar,
  exportando,
  erroExportacao,
  onTentarExportarNovamente,
}: PlanoViewProps) {
  return (
    <div>
      <h3>Plano de desenvolvimento</h3>
      <p>
        {plano.horasPorSemana} h/semana · duração total: {plano.duracaoTotalSemanas}{' '}
        {plano.duracaoTotalSemanas === 1 ? 'semana' : 'semanas'}
      </p>

      {avisos.length > 0 && (
        <ul aria-label="Avisos do plano" role="list">
          {avisos.map((a) => (
            <li key={a} role="status">
              {a}
            </li>
          ))}
        </ul>
      )}

      <ol aria-label="Etapas do plano">
        {plano.etapas.map((etapa) => (
          <li key={etapa.numero}>
            <h4>
              Etapa {etapa.numero} ({etapa.duracaoSemanas} {etapa.duracaoSemanas === 1 ? 'semana' : 'semanas'})
            </h4>
            <p>Habilidades:</p>
            <ul>
              {etapa.habilidades.map((h) => (
                <li key={h}>{nomesHabilidades[h] ?? h}</li>
              ))}
            </ul>
            <p>Recursos de estudo:</p>
            <ul>
              {etapa.recursos.map((r) => (
                <li key={r.url}>
                  <a href={r.url} target="_blank" rel="noopener noreferrer">
                    {r.titulo}
                  </a>
                </li>
              ))}
            </ul>
          </li>
        ))}
      </ol>

      {onExportar && (
        <div role="group" aria-label="Exportar plano">
          <button type="button" onClick={() => onExportar('pdf')} disabled={exportando}>
            Exportar PDF
          </button>
          <button type="button" onClick={() => onExportar('md')} disabled={exportando}>
            Exportar Markdown
          </button>
        </div>
      )}
      {exportando && <p role="status">Gerando arquivo…</p>}
      {erroExportacao && (
        <div role="alert">
          <p>{erroExportacao}</p>
          {onTentarExportarNovamente && (
            <button type="button" onClick={onTentarExportarNovamente}>
              Tentar novamente
            </button>
          )}
        </div>
      )}
    </div>
  );
}
