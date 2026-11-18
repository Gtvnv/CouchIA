export interface BoasVindasProps {
  onIniciar: () => void;
  iniciando?: boolean;
}

export function BoasVindas({ onIniciar, iniciando = false }: BoasVindasProps) {
  return (
    <section aria-labelledby="boas-vindas-titulo">
      <h2 id="boas-vindas-titulo">Bem-vindo(a) ao IA Mentor de Carreira</h2>
      <p>
        Vou conversar com você sobre seus interesses e experiências para sugerir áreas de atuação em
        tecnologia e montar um plano de estudos.
      </p>
      <button type="button" onClick={onIniciar} disabled={iniciando}>
        Iniciar entrevista
      </button>
    </section>
  );
}
