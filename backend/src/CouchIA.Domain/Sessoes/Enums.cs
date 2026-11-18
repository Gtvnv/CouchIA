namespace CouchIA.Domain;

public enum CategoriaEntrevista { Interesses, ExperienciaPrevia, Formacao, EstiloDeTrabalho, DisponibilidadeSemanal, Objetivos }

public enum EstadoCategoria { Pendente, Respondida, NaoRespondida }

public enum EstadoEntrevista { NaoIniciada, EmAndamento, Concluida, Encerrada }

public enum TipoFeedback { Util, NaoUtil }

public enum PapelMensagem { Usuario, Mentor }
