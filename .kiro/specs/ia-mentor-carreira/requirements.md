# Requirements Document

## Introduction

O IA Mentor de Carreira é um sistema que utiliza inteligência artificial para ajudar pessoas a descobrirem caminhos profissionais em tecnologia. O sistema identifica os interesses do usuário por meio de uma conversa guiada, sugere áreas de atuação compatíveis (por exemplo, desenvolvimento backend, dados, segurança, UX, DevOps) e indica as habilidades mais importantes para o desenvolvimento da carreira em cada área, organizadas em um plano de aprendizado.

## Glossary

- **Sistema**: O IA Mentor de Carreira, composto pela interface de usuário, pela API e pelo serviço de IA.
- **Usuário**: Pessoa que utiliza o Sistema para explorar carreiras em tecnologia.
- **Perfil_do_Usuario**: Conjunto de dados do Usuário: interesses, experiência prévia, formação, disponibilidade de estudo e objetivos.
- **Assistente_IA**: Componente do Sistema que usa um modelo de linguagem (LLM) para conduzir a conversa e gerar recomendações.
- **Entrevista_de_Interesses**: Conversa guiada em que o Assistente_IA faz perguntas ao Usuário para construir o Perfil_do_Usuario.
- **Area_de_Atuacao**: Área profissional de tecnologia, como Desenvolvimento Backend, Desenvolvimento Frontend, Dados, IA, Segurança, UX/UI, DevOps, QA e Gestão de Produtos.
- **Recomendacao_de_Area**: Sugestão de uma Area_de_Atuacao, com pontuação de aderência de 0 a 100 e justificativa em texto.
- **Habilidade**: Competência técnica ou comportamental associada a uma Area_de_Atuacao.
- **Mapa_de_Habilidades**: Lista ordenada de Habilidades de uma Area_de_Atuacao, cada uma com nível de prioridade (essencial, importante ou desejável).
- **Plano_de_Desenvolvimento**: Roteiro de aprendizado em etapas, com Habilidades, ordem sugerida e recursos de estudo.
- **Catalogo_de_Carreiras**: Base estruturada de Areas_de_Atuacao e Habilidades usada como referência pelo Assistente_IA.
- **Sessao**: Interação contínua de um Usuário com o Sistema, com histórico de mensagens.

## Requirements

### Requirement 1: Iniciar conversa com o mentor

**User Story:** Como usuário, quero iniciar uma conversa com o mentor, para que eu possa explorar carreiras em tecnologia sem precisar saber por onde começar.

#### Acceptance Criteria

1. WHEN o Usuário acessa o Sistema pela primeira vez, THE Sistema SHALL exibir, em até 2 segundos, uma mensagem de boas-vindas em português do Brasil e um botão para iniciar a Entrevista_de_Interesses.
2. WHEN o Usuário aciona o botão para iniciar a Entrevista_de_Interesses, THE Sistema SHALL criar uma Sessao com identificador único, histórico de mensagens vazio e a data e hora de criação.
3. WHEN o Usuário envia uma mensagem de 1 a 2.000 caracteres, THE Assistente_IA SHALL responder em português do Brasil.
4. WHILE o Assistente_IA gera uma resposta, THE Sistema SHALL exibir um indicador de processamento ao Usuário e impedir o envio de nova mensagem até que a resposta seja concluída ou falhe.
5. IF o serviço de IA não responde em 30 segundos, THEN THE Sistema SHALL interromper o indicador de processamento, exibir uma mensagem de erro indicando a falha de resposta, preservar a mensagem do Usuário no histórico da Sessao e oferecer a opção de reenviar a mensagem. WHEN o Usuário aciona a opção de reenviar, THE Sistema SHALL reenviar a mensagem original preservada e tratá-la como a mesma entrada do histórico, sem criar entrada duplicada.
6. IF o Usuário tenta enviar uma mensagem vazia, composta apenas por espaços em branco ou com mais de 2.000 caracteres, THEN THE Sistema SHALL rejeitar o envio, exibir uma mensagem indicando o limite de 1 a 2.000 caracteres e manter o texto digitado no campo de entrada.

### Requirement 2: Entrevista de interesses

**User Story:** Como usuário, quero responder a perguntas sobre meus interesses e experiências, para que o mentor entenda meu perfil.

#### Acceptance Criteria

