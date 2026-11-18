# Implementation Plan: IA Mentor de Carreira

## Overview

O design define o código em C# (.NET 8, Clean Architecture) no backend e React + TypeScript no frontend, então não há pseudocódigo a traduzir. O plano começa pelo domínio e pelas regras determinísticas (catálogo, entrevista, recomendação, mapa, plano, feedback), que concentram as propriedades de correção. Em seguida entram as portas de IA, a persistência, os handlers MediatR, a API e o frontend. Cada etapa termina ligada à anterior pela fiação em `DependencyInjection.cs`, sem código órfão.

Convenções: testes de propriedade usam FsCheck (backend) com no mínimo 100 iterações (200 para catálogo e plano) e o comentário `Feature: ia-mentor-carreira, Property {n}: {título}`. Testes do frontend usam Vitest, Testing Library e Playwright com axe-core.

## Tasks

- [x] 1. Estrutura da solução e modelo de domínio
  - [x] 1.1 Criar a solução .NET 8 e os projetos
    - Criar `Domain`, `Application`, `Infrastructure`, `WebUI` e os projetos de teste (xUnit, FsCheck.Xunit, Testcontainers) com as referências na direção do centro
    - Adicionar pacotes: MediatR, FluentValidation, AutoMapper (com `MaxDepth` global), EF Core + Npgsql, QuestPDF
    - Criar `DependencyInjection.cs` vazio em `Application` e `Infrastructure`
    - _Requirements: 6.1, 7.6, 8.6_

  - [x] 1.2 Implementar entidades, value objects e enums do domínio
    - `Sessao`, `MensagemSessao`, `PerfilUsuario`, `Entrevista`, `RecomendacaoArea`, `FeedbackRecomendacao`, `PlanoDesenvolvimento`, `EtapaPlano`
    - `Catalogo`, `AreaAtuacao`, `HabilidadeNaArea`, `Habilidade`, `RecursoEstudo`, `SessaoId` (256 bits, Base64Url), `AreaId`, `HabilidadeId`
    - Enums `CategoriaEntrevista`, `EstadoCategoria`, `PrioridadeHabilidade`, `EstadoEntrevista`, `TipoFeedback`
    - _Requirements: 1.2, 2.1, 4.2, 5.1, 6.1, 6.2, 9.1_

