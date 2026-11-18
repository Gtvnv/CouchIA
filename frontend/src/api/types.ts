// Tipos dos contratos da API (design.md, "Contratos da API" e "Data Models").
// JSON em camelCase; enums como strings.

export type CategoriaEntrevista =
  | 'interesses'
  | 'experienciaPrevia'
  | 'formacao'
  | 'estiloDeTrabalho'
  | 'disponibilidadeSemanal'
  | 'objetivos';

export const CATEGORIAS_ENTREVISTA: readonly CategoriaEntrevista[] = [
  'interesses',
  'experienciaPrevia',
  'formacao',
  'estiloDeTrabalho',
  'disponibilidadeSemanal',
  'objetivos',
];

export type EstadoCategoria = 'pendente' | 'respondida' | 'naoRespondida';
export type EstadoEntrevista = 'naoIniciada' | 'emAndamento' | 'concluida' | 'encerrada';
export type PrioridadeHabilidade = 'essencial' | 'importante' | 'desejavel';
export type TipoFeedback = 'util' | 'naoUtil';
export type PapelMensagem = 'user' | 'assistant';
export type FormatoExportacao = 'pdf' | 'md';

export type AreaId = string;
export type HabilidadeId = string;

export const LIMITES = {
  mensagemMax: 2000,
  motivoFeedbackMax: 500,
  justificativaMax: 500,
  horasPorSemanaMin: 1,
  horasPorSemanaMax: 168,
} as const;

export interface MensagemSessao {
  clientMessageId: string;
  ordem: number;
  papel: PapelMensagem;
  texto: string;
  criadaEm: string;
}

export interface PerfilUsuario {
  interesses: string[];
  experienciaPrevia: string | null;
  formacao: string | null;
  estiloDeTrabalho: string | null;
  horasEstudoPorSemana: number | null;
  objetivos: string | null;
  habilidadesDominadas: HabilidadeId[];
}

export interface Entrevista {
  estado: EstadoEntrevista;
  categorias: Record<CategoriaEntrevista, EstadoCategoria>;
  reformulacoes: Record<CategoriaEntrevista, number>;
}

export interface RecursoEstudo {
  titulo: string;
  url: string;
}

export interface RecomendacaoArea {
  areaId: AreaId;
  nome?: string;
  pontuacao: number;
  justificativa: string;
}

export interface FeedbackRecomendacao {
  areaId: AreaId;
  tipo: TipoFeedback;
  motivo: string | null;
  em: string;
}

export interface EtapaPlano {
  numero: number;
  habilidades: HabilidadeId[];
  duracaoSemanas: number;
  recursos: RecursoEstudo[];
}

export interface PlanoDesenvolvimento {
  id: string;
  areaId: AreaId;
  horasPorSemana: number;
  etapas: EtapaPlano[];
  duracaoTotalSemanas: number;
}

export interface Sessao {
  id?: string;
  criadaEm: string;
  historico: MensagemSessao[];
  perfil: PerfilUsuario;
  entrevista: Entrevista;
  recomendacoes: RecomendacaoArea[];
  planos: PlanoDesenvolvimento[];
  feedbacks: FeedbackRecomendacao[];
  baixaConfianca?: boolean;
}

export interface HabilidadeMapa {
  id: HabilidadeId;
  nome: string;
  descricao: string;
  prioridade: PrioridadeHabilidade;
  dominada: boolean;
}

export interface MapaHabilidades {
  areaId: AreaId;
  habilidades: HabilidadeMapa[];
}

export interface MensagemEnviarRequest {
  clientMessageId: string;
  texto: string;
}

export interface FeedbackRegistrarRequest {
  tipo: TipoFeedback;
  motivo?: string | null;
}

export interface PlanoGerarRequest {
  areaId: AreaId;
  /** Disponibilidade informada neste pedido (1 a 168); não altera o perfil. */
  horasPorSemana?: number;
}

export interface SessaoExcluirRequest {
  confirmar: true;
}

export interface EntrevistaEncerrarResponse {
  categoriasSemResposta: CategoriaEntrevista[];
}

// Eventos SSE da resposta do assistente.
export type EventoMensagem =
  | { tipo: 'trecho'; texto: string }
  | { tipo: 'final'; mensagem: MensagemSessao; entrevista: Entrevista; perfil?: PerfilUsuario }
  | { tipo: 'erro'; mensagem: string };

export interface ValidationFailure {
  [campo: string]: string[];
}

// ProblemDetails (RFC 7807) devolvido pela API, com erros do ValidationBehaviour.
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: ValidationFailure;
}
