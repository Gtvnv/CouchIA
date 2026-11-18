export const TEXTO_AVISO_IA =
  'Estas sugestões são geradas por inteligência artificial e não substituem orientação profissional.';

/** Aviso fixo exibido nas telas de recomendações, mapa de habilidades e plano (Req. 8.3). */
export function AvisoIA() {
  return (
    <aside role="note" aria-label="Aviso sobre conteúdo gerado por IA" data-testid="aviso-ia">
      <p>{TEXTO_AVISO_IA}</p>
    </aside>
  );
}