- [x] 2. Catálogo de carreiras
  - [x] 2.1 Implementar `ValidadorCatalogo`
    - Validar limites, unicidade de nomes e identificadores, pré-requisitos existentes, sem auto-referência e sem ciclos
    - Retornar erros que identificam a Habilidade afetada e o tipo de falha
    - _Requirements: 4.5, 6.1, 6.2, 6.3, 6.4_

  - [x] 2.2 Write property test for validação do catálogo
    - **Property 15: Validação do catálogo**
    - Geradores `CatalogoValido` (DAG) e `CatalogoInvalido` (mutações)
    - **Validates: Requirements 4.5, 6.1, 6.2, 6.3, 6.4**

  - [x] 2.3 Implementar `CatalogoSerializer` e `CatalogoJsonProvider`
    - Serializar e carregar JSON UTF-8 com erros localizados (campo ou posição)
    - Definir `ICatalogoProvider` em `Application`; manter o último catálogo válido em memória e registrar log em falha
    - _Requirements: 6.3, 6.4, 6.5, 6.6, 6.7, 6.8_

  - [x] 2.4 Write property test for catálogo inválido preserva o ativo
    - **Property 16: Catálogo inválido preserva o catálogo ativo**
    - **Validates: Requirements 6.4, 6.7**

  - [x] 2.5 Write property test for round-trip do catálogo
    - **Property 17: Round-trip do catálogo**
    - **Validates: Requirements 6.5, 6.6, 6.8**

  - [x] 2.6 Write property test for JSON malformado ou incompleto
    - **Property 18: JSON malformado ou incompleto é recusado**
    - **Validates: Requirements 6.7**

  - [x] 2.7 Criar o arquivo `catalogo.json` inicial versionado
    - Incluir pelo menos 3 áreas, com Habilidades, prioridades, horas estimadas (1 a 500), 1 a 3 recursos e pré-requisitos
    - Carregar na inicialização e validar com o `ValidadorCatalogo`; subir em modo degradado se não houver catálogo válido
    - _Requirements: 3.3, 3.7, 4.1, 4.6, 6.1, 6.2_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Validação de mensagem e entrevista
  - [x] 4.1 Implementar `ValidadorMensagem` e `ConfiancaRecomendacao`
    - Mensagem aceita se `trim` não é vazio e o tamanho está entre 1 e 2000
    - `BaixaConfianca = categoriasRespondidas < 3`
    - _Requirements: 1.6, 2.8, 3.6_

  - [x] 4.2 Write property test for validação de mensagem
    - **Property 1: Validação do tamanho e do conteúdo da mensagem**
    - Strings Unicode, emojis, espaços e tamanhos próximos de 0, 1, 2000 e 2001
    - **Validates: Requirements 1.6, 2.8**

  - [x] 4.3 Write property test for aviso de baixa confiança
    - **Property 8: Aviso de baixa confiança**
    - **Validates: Requirements 3.6**

  - [x] 4.4 Implementar `MaquinaEntrevista`
    - Estados por categoria, transições, uma reformulação por categoria, conclusão e encerramento antecipado (recusado com 0 respondidas)
    - Aplicar extração utilizável ao `PerfilUsuario`, validando disponibilidade de 1 a 168 horas
    - _Requirements: 2.2, 2.4, 2.5, 2.6, 2.7, 5.6_

  - [x] 4.5 Write property test for conclusão e encerramento da entrevista
    - **Property 4: Conclusão e encerramento da entrevista**
    - **Validates: Requirements 2.4, 2.5, 2.7**

  - [x] 4.6 Write property test for reformulação única por categoria
    - **Property 5: Reformulação ocorre no máximo uma vez por categoria**
    - **Validates: Requirements 2.6**

  - [x] 4.7 Write property test for extração atualiza o perfil
    - **Property 6: Extração utilizável atualiza o perfil**
    - **Validates: Requirements 2.2**

- [x] 5. Recomendações e feedback
  - [x] 5.1 Implementar `ValidadorRecomendacoes`
    - Normalizar e validar a saída do LLM: 3 a 5 itens, sem repetição, só áreas do catálogo, pontuação inteira 0 a 100, justificativa de 1 a 500 caracteres citando item do perfil, ordem decrescente
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 5.2 Write property test for recomendações válidas
    - **Property 7: Recomendações válidas**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4**

  - [x] 5.3 Implementar `FeedbackService`
    - Um feedback ativo por área (o último substitui), validação do motivo (0 a 500), ordem de exibição que rebaixa áreas "não útil" sem alterar pontuações
    - _Requirements: 9.1, 9.3, 9.4, 9.5, 9.6_

  - [x] 5.4 Write property test for um feedback ativo por área
    - **Property 24: Feedback: um ativo por área, o último vence**
    - **Validates: Requirements 9.1, 9.4**

  - [x] 5.5 Write property test for ordem de exibição
    - **Property 25: Ordem de exibição depende só do feedback atual**
    - **Validates: Requirements 9.4, 9.5**

  - [x] 5.6 Write property test for validação do motivo
    - **Property 26: Validação do motivo do feedback**
    - **Validates: Requirements 9.3, 9.6**