1. WHEN a Entrevista_de_Interesses é iniciada, THE Assistente_IA SHALL fazer perguntas sobre cada uma das seis categorias obrigatórias: interesses, experiência prévia, formação, estilo de trabalho, disponibilidade semanal de estudo e objetivos profissionais.
2. WHEN o Usuário responde a uma pergunta com informação utilizável, THE Sistema SHALL atualizar o Perfil_do_Usuario com a informação extraída da resposta e marcar a categoria correspondente como respondida antes de enviar a próxima pergunta.
3. THE Assistente_IA SHALL fazer exatamente uma pergunta por mensagem durante a Entrevista_de_Interesses.
4. WHEN as seis categorias obrigatórias estiverem marcadas como respondidas ou não respondidas, THE Sistema SHALL marcar a Entrevista_de_Interesses como concluída.
5. WHEN o Usuário solicita encerrar a Entrevista_de_Interesses antes de concluí-la, THE Sistema SHALL marcar a Entrevista_de_Interesses como encerrada, gerar as Recomendacoes_de_Area com base nas informações já coletadas e exibir a lista das categorias sem resposta.
6. IF a resposta do Usuário não contém informação utilizável para a pergunta feita, THEN THE Assistente_IA SHALL reformular a pergunta uma única vez e, se a segunda resposta também não contiver informação utilizável, THE Sistema SHALL registrar a categoria como não respondida e passar à próxima categoria.
7. IF o Usuário solicita encerrar a Entrevista_de_Interesses e nenhuma categoria foi respondida, THEN THE Sistema SHALL manter a Entrevista_de_Interesses ativa, não gerar Recomendacoes_de_Area e informar ao Usuário que é necessário responder ao menos uma categoria.
8. IF o Usuário envia uma resposta com mais de 2.000 caracteres, THEN THE Sistema SHALL recusar a mensagem, preservar o texto digitado e exibir um erro indicando o limite de 2.000 caracteres.

### Requirement 3: Recomendação de áreas de atuação

**User Story:** Como usuário, quero receber sugestões de áreas de atuação em tecnologia, para que eu saiba quais caminhos combinam comigo.

#### Acceptance Criteria

1. WHEN a Entrevista_de_Interesses é concluída, THE Sistema SHALL gerar de 3 a 5 Recomendacoes_de_Area, sem repetir Area_de_Atuacao, ordenadas pela pontuação de aderência da maior para a menor, e exibi-las em até 10 segundos em 95% das requisições.
2. THE Sistema SHALL incluir em cada Recomendacao_de_Area o nome da Area_de_Atuacao, a pontuação de aderência como número inteiro de 0 a 100 e uma justificativa de 1 a 500 caracteres que cite ao menos um item do Perfil_do_Usuario.
3. THE Sistema SHALL selecionar as Areas_de_Atuacao das Recomendacoes_de_Area somente entre as presentes no Catalogo_de_Carreiras.
4. WHEN o Usuário atualiza seu Perfil_do_Usuario, THE Sistema SHALL recalcular as Recomendacoes_de_Area, mantendo as regras dos critérios 1 a 3, e exibir o resultado atualizado em até 10 segundos em 95% das requisições.
5. WHEN o Usuário solicita mais detalhes sobre uma Area_de_Atuacao, THE Assistente_IA SHALL descrever o dia a dia típico, as funções e níveis de senioridade da área (faixa de atuação) e as perspectivas de mercado da área, em português do Brasil.
6. IF o Perfil_do_Usuario contém menos de três categorias respondidas, THEN THE Sistema SHALL exibir junto às recomendações um aviso de que elas têm baixa confiança e uma sugestão de continuar a Entrevista_de_Interesses.
7. IF o Catalogo_de_Carreiras contém menos de 3 Areas_de_Atuacao ou o serviço de IA falha ao gerar as recomendações, THEN THE Sistema SHALL exibir uma mensagem de erro indicando que as recomendações não puderam ser geradas, manter o Perfil_do_Usuario inalterado e oferecer ao Usuário a opção de tentar novamente.

### Requirement 4: Mapa de habilidades

**User Story:** Como usuário, quero saber quais habilidades são importantes em cada área, para que eu priorize meus estudos.

#### Acceptance Criteria

