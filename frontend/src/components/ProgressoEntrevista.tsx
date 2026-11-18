import { CATEGORIAS_ENTREVISTA } from '../api/types';
import type { CategoriaEntrevista, Entrevista, EstadoCategoria } from '../api/types';

const ROTULOS: Record<CategoriaEntrevista, string> = {
  interesses: 'Interesses',
  experienciaPrevia: 'Experiência prévia',
  formacao: 'Formação',
  estiloDeTrabalho: 'Estilo de trabalho',
  disponibilidadeSemanal: 'Disponibilidade semanal',
  objetivos: 'Objetivos profissionais',
};

const ESTADOS: Record<EstadoCategoria, string> = {
  pendente: 'pendente',
  respondida: 'respondida',
  naoRespondida: 'não respondida',
};

export function ProgressoEntrevista({ entrevista }: { entrevista: Entrevista }) {
  const concluidas = CATEGORIAS_ENTREVISTA.filter((c) => entrevista.categorias[c] !== 'pendente').length;
  return (
    <nav aria-label="Progresso da entrevista">
      <p>
        {concluidas} de {CATEGORIAS_ENTREVISTA.length} categorias concluídas
      </p>
      <ol>
        {CATEGORIAS_ENTREVISTA.map((c) => (
          <li key={c} data-estado={entrevista.categorias[c]}>
            {ROTULOS[c]}: {ESTADOS[entrevista.categorias[c]]}
          </li>
        ))}
      </ol>
    </nav>
  );
}