- [x] 6. Mapa de habilidades, plano e exportação
  - [x] 6.1 Implementar `MapaHabilidadesService`
    - Todas as Habilidades da área, prioridade do catálogo, ordenação estável (essencial, importante, desejável), marcação das dominadas sem mudar posição
    - Erro quando a área não tem Habilidades ou o catálogo está indisponível
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.6_

  - [x] 6.2 Write property test for Mapa de Habilidades
    - **Property 9: Mapa de Habilidades**
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.4**

  - [x] 6.3 Implementar `PlanoDesenvolvimentoService`
    - Algoritmo determinístico do design: remover dominadas, ordem topológica com desempate por prioridade e catálogo, etapas de até 5 Habilidades e até 12 semanas, durações com `ceil`, recursos de 1 a 3
    - Resultados `SemHabilidadesPendentes` e `PlanoInviavel` (com a disponibilidade mínima necessária)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.7_

  - [x] 6.4 Write property test for estrutura do plano
    - **Property 10: Estrutura do Plano_de_Desenvolvimento**
    - **Validates: Requirements 5.1, 5.7**

  - [x] 6.5 Write property test for duração do plano
    - **Property 11: Duração do plano**
    - **Validates: Requirements 5.2**

  - [x] 6.6 Write property test for dominadas e pré-requisitos
    - **Property 12: Dominadas excluídas e pré-requisitos respeitados**
    - **Validates: Requirements 5.3, 5.4**

  - [x] 6.7 Implementar `IExportadorPlano` com `MarkdownExportador` e `PdfExportador`
    - Markdown como fonte; PDF com QuestPDF reutilizando o mesmo modelo
    - Conter todas as etapas, Habilidades, durações e recursos
    - _Requirements: 5.5_

  - [x] 6.8 Write property test for exportação em Markdown
    - **Property 13: Exportação em Markdown é fiel ao plano**
    - Escrever um leitor de Markdown de teste para o round-trip
    - **Validates: Requirements 5.5**

- [x] 7. Integração com IA e segurança de prompts
  - [x] 7.1 Implementar `PromptBuilder` e `RedatorDeSegredos`
    - Prompt de sistema fixo (papel, escopo, pt-BR, recusa fora de escopo); texto do Usuário só no papel `user`, delimitado
    - Remover valores de segredos configurados de logs e respostas
    - _Requirements: 8.1, 8.2, 8.4, 8.6_

  - [x] 7.2 Definir `ILlmClient` e implementar `RepetidorFormatoLlm`
    - Máximo de duas chamadas; devolver a primeira resposta válida; se ambas inválidas, devolver erro sem alterar histórico nem perfil
    - Tratar `LlmTimeoutException` após 30 s
    - _Requirements: 1.5, 8.5, 10.5_

  - [x] 7.3 Write property test for isolamento da entrada no prompt
    - **Property 21: Isolamento da entrada do Usuário no prompt**
    - **Validates: Requirements 8.4**

  - [x] 7.4 Write property test for segredos não vazam
    - **Property 23: Segredos não vazam**
    - **Validates: Requirements 8.6**

  - [x] 7.5 Write property test for repetição por formato inválido
    - **Property 22: Repetição por formato inválido**
    - Usar `ILlmClient` falso
    - **Validates: Requirements 8.5**

  - [x] 7.6 Implementar `AnthropicLlmClient` em `Infrastructure`
    - Saída estruturada via tool use, resposta em fluxo (SSE), timeout de 30 s, chave lida de variável de ambiente ou cofre
    - _Requirements: 1.3, 1.5, 8.6, 10.3_

- [x] 8. Persistência e criptografia
  - [x] 8.1 Implementar criptografia de campo AES-256-GCM
    - Conversores de valor do EF Core com nonce único por operação; chave de variável de ambiente ou cofre
    - _Requirements: 7.6_

  - [x] 8.2 Write property test for criptografia de campo
    - **Property 20: Criptografia de campo**
    - **Validates: Requirements 7.6**

  - [x] 8.3 Implementar `ApplicationDbContext`, mapeamentos, migração e `SessaoRepository`
    - Tabelas `sessoes` (id como hash SHA-256), `mensagens` (único por `client_message_id`), `recomendacoes`, `feedbacks`, `planos`; colunas sensíveis cifradas
    - Uma nova tentativa em falha de gravação; consultas sempre filtradas pela Sessao
    - Definir `IApplicationDbContext` e `ISessaoRepository` em `Application`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 8.4 Write property test for round-trip de persistência da Sessao
    - **Property 19: Round-trip de persistência da Sessao**
    - Usar PostgreSQL via Testcontainers
    - **Validates: Requirements 7.1, 7.2**