1. WHEN o Usuário seleciona uma Area_de_Atuacao recomendada, THE Sistema SHALL exibir o Mapa_de_Habilidades da área com todas as Habilidades dessa área registradas no Catalogo_de_Carreiras, em até 2 segundos em 95% das requisições.
2. THE Sistema SHALL classificar cada Habilidade do Mapa_de_Habilidades com exatamente um nível de prioridade entre essencial, importante e desejável, conforme o nível registrado no Catalogo_de_Carreiras.
3. THE Sistema SHALL ordenar o Mapa_de_Habilidades por prioridade na sequência essenciais, importantes e desejáveis, mantendo a ordem do Catalogo_de_Carreiras entre Habilidades do mesmo nível.
4. WHERE o Usuário informou Habilidades que já possui, THE Sistema SHALL marcar como já dominadas, no Mapa_de_Habilidades, as Habilidades informadas que constam na área selecionada, mantendo-as na posição definida pela prioridade.
5. THE Sistema SHALL incluir em cada Habilidade uma descrição de 1 a 3 frases, com no máximo 300 caracteres.
6. IF o Catalogo_de_Carreiras não contém Habilidades para a Area_de_Atuacao selecionada ou não está disponível, THEN THE Sistema SHALL exibir uma mensagem de erro indicando que o Mapa_de_Habilidades não pôde ser carregado, não exibir um mapa parcial e oferecer ao Usuário a opção de tentar novamente.

### Requirement 5: Plano de desenvolvimento

**User Story:** Como usuário, quero um plano de desenvolvimento para a área escolhida, para que eu tenha um roteiro concreto de aprendizado.

#### Acceptance Criteria

1. WHEN o Usuário solicita um Plano_de_Desenvolvimento para uma Area_de_Atuacao presente no Catalogo_de_Carreiras, THE Sistema SHALL gerar um plano em etapas numeradas sequencialmente a partir de 1, cada etapa com de 1 a 5 Habilidades, duração estimada em semanas (inteiro de 1 a 12) e de 1 a 3 recursos de estudo.
2. THE Sistema SHALL calcular a duração total estimada do Plano_de_Desenvolvimento em semanas, como a soma das durações das etapas, usando a disponibilidade semanal de estudo (em horas por semana) informada no Perfil_do_Usuario, de modo que a duração de cada etapa seja igual ao total de horas estimadas das suas Habilidades dividido pela disponibilidade semanal, arredondado para cima.
3. THE Sistema SHALL excluir do Plano_de_Desenvolvimento todas as Habilidades marcadas como já dominadas.
4. THE Sistema SHALL posicionar cada Habilidade em uma etapa igual ou posterior à etapa de cada uma das suas Habilidades pré-requisito não dominadas, e, dentro da mesma etapa, após esses pré-requisitos.
5. WHEN o Usuário solicita exportar o Plano_de_Desenvolvimento e escolhe PDF ou Markdown, THE Sistema SHALL gerar um arquivo no formato escolhido contendo todas as etapas, Habilidades, durações e recursos do plano, em até 10 segundos.
6. IF o Perfil_do_Usuario não contém disponibilidade semanal de estudo, THEN THE Sistema SHALL solicitar essa informação ao Usuário, não gerar o Plano_de_Desenvolvimento até que um valor de 1 a 168 horas por semana seja informado e manter o Perfil_do_Usuario inalterado enquanto isso.
7. IF todas as Habilidades da Area_de_Atuacao estão marcadas como já dominadas, THEN THE Sistema SHALL informar ao Usuário que não há Habilidades pendentes e não gerar um Plano_de_Desenvolvimento vazio.
8. IF a geração ou a exportação do Plano_de_Desenvolvimento falha, THEN THE Sistema SHALL exibir ao Usuário uma mensagem de erro indicando a falha, oferecer a opção de tentar novamente e preservar o Perfil_do_Usuario e os planos já gerados na Sessao.

### Requirement 6: Catálogo de carreiras

**User Story:** Como mantenedor, quero um catálogo estruturado de carreiras e habilidades, para que as recomendações sejam consistentes e verificáveis.

#### Acceptance Criteria

1. THE Catalogo_de_Carreiras SHALL armazenar para cada Area_de_Atuacao um nome único de 1 a 100 caracteres, uma descrição de 1 a 1000 caracteres, de 1 a 100 Habilidades associadas e o nível de prioridade de cada Habilidade (essencial, importante ou desejável).
2. THE Catalogo_de_Carreiras SHALL armazenar para cada Habilidade um identificador único, um nome de 1 a 100 caracteres e a lista de Habilidades pré-requisito, com 0 a 10 itens, sem repetição e sem referência à própria Habilidade.
3. WHEN o Catalogo_de_Carreiras é carregado, THE Sistema SHALL validar que cada pré-requisito referencia uma Habilidade existente no catálogo e que não há dependências circulares, em até 5 segundos para um catálogo com até 20 Areas_de_Atuacao e 1000 Habilidades.
4. IF o Catalogo_de_Carreiras contém uma referência inválida ou dependência circular, THEN THE Sistema SHALL registrar em log um erro que identifica a Habilidade afetada e o tipo de falha, recusar o carregamento do catálogo e manter em uso o último Catalogo_de_Carreiras carregado com sucesso, se existir.
5. THE Sistema SHALL serializar o Catalogo_de_Carreiras em JSON válido, codificado em UTF-8.
6. WHEN o Sistema recebe um JSON de Catalogo_de_Carreiras, THE Sistema SHALL carregá-lo em um objeto de domínio.
7. IF o JSON recebido é malformado ou não contém algum campo obrigatório de uma Area_de_Atuacao ou Habilidade, THEN THE Sistema SHALL registrar em log um erro que identifica o campo ou a posição da falha e recusar o carregamento do catálogo.
8. FOR ALL Catalogo_de_Carreiras válidos, carregar, serializar e carregar novamente SHALL produzir um catálogo equivalente, com as mesmas Areas_de_Atuacao, Habilidades, níveis de prioridade e pré-requisitos, independentemente da ordem dos itens nas listas de pré-requisitos (propriedade de round-trip).

### Requirement 7: Persistência e privacidade dos dados

**User Story:** Como usuário, quero retomar minhas conversas e resultados, para que eu não precise repetir informações.

#### Acceptance Criteria

1. WHEN o Assistente_IA conclui a resposta a uma mensagem do Usuário, THE Sistema SHALL persistir a mensagem do Usuário e a resposta do Assistente_IA no histórico da Sessao, em ordem cronológica, em até 2 segundos após o fim da resposta.
2. WHEN o Usuário retorna a uma Sessao existente, THE Sistema SHALL restaurar o histórico, o Perfil_do_Usuario, as Recomendacoes_de_Area e os Planos_de_Desenvolvimento da Sessao, com conteúdo idêntico ao último estado persistido, em até 3 segundos, em 95% das requisições.
3. IF a persistência de uma mensagem ou resposta falha, THEN THE Sistema SHALL tentar novamente uma vez, e persistindo a falha, exibir ao Usuário uma mensagem de erro indicando que o histórico não foi salvo e manter a mensagem exibida na interface para reenvio.
4. WHEN o Usuário solicita excluir seus dados, THE Sistema SHALL solicitar confirmação explícita do Usuário e, após a confirmação, remover o Perfil_do_Usuario, o histórico, as Recomendacoes_de_Area, os Planos_de_Desenvolvimento e os feedbacks associados em até 24 horas.
5. WHEN a exclusão de dados é concluída, THE Sistema SHALL tornar os dados excluídos irrecuperáveis pelo Usuário e exibir, em uma Sessao posterior com o mesmo identificador, uma Sessao vazia ou uma indicação de que a Sessao não existe.
6. THE Sistema SHALL armazenar o Perfil_do_Usuario, o histórico de mensagens, as Recomendacoes_de_Area e os Planos_de_Desenvolvimento de forma criptografada em repouso.

### Requirement 8: Segurança e confiabilidade da IA

**User Story:** Como usuário, quero respostas confiáveis e seguras do mentor, para que eu possa confiar nas orientações recebidas.

#### Acceptance Criteria

1. WHEN o Assistente_IA gera uma resposta, THE Assistente_IA SHALL limitar o conteúdo da resposta a orientação de carreira em tecnologia, abrangendo as Areas_de_Atuacao, Habilidades, Plano_de_Desenvolvimento e o Perfil_do_Usuario.
2. IF o Usuário envia uma mensagem fora do escopo de carreira em tecnologia, THEN THE Assistente_IA SHALL responder sem atender ao pedido fora do escopo, informar em uma mensagem que o Sistema trata somente de carreira em tecnologia e fazer uma pergunta ou sugestão que retome o tema de carreira, mantendo a Sessao e o histórico inalterados.
3. WHEN o Sistema exibe uma tela de Recomendacao_de_Area, Mapa_de_Habilidades ou Plano_de_Desenvolvimento, THE Sistema SHALL exibir, visível sem ação do Usuário, um aviso em português do Brasil de que as sugestões são geradas por IA e não substituem orientação profissional.
4. IF uma mensagem do Usuário contém texto que tenta alterar as instruções, o papel ou o escopo do Assistente_IA, THEN THE Sistema SHALL tratar o texto apenas como conteúdo da conversa, manter o comportamento e o escopo definidos para o Assistente_IA e responder conforme o critério 2 quando o pedido estiver fora do escopo.
5. IF o serviço de IA retorna uma resposta que não segue o formato estruturado esperado, THEN THE Sistema SHALL repetir a requisição uma vez e, se a segunda resposta também não seguir o formato, descartar as duas respostas, exibir ao Usuário uma mensagem de erro com a opção de reenviar a mensagem, preservar o histórico da Sessao e o Perfil_do_Usuario sem alterações e não exibir a tela de Recomendacao_de_Area nem o aviso de IA do critério 3, de modo que o fluxo de erro substitua totalmente a tela.
6. THE Sistema SHALL manter as chaves de acesso ao serviço de IA fora do código-fonte, fora dos logs e fora de qualquer resposta enviada à interface do Usuário.