- [x] 9. Handlers da aplicação e fiação
  - [x] 9.1 Implementar `SessaoCriarCommand`, `SessaoObterQuery`, `SessaoExcluirCommand` e `MensagemEnviarCommand`
    - Criação com identificador único e histórico vazio; restauração completa; exclusão com confirmação explícita
    - Mensagem idempotente por `clientMessageId`; fluxo: validar, registrar, chamar LLM com `RepetidorFormatoLlm`, aplicar `MaquinaEntrevista`, persistir
    - Mensagens de erro via `IStringLocalizer<MentorLocalization>`
    - _Requirements: 1.2, 1.3, 1.5, 1.6, 2.2, 2.3, 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 9.2 Write property tests for criação de Sessao e reenvio idempotente
    - **Property 2: Criação de Sessao**
    - **Property 3: Reenvio de mensagem é idempotente**
    - **Validates: Requirements 1.2, 1.5**

  - [x] 9.3 Implementar comandos e queries de entrevista, recomendação, feedback e mapa
    - `EntrevistaEncerrarCommand`, `RecomendacaoListarQuery` (inclui geração e recálculo), `FeedbackRegistrarCommand` (+ validator do motivo), `MapaHabilidadesObterQuery`
    - Tratar falha de IA com perfil inalterado e opção de tentar novamente
    - _Requirements: 2.5, 2.7, 3.1, 3.4, 3.6, 3.7, 4.1, 4.6, 9.2, 9.5, 9.6, 9.7_

  - [x] 9.4 Implementar `PlanoGerarCommand`, `PlanoGerarCommandValidator` e `PlanoExportarQuery`
    - Validar disponibilidade de 1 a 168 horas, sem alterar o perfil enquanto ausente
    - Preservar perfil e planos anteriores em falha de geração ou exportação
    - _Requirements: 5.1, 5.5, 5.6, 5.7, 5.8_

  - [x] 9.5 Write property test for validação da disponibilidade semanal
    - **Property 14: Validação da disponibilidade semanal**
    - **Validates: Requirements 5.6**

  - [x] 9.6 Registrar behaviours e a fiação de DI
    - Pipeline na ordem: `UnhandledExceptionBehaviour`, `AuthorizationBehaviour` (posse da Sessao), `ValidationBehaviour`, `PerformanceBehaviour`, `LoggingBehaviour`
    - Completar `AddApplication` e `AddInfrastructure` (ports, repositório, exportadores, LLM, catálogo, redator de segredos)
    - Log sem corpo de mensagens do Usuário e sem segredos
    - _Requirements: 6.4, 7.6, 8.6_

  - [x] 9.7 Write unit tests for fluxos de falha
    - Timeout de 30 s e reenvio, falha de IA (3.7), catálogo ausente (4.6), falha de exportação (5.8), falha de persistência com retry (7.3), exclusão e Sessao inexistente (7.4, 7.5), falha de feedback (9.7)
    - _Requirements: 1.5, 3.7, 4.6, 5.8, 7.3, 7.4, 7.5, 9.7, 10.5_

- [x] 10. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. API (WebUI)
  - [x] 11.1 Criar `Program`/`Startup`, `ApiControllerBase` e middlewares
    - Cookie de sessão `HttpOnly; Secure; SameSite=Strict`, middleware de Sessao (cookie inválido tratado como Sessao vazia), `ProblemDetails` genérico para exceções, limite de requisições por Sessao e por IP (429 localizado), pt-BR como cultura padrão
    - _Requirements: 7.5, 8.6, 10.3_

  - [x] 11.2 Implementar os controllers e o SSE
    - Rotas do design: `POST /api/sessoes`, `GET /api/sessoes/atual`, `POST .../mensagens` (SSE), `POST .../entrevista/encerrar`, `GET .../recomendacoes`, `PUT .../recomendacoes/{areaId}/feedback`, `GET /api/areas/{areaId}/habilidades`, `POST .../planos`, `GET .../planos/{id}/exportar`, `DELETE /api/sessoes/atual`
    - Controllers finos delegando ao `Mediator`
    - _Requirements: 1.2, 1.3, 2.5, 3.1, 4.1, 5.1, 5.5, 7.2, 7.4, 9.2, 10.3_

  - [x] 11.3 Write integration tests for a API
    - Fluxo completo com LLM falso e PostgreSQL via Testcontainers; atributos do cookie, isolamento entre Sessoes, colunas cifradas no banco, ordem do pipeline MediatR
    - _Requirements: 7.4, 7.5, 7.6, 8.6_

- [x] 12. Frontend (React + TypeScript)
  - [x] 12.1 Criar o projeto Vite React 18 + TypeScript
    - Configurar Vitest, Testing Library, fast-check, Playwright e axe-core
    - Tipos dos contratos da API e cliente HTTP com cookie de sessão
    - _Requirements: 10.1, 10.2_

  - [x] 12.2 Implementar as telas de Boas-vindas e Conversa
    - `ChatPanel` com contador, indicador de processamento, bloqueio de envio e botão de reenvio; `ProgressoEntrevista`; hook `useMensagemEnvio` que preserva o texto em rejeição ou falha e dispara o erro de demora aos 30 s sem primeiro trecho
    - _Requirements: 1.1, 1.4, 1.5, 1.6, 2.8, 10.5_

  - [x] 12.3 Implementar as telas de Recomendações, Mapa de Habilidades e Plano
    - `RecomendacaoCard` com pontuação, justificativa, controles "útil/não útil" e motivo de até 500 caracteres; `HabilidadeLista`; `PlanoEtapas` com exportação PDF/Markdown; `AvisoIA` visível nas três telas e ausente no fluxo de erro; aviso de baixa confiança
    - Layout responsivo de 360 a 1920 px e semântica acessível (WCAG 2.1 A e AA)
    - _Requirements: 3.6, 4.1, 5.5, 8.3, 8.5, 9.1, 9.3, 9.6, 10.1, 10.2_

  - [x] 12.4 Ligar o frontend à API
    - Consumir SSE para a resposta do assistente, recomendações, feedback, mapa, plano, exportação e exclusão de dados com confirmação; tratar os erros com opção de tentar novamente
    - _Requirements: 1.3, 3.7, 4.6, 5.8, 7.4, 9.7, 10.3, 10.4_

  - [x] 12.5 Write unit tests for componentes
    - Indicador de processamento e bloqueio de envio (1.4), aviso de IA presente nas três telas e ausente no erro (8.3, 8.5), controles de feedback (9.1, 9.3), preservação do texto digitado
    - _Requirements: 1.4, 8.3, 8.5, 9.1, 9.3_

  - [x] 12.6 Write testes Playwright de responsividade e acessibilidade
    - Viewports de 360 a 1920 px sem rolagem horizontal; 0 violações axe-core WCAG 2.1 A e AA nas telas de conversa, recomendações e plano
    - _Requirements: 10.1, 10.2_

- [x] 13. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties; unit and integration tests validate examples and edge cases
- Requisitos de desempenho (3.1, 4.1, 7.2, 9.2, 10.3, 10.4), verificação manual de acessibilidade e testes com LLM real ficam fora das tarefas de código, conforme a estratégia de testes do design

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "12.1"] },
    { "id": 1, "tasks": ["1.2", "12.2"] },
    { "id": 2, "tasks": ["2.1", "4.1", "4.4", "5.1", "5.3", "6.1", "6.3", "6.7", "7.1", "7.2", "8.1", "12.3"] },
    { "id": 3, "tasks": ["2.2", "2.3", "4.2", "4.3", "4.5", "4.6", "4.7", "5.2", "5.4", "5.5", "5.6", "6.2", "6.4", "6.5", "6.6", "6.8", "7.3", "7.4", "7.5", "7.6", "8.2", "8.3"] },
    { "id": 4, "tasks": ["2.4", "2.5", "2.6", "2.7", "8.4", "9.1", "9.3", "9.4"] },
    { "id": 5, "tasks": ["9.2", "9.5", "9.6", "9.7"] },
    { "id": 6, "tasks": ["11.1"] },
    { "id": 7, "tasks": ["11.2"] },
    { "id": 8, "tasks": ["11.3", "12.4"] },
    { "id": 9, "tasks": ["12.5"] },
    { "id": 10, "tasks": ["12.6"] }
  ]
}
```