### Requirement 9: Feedback sobre recomendações

**User Story:** Como usuário, quero fornecer feedback sobre as recomendações, para que o mentor melhore as sugestões.

#### Acceptance Criteria

1. WHEN o Sistema exibe uma Recomendacao_de_Area, THE Sistema SHALL oferecer ao Usuário os controles "útil" e "não útil" para essa Recomendacao_de_Area, com no máximo um feedback ativo por Recomendacao_de_Area por Sessao.
2. WHEN o Usuário marca uma Recomendacao_de_Area como "útil" ou "não útil", THE Sistema SHALL registrar o feedback associado à Sessao e à Area_de_Atuacao e exibir ao Usuário a confirmação do registro em até 2 segundos, em 95% das requisições.
3. WHEN o Usuário marca uma Recomendacao_de_Area como "não útil", THE Sistema SHALL solicitar um motivo opcional em texto livre de 0 a 500 caracteres, permitindo ao Usuário enviar o feedback sem preencher o motivo.
4. WHEN o Usuário altera o feedback de uma Recomendacao_de_Area, THE Sistema SHALL substituir o feedback anterior pelo novo, e, se o feedback anterior era "não útil", THE Sistema SHALL remover o efeito dele sobre as Recomendacoes_de_Area recalculadas.
5. WHEN o Usuário registra feedback "não útil" para uma Area_de_Atuacao, THE Sistema SHALL recalcular as Recomendacoes_de_Area da mesma Sessao e posicionar essa área abaixo de todas as áreas sem feedback "não útil", sem removê-la da lista e mantendo a ordenação das demais por pontuação de aderência.
6. IF o motivo informado excede 500 caracteres, THEN THE Sistema SHALL recusar o envio, exibir mensagem de erro indicando o limite de caracteres e preservar o texto digitado pelo Usuário.
7. IF o registro do feedback falha, THEN THE Sistema SHALL exibir mensagem de erro, oferecer a opção de tentar novamente e manter inalteradas as Recomendacoes_de_Area exibidas.

### Requirement 10: Desempenho e acessibilidade

**User Story:** Como usuário, quero usar o sistema com boa velocidade e acessibilidade, para que a experiência seja fluida em qualquer dispositivo.

#### Acceptance Criteria

1. THE Sistema SHALL exibir as telas de conversa, recomendações e plano, em larguras de viewport de 360 px a 1920 px, sem rolagem horizontal e sem sobreposição ou corte de texto e controles.
2. THE Sistema SHALL atender a todos os critérios de sucesso de nível A e AA da WCAG 2.1 nas telas de conversa, recomendações e plano, com 0 violações reportadas por verificação automatizada de acessibilidade e 0 falhas em verificação manual de navegação por teclado e leitor de tela.
3. WHEN o Usuário envia uma mensagem, THE Sistema SHALL exibir o primeiro trecho da resposta do Assistente_IA em até 5 segundos, em pelo menos 95% das requisições medidas em uma amostra mínima de 100 requisições.
4. WHEN o Usuário abre uma Recomendacao_de_Area ou um Mapa_de_Habilidades, THE Sistema SHALL exibir o conteúdo completo em até 2 segundos, em pelo menos 95% das requisições medidas em uma amostra mínima de 100 requisições.
5. IF o primeiro trecho da resposta do Assistente_IA não é exibido em até 30 segundos após o envio da mensagem, THEN THE Sistema SHALL exibir uma mensagem de erro indicando a demora e oferecer a opção de reenviar a mensagem, preservando o texto digitado e o histórico da Sessao.